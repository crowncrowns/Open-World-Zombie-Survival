import bpy,bmesh,json,sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
manifest=json.loads((ASSET/'manifest.json').read_text(encoding='utf-8'));repairs=[]
audit=json.loads((VERIFY/'ProfessionalAudit/clean_fbx_audit.json').read_text(encoding='utf-8'))
required={a['name']:{m['name'].split('.')[0] for m in a['meshes'] if m['boundaryEdges']} for a in audit['results']}
for f in manifest['fixtures']:
    col=bpy.data.collections[f['name']];changed=False
    needed=required.get(f['name'],set());imported=[]
    if needed:
        before_objects=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(ASSET/'Models'/f['category']/(f['name']+'.fbx')),use_anim=False)
        imported=[o for o in bpy.data.objects if o not in before_objects]
    for o in col.objects:
        if not o.name.startswith('LOD'):continue
        prefix=o.name.split('.')[0]
        if prefix in needed:
            src=next(q for q in imported if q.type=='MESH' and q.name.startswith(prefix))
            me=src.data.copy();me.transform(o.matrix_world.inverted()@src.matrix_world)
            for k,m in enumerate(me.materials):
                key=m.name.split('.')[0];canonical=bpy.data.materials.get(key)
                if canonical:me.materials[k]=canonical
            old=o.data;label=old.name;o.data=me
            if old.users==0:bpy.data.meshes.remove(old)
            o.data.name=label
        elif not o.name.startswith('LOD2_'):continue
        bm=bmesh.new();bm.from_mesh(o.data);edges=[e for e in bm.edges if e.is_boundary]
        if not edges:bm.free();continue
        before=len(edges)
        # Decimation can leave isolated planar triangles from subpixel screws/contacts.
        # They have no thickness and cannot be closed by duplicating the same face.
        remaining=set(bm.faces);discard=[]
        while remaining:
            seed=remaining.pop();group={seed};pending=[seed]
            while pending:
                face=pending.pop()
                for edge in face.edges:
                    for linked in edge.link_faces:
                        if linked in remaining:remaining.remove(linked);group.add(linked);pending.append(linked)
            vertices={v for face in group for v in face.verts};span=Vector([max(v.co[k] for v in vertices)-min(v.co[k] for v in vertices) for k in range(3)])
            if len(group)<=16 and span.length<.03 and all(abs(face.normal.dot(seed.normal))>.9999 for face in group) and any(e.is_boundary for face in group for e in face.edges):discard.extend(group)
        if discard:bmesh.ops.delete(bm,geom=discard,context='FACES')
        loose=[v for v in bm.verts if not v.link_faces]
        if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
        edges=[e for e in bm.edges if e.is_boundary]
        result=bmesh.ops.holes_fill(bm,edges=edges,sides=16)
        for face in list(result.get('faces',[])):
            if face.is_valid and face.calc_area()<1e-12:
                vertices=list(face.verts);center=sum((v.co for v in vertices),Vector())/len(vertices)
                bmesh.ops.pointmerge(bm,verts=vertices,merge_co=center)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));after=sum(e.is_boundary for e in bm.edges)
        if after:raise RuntimeError((f['name'],o.name,'Unclosed boundary',after))
        bm.to_mesh(o.data);bm.free();o.data.update();o.hide_set(False)
        uv=o.data.uv_layers[0]
        for face in o.data.polygons:
            axis=max(range(3),key=lambda k:abs(face.normal[k]));axes=[k for k in range(3) if k!=axis]
            for li in face.loop_indices:
                p=o.data.vertices[o.data.loops[li].vertex_index].co;uv.data[li].uv=(p[axes[0]]*2+.5,p[axes[1]]*2+.5)
        bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;o.data.uv_layers.active_index=1
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.25,island_margin=.007,area_weight=.35,correct_aspect=True,scale_to_bounds=False);bpy.ops.object.mode_set(mode='OBJECT');o.data.uv_layers.active_index=0
        tri=o.modifiers.new('Final closed LOD2 triangulation','TRIANGULATE');tri.keep_custom_normals=True;bpy.ops.object.modifier_apply(modifier=tri.name)
        repairs.append(dict(asset=f['name'],mesh=o.name,boundaryBefore=before,boundaryAfter=0));changed=True
    if changed:
        objects=[o for o in col.objects if o.name.startswith('LOD')];bpy.ops.object.select_all(action='DESELECT')
        for o in objects:o.hide_set(False);o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(ASSET/'Models'/f['category']/(f['name']+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
        for o in objects:o.hide_set(True)
        f['triangles']=[sum(len(o.data.polygons) for o in objects if o.name.startswith('LOD'+str(lod)+'_')) for lod in range(3)]
    for o in imported:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
(ASSET/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(VERIFY/'ProfessionalAudit'/'lod2_closure.json').write_text(json.dumps(dict(passed=True,repairs=repairs),indent=2),encoding='utf-8')
print('LOD2_CLOSURE_PASS',len(repairs),flush=True)
