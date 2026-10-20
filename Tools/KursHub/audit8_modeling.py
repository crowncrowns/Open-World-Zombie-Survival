"""Closed, metre-scale manufacturing primitives for authored hub equipment.

Input coordinates use Unity +Y up / front -Z. Each new prop remains an editable
collection; only evaluated export copies are combined by material/collision.
"""
from pathlib import Path
import bpy,bmesh,math,json,collections
from mathutils import Vector,Matrix

def xyz(p):return Vector((p[0],p[2],p[1]))
def unity(p):return [float(p[0]),float(p[2]),float(p[1])]
BOX=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]

class Builder:
 def __init__(self,level=0,start_index=55):
  self.level=level;self.start_index=start_index;self.props=[];self.labels={};self.current=None;self.serial=0
 def N(self,a,b,c):return (a,b,c)[self.level]
 def begin(self,identifier,description):
  assert self.current is None,'end the previous prop'
  assert bpy.data.collections.get(identifier) is None,identifier
  collection=bpy.data.collections.new(identifier);bpy.context.scene.collection.children.link(collection)
  index=self.start_index+len(self.props);offset=Vector((80+(index-self.start_index)%6*9,200+(index-self.start_index)//6*9,0))
  collection.instance_offset=offset
  self.current=dict(id=identifier,collection=identifier,origin=description,sourceOffset=unity(offset),objects=[],offset=offset,col=collection)
 def end(self):
  assert self.current and self.current['objects'];self.props.append(self.current);self.current=None
 def material(self,name):
  material=bpy.data.materials.get(name)
  if material is None:
   assert name.startswith('Hub8Labels'),name
   material=bpy.data.materials.new(name);material.use_nodes=True;material.diffuse_color=(1,1,1,1)
   bsdf=material.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(1,1,1,1);bsdf.inputs['Roughness'].default_value=.82
  return material
 def mesh(self,name,vertices,faces,mat,collision=True,smooth=False,uv_fixed=None):
  assert self.current
  mesh=bpy.data.meshes.new(self.current['id']+' '+name);mesh.from_pydata([xyz(v) for v in vertices],[],faces);mesh.update()
  bm=bmesh.new();bm.from_mesh(mesh)
  bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
  bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
  if bm.calc_volume(signed=True)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
  assert not any(not e.is_manifold for e in bm.edges),(name,'open surface')
  assert not any(f.calc_area()<=1e-12 for f in bm.faces),(name,'zero face')
  bm.to_mesh(mesh);bm.free();mesh.update()
  layer=mesh.uv_layers.new(name='PhysicalUV')
  for f in mesh.polygons:
   axis=max(range(3),key=lambda i:abs(f.normal[i]));projection=((1,2),(0,2),(0,1))[axis]
   for li in f.loop_indices:
    p=mesh.vertices[mesh.loops[li].vertex_index].co
    layer.data[li].uv=uv_fixed(p) if uv_fixed else (p[projection[0]]/2,p[projection[1]]/2)
   f.use_smooth=smooth
  if smooth:mesh.set_sharp_from_angle(angle=math.radians(60))
  mesh.materials.append(self.material(mat));obj=bpy.data.objects.new(self.current['id']+' '+name,mesh);self.current['col'].objects.link(obj)
  obj.location=self.current['offset'];obj['unity_collision']=bool(collision);obj['Audit8Manufactured']=True;self.current['objects'].append(obj)
  return obj
 def box(self,name,center,size,mat='Frame',bevel=.006,collision=True,yaw=0):
  x,y,z=center;sx,sy,sz=[q/2 for q in size];angle=math.radians(yaw);cs,sn=math.cos(angle),math.sin(angle)
  verts=[]
  for px,py,pz in [(-sx,-sy,-sz),(sx,-sy,-sz),(sx,sy,-sz),(-sx,sy,-sz),(-sx,-sy,sz),(sx,-sy,sz),(sx,sy,sz),(-sx,sy,sz)]:
   verts.append((x+cs*px+sn*pz,y+py,z-sn*px+cs*pz))
  obj=self.mesh(name,verts,BOX,mat,collision,smooth=bool(bevel))
  if bevel and self.level<2:
   m=obj.modifiers.new('Manufactured edge radius','BEVEL');m.width=min(bevel,min(size)*.35);m.segments=self.N(3,1,0);m.limit_method='ANGLE';m.harden_normals=True
   normal=obj.modifiers.new('Weighted face normals','WEIGHTED_NORMAL');normal.keep_sharp=True
  if mat in ('TimberRaw','WornWood'):
   # Explicit grain coordinate along the longest stock axis, before yaw.
   long_axis=max(range(3),key=lambda i:size[i]);layer=obj.data.uv_layers.active
   for loop in obj.data.loops:
    p=unity(obj.data.vertices[loop.vertex_index].co);q=[p[i]-center[i] for i in range(3)];q[0],q[2]=cs*q[0]-sn*q[2],sn*q[0]+cs*q[2]
    layer.data[loop.index].uv=(q[(long_axis+1)%3]/2,q[long_axis]/2)
   obj['Audit8WoodGrainAxis']=long_axis
  return obj
 def lathe(self,name,center,profile,mat='SteelOxide',sides=None,collision=True):
  n=sides or self.N(40,24,16);verts=[]
  for height,radius in profile:
   assert radius>0,(name,'use positive ring radii and real caps')
   for i in range(n):
    angle=2*math.pi*i/n;verts.append((center[0]+radius*math.cos(angle),center[1]+height,center[2]+radius*math.sin(angle)))
  faces=[tuple(range(n-1,-1,-1))]
  for j in range(len(profile)-1):
   for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
  faces.append(tuple((len(profile)-1)*n+i for i in range(n)))
  obj=self.mesh(name,verts,faces,mat,collision,True)
  # Cylindrical stock UV, seam at +X; no stretched planar projection.
  layer=obj.data.uv_layers.active
  for f in obj.data.polygons:
   angular=[]
   for li in f.loop_indices:
    p=unity(obj.data.vertices[obj.data.loops[li].vertex_index].co);angular.append(math.atan2(p[2]-center[2],p[0]-center[0])/(2*math.pi)%1)
   cross=max(angular)-min(angular)>.5
   for li,u in zip(f.loop_indices,angular):
    p=unity(obj.data.vertices[obj.data.loops[li].vertex_index].co);u=u+1 if cross and u<.5 else u
    layer.data[li].uv=(u*2*math.pi*max(r for h,r in profile)/2,(p[1]-center[1])/2)
  return obj
 def pipe(self,name,points,radius=.02,mat='Frame',sides=None,collision=False):
  n=sides or self.N(16,10,6);points=[Vector(p) for p in points];assert len(points)>=2
  closed=(points[0]-points[-1]).length<1e-7
  if closed:points.pop();assert len(points)>=3
  verts=[];previous=None
  for j,p in enumerate(points):
   tangent=(points[(j+1)%len(points)]-points[(j-1)%len(points)]).normalized() if closed else (points[min(j+1,len(points)-1)]-points[max(j-1,0)]).normalized()
   if previous is None:
    seed=Vector((0,1,0)) if abs(tangent.y)<.9 else Vector((1,0,0));u=tangent.cross(seed).normalized()
   else:
    u=(previous-tangent*previous.dot(tangent)).normalized()
   v=tangent.cross(u).normalized();previous=u
   for i in range(n):verts.append(tuple(p+radius*(u*math.cos(2*math.pi*i/n)+v*math.sin(2*math.pi*i/n))))
  faces=[] if closed else [tuple(range(n-1,-1,-1))]
  for j in range(len(points) if closed else len(points)-1):
   k=(j+1)%len(points)
   for i in range(n):faces.append((j*n+i,j*n+(i+1)%n,k*n+(i+1)%n,k*n+i))
  if not closed:faces.append(tuple((len(points)-1)*n+i for i in range(n)))
  return self.mesh(name,verts,faces,mat,collision,True)
 def text(self,name,text,position,size=.04,mat='Lettering',yaw=0):
  key=(self.current['id'],name,text,mat)
  if key not in self.labels:self.labels[key]=len(self.labels)
  index=self.labels[key];page=index//32;slot=index%32;family='Hub8Labels'+chr(65+page)
  lines=text.split('\n');width=max(len(line) for line in lines)*size*.55;height=size*1.4*len(lines)
  x,y,z=position;angle=math.radians(yaw);cs,sn=math.cos(angle),math.sin(angle)
  # The thin sticker uses one atlas slot; its paper backing is real geometry.
  obj=self.box(name,(x+width*.5*cs,y+height*.5,z-width*.5*sn),(width,height,.0016),family,bevel=0,collision=False,yaw=yaw)
  layer=obj.data.uv_layers.active;col,row=slot%4,slot//4
  for loop in obj.data.loops:
   p=unity(obj.data.vertices[loop.vertex_index].co);dx,dz=p[0]-x,p[2]-z;u=(cs*dx-sn*dz)/max(width,1e-5);v=(p[1]-y)/max(height,1e-5)
   layer.data[loop.index].uv=((col+.015+u*.97)/4,(7-row+.03+v*.94)/8)
  obj['Audit8PrintedText']=text;obj['Audit8LabelSlot']=index
  return obj

def merged_mesh(objects,name,offset):
 deps=bpy.context.evaluated_depsgraph_get();vertices=[];faces=[];uvs=[];normals=[];smooth=[]
 for obj in objects:
  ev=obj.evaluated_get(deps);mesh=ev.to_mesh(preserve_all_data_layers=True,depsgraph=deps);start=len(vertices)
  vertices.extend(tuple(obj.matrix_world@v.co-offset) for v in mesh.vertices);nmat=obj.matrix_world.to_3x3().inverted().transposed();layer=mesh.uv_layers.active
  for face in mesh.polygons:
   faces.append(tuple(start+i for i in face.vertices));smooth.append(face.use_smooth)
   for li in face.loop_indices:uvs.append(tuple(layer.data[li].uv));normals.append(tuple((nmat@mesh.corner_normals[li].vector).normalized()))
  ev.to_mesh_clear()
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update();uv=mesh.uv_layers.new(name='PhysicalUV')
 for target,value in zip(uv.data,uvs):target.uv=value
 for target,value in zip(mesh.polygons,smooth):target.use_smooth=value
 mesh.normals_split_custom_set(normals);return mesh

def export_prop(prop,root,level):
 groups=collections.defaultdict(list)
 for obj in prop['objects']:
  mat=obj.data.materials[0].name;collision=bool(obj['unity_collision']);name=obj.get('Hub8ExportChunk',f"{prop['id']}_{mat}_{'C' if collision else 'NC'}")
  groups[(name,mat,collision)].append(obj)
 collection=bpy.data.collections.new('EXPORT8_TEMP');bpy.context.scene.collection.children.link(collection);empty=bpy.data.objects.new(prop['id']+'_Root',None);collection.objects.link(empty)
 exports=[];chunks=[];triangles=0;points=[]
 assert len({key[0] for key in groups})==len(groups),'One export chunk cannot silently gain multiple materials'
 for (name,material,collision),items in sorted(groups.items()):
  mesh=merged_mesh(items,name,prop['offset']);mesh.materials.append(bpy.data.materials[material]);mesh.calc_loop_triangles();triangles+=len(mesh.loop_triangles);points.extend(v.co.copy() for v in mesh.vertices)
  obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj);obj.parent=empty;exports.append(obj);chunks.append(dict(name=name,material=material,collider=collision))
 bpy.ops.object.select_all(action='DESELECT');empty.select_set(True)
 for obj in exports:obj.select_set(True)
 bpy.context.view_layer.objects.active=empty
 path=Path(root)/'Assets/Environment/KursHub/Props/Models'/((prop['id']+'.fbx') if level==0 else ('LOD/'+prop['id']+f'_LOD{level}.fbx'));path.parent.mkdir(parents=True,exist_ok=True)
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'EMPTY','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
 record=dict(level=level,triangles=triangles,model=path.relative_to(root).as_posix())
 extra=dict(chunks=chunks,boundsMin=[min(unity(v)[i] for v in points) for i in range(3)],boundsMax=[max(unity(v)[i] for v in points) for i in range(3)])
 for obj in exports:
  mesh=obj.data;bpy.data.objects.remove(obj,do_unlink=True);bpy.data.meshes.remove(mesh)
 bpy.data.objects.remove(empty,do_unlink=True);bpy.data.collections.remove(collection)
 return record,extra
