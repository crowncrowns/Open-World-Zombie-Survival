"""Targeted, authored replacement surfaces for three existing KursHub props.

Call apply(staged_root, audit_work) from Blender after other prop authoring has
finished. This module is intentionally inert on import. It never rebuilds the
whole library and does not save the manifest: returned propUpdates must be
merged by the caller after the actual nine FBX re-import checks have passed.
"""
from pathlib import Path
from math import sin, cos, pi, sqrt, exp, isfinite
import json
import hashlib
import shutil
import sys

import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree


TARGETS = ("KH_Sandbag", "KH_SupplySackStack", "KH_GoodsFood")


def _n(level, high, medium, low):
    return (high, medium, low)[level]


def _bounds(obj):
    return ([min(v.co[i] for v in obj.data.vertices) for i in range(3)],
            [max(v.co[i] for v in obj.data.vertices) for i in range(3)])


def _fit(points, bounds):
    lo = [min(p[i] for p in points) for i in range(3)]
    hi = [max(p[i] for p in points) for i in range(3)]
    assert all(hi[i] > lo[i] for i in range(3))
    return [tuple(bounds[0][i] + (p[i]-lo[i])/(hi[i]-lo[i]) *
                  (bounds[1][i]-bounds[0][i]) for i in range(3)) for p in points]


def _install(obj, vertices, faces, uv=None, seam_period=None):
    """Replace data, preserving the original object, transform and material group."""
    old = obj.data
    materials = list(old.materials)
    mesh = bpy.data.meshes.new(obj.name + " Q8 manufactured surface")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if bm.calc_volume(signed=True) < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    assert not any(not e.is_manifold for e in bm.edges), (obj.name, "open replacement")
    assert not any(f.calc_area() <= 1e-12 for f in bm.faces), (obj.name, "zero-area replacement")
    assert bm.calc_volume(signed=True) > 0, (obj.name, "nonpositive replacement")
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    layer = mesh.uv_layers.new(name="PhysicalUV")
    for poly in mesh.polygons:
        values = [uv[i] if uv is not None else (mesh.vertices[i].co.x/2,
                  mesh.vertices[i].co.z/2) for i in poly.vertices]
        cross = seam_period and max(p[0] for p in values)-min(p[0] for p in values) > seam_period*.5
        if cross:
            values = [(u+seam_period if u < seam_period*.5 else u, v) for u,v in values]
        uv_area = abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in
                          zip(values,values[1:]+values[:1])))*.5
        if uv_area <= 1e-12:
            # A revolved/end cap must have a planar physical chart rather than
            # all corners sharing the axial UV coordinate of its outer ring.
            axis=max(range(3),key=lambda i:abs(poly.normal[i]))
            axes=((1,2),(0,2),(0,1))[axis]
            values=[(mesh.vertices[i].co[axes[0]]/2,
                     mesh.vertices[i].co[axes[1]]/2) for i in poly.vertices]
        for li, value in zip(poly.loop_indices, values):
            u, v = value
            layer.data[li].uv = (u, v)
        poly.use_smooth = True
    mesh.set_sharp_from_angle(angle=pi/3)
    for material in materials:
        mesh.materials.append(material)
    obj.data = mesh
    obj["Hub8SoftGoodsRevision"] = 8
    obj["Hub8SoftGoodsReason"] = "Constructed smooth filled cloth / manufactured container profile; original object and material retained"
    # Do not remove original datablocks: original object identity is the contract;
    # the unused control mesh can remain available for authoring comparison.
    return obj


def _tube(obj, points, radius, sides, closed=False):
    points = [Vector(p) for p in points]
    assert len(points) >= (4 if closed else 2)
    vertices, previous = [], None
    for j, p in enumerate(points):
        before = points[(j-1) % len(points)] if closed else points[max(j-1, 0)]
        after = points[(j+1) % len(points)] if closed else points[min(j+1, len(points)-1)]
        tangent = (after-before).normalized()
        if previous is None:
            seed = Vector((0, 0, 1)) if abs(tangent.z) < .9 else Vector((1, 0, 0))
            u = tangent.cross(seed).normalized()
        else:
            u = (previous-tangent*previous.dot(tangent)).normalized()
        v = tangent.cross(u).normalized()
        previous = u
        for k in range(sides):
            a = k*2*pi/sides
            vertices.append(tuple(p+radius*(u*cos(a)+v*sin(a))))
    faces = []
    count = len(points) if closed else len(points)-1
    for j in range(count):
        q = (j+1) % len(points)
        for k in range(sides):
            faces.append((j*sides+k, j*sides+(k+1)%sides,
                          q*sides+(k+1)%sides, q*sides+k))
    if not closed:
        faces.extend([tuple(range(sides-1, -1, -1)),
                      tuple((len(points)-1)*sides+k for k in range(sides))])
    distance, uv = 0., []
    for j, p in enumerate(points):
        if j:
            distance += (p-points[j-1]).length
        uv.extend((k/sides*2*pi*radius/2, distance/2) for k in range(sides))
    return _install(obj, vertices, faces, uv, pi*radius)


def _lathe(obj, center, profile, sides, radial=None, close_profile=False):
    """Closed revolution around native Blender Z, including real annular bands."""
    vertices, uv = [], []
    max_radius = max(r for h, r in profile)
    for j, (height, radius) in enumerate(profile):
        assert radius > 0
        for k in range(sides):
            a = k*2*pi/sides
            r = radius * (radial(a, j) if radial else 1.)
            vertices.append((center[0]+cos(a)*r, center[1]+sin(a)*r, center[2]+height))
            uv.append((k/sides*2*pi*max_radius/2, height/2))
    faces = []
    rows = len(profile) if close_profile else len(profile)-1
    for j in range(rows):
        q = (j+1) % len(profile)
        for k in range(sides):
            faces.append((j*sides+k, j*sides+(k+1)%sides,
                          q*sides+(k+1)%sides, q*sides+k))
    if not close_profile:
        faces.extend([tuple(range(sides-1, -1, -1)),
                      tuple((len(profile)-1)*sides+k for k in range(sides))])
    return _install(obj, vertices, faces, uv, pi*max_radius)


def _pillow(obj, bounds, level, variant):
    """A squat filled textile envelope with compressed sewn ends, not a pellet."""
    rows, sides = _n(level, 25, 17, 11), _n(level, 48, 32, 20)
    vertices, uv = [], []
    for j in range(rows):
        t = -1+2*j/(rows-1)
        mass = max(0., 1-abs(t)**2.7)**.42
        width = .59+.41*max(0., sin(pi*(t+1)/2))**.50
        depth = .115+.885*mass
        for k in range(sides):
            a = k*2*pi/sides
            cy, sz = cos(a), sin(a)
            y = (1 if cy >= 0 else -1)*abs(cy)**.78*width
            z = (1 if sz >= 0 else -1)*abs(sz)**.85*depth
            crease = .033*abs(t)**4*sin(a*7+variant*.7+t*4)
            y *= 1+crease
            z *= 1+crease*.75
            # Gravity broadens the resting face without a zero-area clamp.
            if z < -.65:
                z = -.65+(z+.65)*.48
            y += .025*sin(t*pi)*(1-t*t)*sin(variant*.9)
            vertices.append((t, y, z))
            uv.append((k/sides*(bounds[1][1]-bounds[0][1]+bounds[1][2]-bounds[0][2]),
                       (t+1)*(bounds[1][0]-bounds[0][0])/4))
    vertices = _fit(vertices, bounds)
    faces = [tuple(range(sides-1, -1, -1))]
    for j in range(rows-1):
        for k in range(sides):
            faces.append((j*sides+k, j*sides+(k+1)%sides,
                          (j+1)*sides+(k+1)%sides, (j+1)*sides+k))
    faces.append(tuple((rows-1)*sides+k for k in range(sides)))
    _install(obj, vertices, faces, uv,
             bounds[1][1]-bounds[0][1]+bounds[1][2]-bounds[0][2])


def _upright_sack(obj, bounds, level, variant):
    sides, rows = _n(level, 48, 32, 20), _n(level, 27, 19, 13)
    curve = [(0,.72),(.04,.94),(.15,1),(.43,1.025),(.64,.95),
             (.77,.79),(.86,.48),(.905,.255),(.943,.28),(.985,.48),(1,.51)]
    def radius(u):
        for (a, ra), (b, rb) in zip(curve, curve[1:]):
            if a <= u <= b:
                f=(u-a)/(b-a); f=f*f*(3-2*f)
                return ra+(rb-ra)*f
        return curve[-1][1]
    vertices, uv = [], []
    for j in range(rows):
        u=j/(rows-1)
        for k in range(sides):
            a=k*2*pi/sides
            wrinkles = 1+.075*cos(a*8+variant)*exp(-((u-.88)/.19)**2)
            r=radius(u)*wrinkles
            vertices.append((cos(a)*r+.035*sin(u*pi)*cos(variant),
                             sin(a)*r+.045*sin(u*pi)*sin(variant),u))
            uv.append((k/sides*pi*(bounds[1][0]-bounds[0][0])/2,
                       u*(bounds[1][2]-bounds[0][2])/2))
    vertices=_fit(vertices,bounds)
    faces=[tuple(range(sides-1,-1,-1))]
    for j in range(rows-1):
        for k in range(sides):
            faces.append((j*sides+k,j*sides+(k+1)%sides,
                          (j+1)*sides+(k+1)%sides,(j+1)*sides+k))
    faces.append(tuple((rows-1)*sides+k for k in range(sides)))
    return _install(obj,vertices,faces,uv,pi*(bounds[1][0]-bounds[0][0])/2)


def _bvh(obj):
    return BVHTree.FromPolygons([v.co.copy() for v in obj.data.vertices],
                               [tuple(p.vertices) for p in obj.data.polygons],all_triangles=False)


def _surface_y(body, x, z, outward=.001):
    lo, hi = _bounds(body)
    hit = _bvh(body).ray_cast(Vector((x,lo[1]-.5,z)),Vector((0,1,0)),hi[1]-lo[1]+1.)[0]
    if hit is None:
        raise RuntimeError("Cannot conform sewn patch to actual filled sack: "+body.name)
    return hit.y-outward


def _patch(obj, body, bounds, level):
    nx,nz=_n(level,9,6,4),_n(level,7,5,3)
    vertices=[]
    tree=_bvh(body);blo,bhi=_bounds(body)
    for back in (False,True):
        for iz in range(nz):
            z=bounds[0][2]+(bounds[1][2]-bounds[0][2])*iz/(nz-1)
            for ix in range(nx):
                x=bounds[0][0]+(bounds[1][0]-bounds[0][0])*ix/(nx-1)
                hit=tree.ray_cast(Vector((x,blo[1]-.5,z)),Vector((0,1,0)),bhi[1]-blo[1]+1.)[0]
                if hit is None:raise RuntimeError("Patch outside filled cloth: "+obj.name)
                vertices.append((x,hit.y-(.0006 if back else .0014),z))
    count=nx*nz;faces=[]
    for iz in range(nz-1):
        for ix in range(nx-1):
            a=iz*nx+ix;b=a+1;c=a+nx+1;d=a+nx
            faces.extend([(a,b,c,d),(count+d,count+c,count+b,count+a)])
    border=list(range(nx))+[iz*nx+nx-1 for iz in range(1,nz)]+list(range((nz-1)*nx+nx-2,(nz-1)*nx-1,-1))+[iz*nx for iz in range(nz-2,0,-1)]
    for a,b in zip(border,border[1:]+border[:1]):faces.append((a,count+a,count+b,b))
    return _install(obj,vertices,faces)


def _extra(collection, template, name, material):
    obj=bpy.data.objects.get(name)
    if obj is not None:
        assert obj.get("Hub8SoftGoodsOwned"),name
        return obj
    mesh=bpy.data.meshes.new(name)
    mesh.materials.append(bpy.data.materials[material])
    obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj)
    obj.matrix_world=template.matrix_world.copy()
    obj["unity_collision"]=False;obj["prop_id"]=template["prop_id"]
    obj["Hub8SoftGoodsOwned"]=True
    return obj


def _retarget(collections, originals, level, log):
    changes=[]
    for identifier, collection in collections.items():
        specs=originals[identifier]
        objects={name:bpy.data.objects[name] for name in specs}
        if identifier=="KH_Sandbag":
            body=objects["SoftSack"]
            _pillow(body,specs[body.name]["bounds"],level,0)
            lo,hi=_bounds(body);center=Vector(tuple((lo[i]+hi[i])/2 for i in range(3)))
            tree=_bvh(body)
            for j,name in enumerate(("SackStitchedEnd","SackStitchedEnd.001")):
                x=lo[0]+.018 if j==0 else hi[0]-.018
                points=[]
                for k in range(_n(level,48,32,20)):
                    a=k*2*pi/_n(level,48,32,20)
                    seed=Vector((x,center.y+cos(a),center.z+sin(a)))
                    target=Vector((x,center.y,center.z))
                    hit=tree.ray_cast(seed,(target-seed).normalized(),2.)[0]
                    if hit is None:raise RuntimeError("Cannot locate sewn end on pillow")
                    normal=(hit-target).normalized();points.append(tuple(hit+normal*.0017))
                _tube(objects[name],points,.0015,_n(level,8,6,5),True)
            changes.extend([(body.name,"Filled pillow with compressed sewn ends"),
                            ("SackStitchedEnd","Raised textile end piping"),("SackStitchedEnd.001","Raised textile end piping")])
        elif identifier=="KH_SupplySackStack":
            bodies=sorted(n for n in objects if n.startswith("LumpySupplyBag"))
            for j,name in enumerate(bodies):
                suffix=name[len("LumpySupplyBag"):];body=objects[name]
                _pillow(body,specs[name]["bounds"],level,j+1)
                patch_name="SackSewnPatch"+suffix;seam_name="SackSeam"+suffix
                _patch(objects[patch_name],body,specs[patch_name]["bounds"],level)
                lo,hi=_bounds(body);z=(lo[2]+hi[2])/2
                points=[]
                for k in range(_n(level,28,18,12)):
                    x=lo[0]+(hi[0]-lo[0])*(.055+.89*k/(_n(level,28,18,12)-1))
                    points.append((x,_surface_y(body,x,z,.0020),z))
                _tube(objects[seam_name],points,.0018,_n(level,8,6,5))
                # Small real running stitches border the fitted fabric patch.
                patch_bounds=specs[patch_name]["bounds"]
                for edge in (0,1):
                    count=_n(level,8,5,3)
                    for k in range(count):
                        x=patch_bounds[0][0]+(patch_bounds[1][0]-patch_bounds[0][0])*(k+.5)/count
                        zz=patch_bounds[0][2]+.003 if edge==0 else patch_bounds[1][2]-.003
                        dx=min(.005,(patch_bounds[1][0]-patch_bounds[0][0])/count*.25)
                        ends=[(xx,_surface_y(body,xx,zz,.0025),zz) for xx in (x-dx,x+dx)]
                        stitch=_extra(collection,body,"Audit8 "+identifier+" patch stitch %s %s %s"%(j,edge,k),"WornWood")
                        _tube(stitch,ends,.00085,_n(level,6,5,4))
                changes.extend([(name,"Smooth heavy filled sack; distinct bulge and gathered end folds"),
                                (patch_name,"Thin fitted sewn repair patch"),(seam_name,"Narrow curved sewn seam, replacing oversized wooden-looking strip")])
        else:
            for name,obj in objects.items():
                lo,hi=specs[name]["bounds"];cx,cy=(lo[0]+hi[0])/2,(lo[1]+hi[1])/2
                height=hi[2]-lo[2];radius=(hi[0]-lo[0])/2
                sides=_n(level,64,40,24)
                if name.startswith("PreservedBottlePaperLabel"):
                    suffix=name[len("PreservedBottlePaperLabel"):]
                    blo,bhi=specs["PreservedBottle"+suffix]["bounds"]
                    r=(bhi[0]-blo[0])/2
                    _lathe(obj,(cx,cy,lo[2]),[(0,r+.0002),(0,r+.0008),(height,r+.0008),(height,r+.0002)],sides,close_profile=True)
                    changes.append((name,"Smooth thin annular paper label, physical thickness"))
                elif name.startswith("PreservedBottleCap"):
                    profile=[(0,radius*.94),(height*.10,radius),(height*.78,radius),(height*.94,radius*.97),(height,radius*.89)]
                    _lathe(obj,(cx,cy,lo[2]),profile,sides,
                           radial=lambda a,j:1+.014*cos(a*_n(level,24,16,8)) if j in (1,2) else 1)
                    changes.append((name,"Rolled screw-cap rim and fine formed grip"))
                elif name.startswith("PreservedBottle"):
                    profile=[(0,.91),(.012,.97),(.032,1),(.62,1),(.69,.99),(.75,.91),(.81,.69),(.86,.49),(.96,.47),(1,.48)]
                    _lathe(obj,(cx,cy,lo[2]),[(z*height,r*radius) for z,r in profile],sides)
                    changes.append((name,"Round bottle heel, body, formed shoulder, neck and lip"))
                elif name.startswith("PreservedTinLidRim"):
                    _lathe(obj,(cx,cy,lo[2]),[(0,radius*.91),(0,radius*.985),(height*.25,radius),(height*.75,radius),(height,radius*.96),(height,radius*.88)],sides,close_profile=True)
                    changes.append((name,"Closed annular rolled tin seam"))
                elif name.startswith("PreservedTin"):
                    profile=[(0,.955),(.012,.995),(.028,1),(.055,.977),(.925,.977),(.96,1),(.985,1),(1,.96)]
                    _lathe(obj,(cx,cy,lo[2]),[(z*height,r*radius) for z,r in profile],sides)
                    pull=_extra(collection,obj,"Audit8 "+identifier+" tin pull "+name,"Frame")
                    points=[(cx+cos(k*2*pi/_n(level,24,16,10))*.009,
                             cy+sin(k*2*pi/_n(level,24,16,10))*.017,
                             hi[2]+.0015) for k in range(_n(level,24,16,10))]
                    _tube(pull,points,.00125,_n(level,8,6,5),True)
                    changes.append((name,"Smooth food tin with rolled heel/rim and real pull tab"))
                elif name.startswith("WideGlassJarLidRim"):
                    _lathe(obj,(cx,cy,lo[2]),[(0,radius*.93),(height*.10,radius),(height*.75,radius),(height,radius*.93)],sides,
                           radial=lambda a,j:1+.010*cos(a*_n(level,24,16,8)) if j in (1,2) else 1)
                    changes.append((name,"Wide screw-lid with softened rolled perimeter"))
                elif name.startswith("WideGlassJar"):
                    profile=[(0,.88),(.018,.97),(.04,1),(.76,1),(.86,.98),(.92,.86),(1,.84)]
                    _lathe(obj,(cx,cy,lo[2]),[(z*height,r*radius) for z,r in profile],sides)
                    changes.append((name,"Round wide-mouth jar with shoulder and heel; existing opaque material retained"))
                elif name.startswith("GrainSackTiedNeckKnotTail"):
                    suffix=name[len("GrainSackTiedNeckKnotTail"):]
                    blo,bhi=specs["GrainSack"+suffix]["bounds"]
                    bx,by=(blo[0]+bhi[0])/2,(blo[1]+bhi[1])/2;h=bhi[2]-blo[2]
                    points=[(bx,by-.040,blo[2]+h*.905),(bx+.040,by-.066,blo[2]+h*.89),
                            (bx+.060,by-.082,blo[2]+h*.80),(bx+.047,by-.094,blo[2]+h*.76)]
                    _tube(obj,points,.0032,_n(level,12,8,6))
                    changes.append((name,"Real small hanging tie tail replacing angular spike"))
                elif name.startswith("GrainSackTiedNeck"):
                    suffix=name[len("GrainSackTiedNeck"):]
                    blo,bhi=specs["GrainSack"+suffix]["bounds"]
                    bx,by=(blo[0]+bhi[0])/2,(blo[1]+bhi[1])/2;h=bhi[2]-blo[2]
                    points=[]
                    for k in range(_n(level,64,40,24)):
                        a=k*4*pi/(_n(level,64,40,24)-1)
                        points.append((bx+.044*cos(a),by+.042*sin(a),blo[2]+h*.902+.007*k/(_n(level,64,40,24)-1)))
                    _tube(obj,points,.0031,_n(level,12,8,6))
                    changes.append((name,"Two turns of binding cord around gathered cloth"))
                elif name.startswith("GrainSack"):
                    _upright_sack(obj,(lo,hi),level,1 if name=="GrainSack" else 2)
                    changes.append((name,"Upright filled grain sack with rounded heel, gathered neck and ruffled cloth"))
                # Existing beveled crate, real fasteners, vegetables and folded
                # packet are deliberately retained without arbitrary deformation.
    for name,reason in changes:
        bpy.data.objects[name]["Hub8SoftGoodsReason"]=reason
    if level==0:
        log.extend(dict(object=name,correction=reason) for name,reason in changes)
    return changes


def _roundtrip(path, prop, expected_triangles):
    # This intentionally destroys the working scene only after native LOD0 has
    # been saved to the work file, then reads the real FBX with Blender's importer.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    from audit7_verify_props import geometry
    root=bpy.data.objects.get(prop["id"]+"_Root")
    objects=[o for o in bpy.context.scene.objects if o.type=="MESH"]
    assert root is not None and root.type=="EMPTY",(path,"missing FBX root")
    assert sorted(o.name for o in objects)==sorted(c["name"] for c in prop["chunks"]),(path,"chunk identities changed")
    records=[]
    for obj in objects:
        metrics=geometry(obj.data)
        assert obj.parent==root and obj.matrix_world.determinant()>0,(obj.name,"transform")
        assert all(abs(s-1)<1e-5 for s in obj.scale),(obj.name,"scale")
        assert not metrics["nonManifoldEdges"] and not metrics["degenerateFaces"],(obj.name,metrics)
        assert not metrics["invalidFaceNormals"] and not metrics["nonPositiveSolids"],(obj.name,metrics)
        assert metrics["signedVolume"]>0 and metrics["finiteUV"] and metrics["uvLayers"],(obj.name,metrics)
        assert len(obj.data.materials)==1 and obj.data.materials[0],obj.name
        records.append(dict(chunk=obj.name,**metrics))
    triangles=sum(r["triangles"] for r in records)
    assert triangles==expected_triangles,(path,triangles,expected_triangles)
    return dict(path=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                triangles=triangles,chunks=records)


def apply(root, work):
    """Save targeted native LOD0 and nine proved FBX exports; return manifest updates."""
    root,work=Path(root).resolve(),Path(work).resolve()
    directory=work/"soft_goods";directory.mkdir(parents=True,exist_ok=True)
    source=root/"Sources/KursHub/KursHubProps.blend"
    manifest_path=root/"Assets/Environment/KursHub/Props/KursHubPropsManifest.json"
    manifest=json.loads(manifest_path.read_text(encoding="utf8"))
    props={p["id"]:p for p in manifest["props"] if p["id"] in TARGETS}
    assert set(props)==set(TARGETS),"The three known authored prop records are required"
    backup=directory/"before";backup.mkdir(exist_ok=True)
    if not (backup/source.name).exists():shutil.copy2(source,backup/source.name)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    collections={identifier:bpy.data.collections[identifier] for identifier in TARGETS}
    originals={};original_matrices={};modifier_states={}
    for identifier,collection in collections.items():
        originals[identifier]={}
        for obj in collection.all_objects:
            if obj.type!="MESH":continue
            assert not obj.get("Hub8SoftGoodsRevision"),(obj.name,"already targeted; restore the before source for a clean rerun")
            assert len(obj.data.materials)==1 and obj.data.materials[0],obj.name
            originals[identifier][obj.name]=dict(bounds=_bounds(obj))
            original_matrices[obj.name]=tuple(v for row in obj.matrix_world for v in row)
            modifier_states[obj.name]=[(m.name,m.type,m.show_viewport,m.show_render,getattr(m,"segments",None)) for m in obj.modifiers]
    before_names={identifier:set(values) for identifier,values in originals.items()}
    log=[];exports=[];updates={identifier:dict(props[identifier]) for identifier in TARGETS}
    tools=Path(__file__).resolve().parent
    if str(tools) not in sys.path:sys.path.insert(0,str(tools))
    from audit8_modeling import export_prop
    temporary_native=directory/"KursHubProps_LOD0.blend"
    for level in range(3):
        _retarget(collections,originals,level,log)
        for identifier,collection in collections.items():
            for name in before_names[identifier]:
                assert bpy.data.objects.get(name) is not None,(name,"original object lost")
                assert tuple(v for row in bpy.data.objects[name].matrix_world for v in row)==original_matrices[name],(name,"original transform changed")
            allowed={(c["material"],bool(c["collider"])):c["name"] for c in props[identifier]["chunks"]}
            objects=[o for o in collection.all_objects if o.type=="MESH"]
            for obj in objects:
                key=(obj.data.materials[0].name,bool(obj.get("unity_collision",False)))
                assert key in allowed,(obj.name,"new material or collider group",key)
                obj["Hub8ExportChunk"]=allowed[key]
                for m in obj.modifiers:
                    if m.type=="BEVEL":
                        if level==1:m.segments=1
                        if level==2:m.show_viewport=False;m.show_render=False
            prop_record=dict(id=identifier,objects=objects,offset=collection.instance_offset.copy())
            record,extra=export_prop(prop_record,root,level)
            assert sorted(c["name"] for c in extra["chunks"])==sorted(c["name"] for c in props[identifier]["chunks"])
            exports.append(dict(id=identifier,level=level,record=record,extra=extra))
            if level==0:
                updates[identifier].update(triangles=record["triangles"],boundsMin=extra["boundsMin"],boundsMax=extra["boundsMax"])
            updates[identifier].setdefault("hub8SoftGoods",dict(revision=8,approach="Procedural closed profiles and sewn cloth, original objects and chunks retained"))
        if level==0:
            # Preserve existing relative image paths for the canonical source
            # location. The final copy is written only after all FBX proofs pass.
            bpy.ops.wm.save_as_mainfile(filepath=str(temporary_native),relative_remap=False)
    roundtrips=[]
    for identifier in TARGETS:
        levels=sorted((e for e in exports if e["id"]==identifier),key=lambda e:e["level"])
        counts=[e["record"]["triangles"] for e in levels]
        assert counts[0]>counts[1]>counts[2],(identifier,"unsafe/nondecreasing LOD",counts)
        updates[identifier]["lods"]=[e["record"] for e in levels]
        for exported in levels:
            roundtrips.append(_roundtrip(root/exported["record"]["model"],props[identifier],exported["record"]["triangles"]))
    incoming=source.with_name(source.name+".hub8-incoming")
    shutil.copy2(temporary_native,incoming)
    incoming.replace(source)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    result=dict(status="HUB8_SOFT_GOODS_ROUNDTRIP_OK",source=str(source),
                sourceSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),
                targets=list(TARGETS),corrections=log,propUpdates=list(updates.values()),
                roundtrips=roundtrips,
                retained="All original object names, transforms, source offsets, material/collision groups and chunk node names. Existing crate/vegetable/packet geometry retained.",
                limitation="No kettle object exists in these three collections. No unknown mesh was reinterpreted as a kettle. Actual scene visual review remains required.")
    (directory/"soft_goods_validation.json").write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf8")
    print("HUB8_SOFT_GOODS_ROUNDTRIP_OK",len(log),"corrections",len(roundtrips),"actual FBX imports",flush=True)
    return result
