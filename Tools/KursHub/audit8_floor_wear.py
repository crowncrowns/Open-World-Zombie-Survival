"""Location-authored tracked dirt; a reusable thin coating, no collision."""
from pathlib import Path
import json,sys,math,hashlib
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit8';A=W/'authoring';H=A/'Assets/Environment/KursHub';F='Hub8FloorWear';ID='KH_FloorWearPatch'

def write(path,obj):path.write_text(json.dumps(obj,ensure_ascii=False,indent=2),encoding='utf8')

def textures():
 import numpy as np
 from PIL import Image,ImageDraw,ImageFilter
 n=2048;rng=np.random.default_rng(81691)
 yy,xx=np.mgrid[0:n,0:n].astype(np.float32)
 base=np.exp(-((xx-1010-yy*.06)/310)**2-((yy-1024)/770)**2)*.045
 # Original boot-sole stencil with separate heel, tread and scuffed edges.
 sole=Image.new('L',(180,400),0);d=ImageDraw.Draw(sole)
 d.ellipse((20,4,160,180),fill=180);d.polygon([(23,90),(155,90),(146,260),(120,291),(58,291),(30,238)],fill=175)
 d.rounded_rectangle((29,294,148,388),radius=16,fill=190)
 for row in range(22,260,27):d.line((28,row,82,row+13),fill=20,width=8);d.line((99,row+13,152,row),fill=20,width=8)
 for row in (311,337,363):d.line((36,row,140,row),fill=30,width=9)
 for _ in range(190):
  x,y=rng.integers(0,180),rng.integers(0,400);d.ellipse((int(x),int(y),int(x+4),int(y+3)),fill=0)
 mask=Image.new('L',(n,n),0)
 for i in range(6):
  stamp=sole.resize((116,270),Image.Resampling.LANCZOS).rotate((-1 if i%2 else 1)*8+int(rng.integers(-6,7)),Image.Resampling.BICUBIC,expand=True)
  x=910+(150 if i%2 else -150)+int(rng.integers(-25,26));y=150+i*286
  mask.paste(stamp,(x,y),stamp)
 blur=mask.filter(ImageFilter.GaussianBlur(2.0));alpha=base+np.asarray(blur,dtype=np.float32)/255*.46
 # Scattered crumbs of tracked soil around the print, fading at the boundary.
 alpha+=rng.random((n,n)).astype(np.float32)*base*1.8
 fade=np.clip(np.minimum.reduce([xx,yy,n-1-xx,n-1-yy])/150,0,1)
 alpha=np.clip(alpha*fade,0,.46)
 rgba=np.zeros((n,n,4),np.uint8)
 grain=rng.normal(0,1.4,(n,n))
 for c,value in enumerate((53,45,32)):rgba[:,:,c]=np.uint8(np.clip(value+grain,0,255))
 rgba[:,:,3]=np.uint8(alpha*255)
 t=H/'Textures';Image.fromarray(rgba).save(t/(F+'_BaseColor.png'))
 for suffix,value in [('Metallic',0),('Roughness',238),('Occlusion',255)]:Image.new('L',(n,n),value).save(t/(F+'_'+suffix+'.png'))
 Image.new('RGB',(n,n),(128,128,255)).save(t/(F+'_Normal.png'))
 Image.new('RGBA',(n,n),(0,0,0,17)).save(t/(F+'_MetallicSmoothness.png'))
 Image.new('I;16',(n,n),32768).save(t/(F+'_Height.png'))
 p=t/'Audit7PBRManifest.json';obj=json.loads(p.read_text(encoding='utf8'))
 obj['families']=[f for f in obj['families'] if f['name']!=F]+[dict(name=F,resolution=n,periodMetres=2,periodYMetres=2,tiling=1,normalStrength=1,heightMinMetres=0,heightMaxMetres=0,alpha=True,uniqueUV=True,heightUsage='Thin visual coating; no displacement or collider')]
 write(p,obj)
 for relative in ('KursHubManifest.json','Props/KursHubPropsManifest.json'):
  p=H/relative;o=json.loads(p.read_text(encoding='utf8'))
  if not any(v['name']==F for v in o['palette']):o['palette'].append(dict(name=F,rgba=[1,1,1,1]))
  write(p,o)
 print('AUDIT8_FLOOR_WEAR_TEXTURES_OK')

def model():
 import bpy
 from mathutils import Vector
 sys.path.insert(0,str(Path(__file__).parent))
 from audit8_modeling import Builder,export_prop
 from audit7_verify_props import geometry
 source=A/'Sources/KursHub/KursHubProps.blend';p=H/'Props/KursHubPropsManifest.json';manifest=json.loads(p.read_text(encoding='utf8'))
 assert not any(r['id']==ID for r in manifest['props']),'Do not regenerate an existing edited prop'
 bpy.ops.wm.open_mainfile(filepath=str(source));assert bpy.data.collections.get(ID) is None
 mat=bpy.data.materials.new(F);mat.use_nodes=True
 api=Builder(0,len(manifest['props']));api.begin(ID,'2 x 2 m thin tracked-soil coating; unique UV; no collider. Single 12-triangle mesh, no redundant LOD copies.')
 prop=api.current;prop['offset']=Vector((80,227,0));prop['sourceOffset']=[80,0,227];prop['col'].instance_offset=prop['offset']
 api.box('Tracked soil film',(0,0,0),(2,.0008,2),F,bevel=0,collision=False)
 obj=prop['objects'][0];layer=obj.data.uv_layers.active
 for loop in obj.data.loops:
  v=obj.data.vertices[loop.vertex_index].co;layer.data[loop.index].uv=((v.x+1)/2,(v.y+1)/2)
 assert not geometry(obj.data)['nonManifoldEdges'];api.end()
 record,extra=export_prop(prop,A,0)
 manifest['props'].append(dict(id=ID,model=record['model'],collection=ID,sourceOffset=prop['sourceOffset'],origin=prop['origin'],triangles=record['triangles'],lods=[record],**extra))
 obj['KursHubWearPurpose']='Location-authored boot marks; visual only, no physics'
 tmp=source.with_name('KursHubProps.floorwear.tmp.blend');bpy.context.preferences.filepaths.save_version=0
 bpy.ops.wm.save_as_mainfile(filepath=str(tmp),relative_remap=False);tmp.replace(source);write(p,manifest)
 # Real clean-scene FBX readback before the new library entry can be installed.
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(A/record['model']))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==1
 m=geometry(meshes[0].data);assert m['triangles']==12 and m['nonManifoldEdges']==0 and m['nonPositiveSolids']==0 and m['finiteUV']
 write(W/'floor_wear_model_proof.json',dict(status='FLOOR_WEAR_REAL_FBX_PASS',sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),geometry=m,prop=manifest['props'][-1],lodPolicy='One minimal 12-triangle mesh; no redundant tessellation solely to manufacture LODs'))
 print('AUDIT8_FLOOR_WEAR_MODEL_OK')

def placements():
 p=H/'KursHubManifest.json';m=json.loads(p.read_text(encoding='utf8'));rows=m['settlementInstances'];known={r['id'] for r in rows};added=[]
 def put(name,x,y,z,yaw):
  identifier='Hub8 wear '+name
  row=dict(id=identifier,prop=ID,position=[round(x,5),y+.007,round(z,5)],rotation=[0,yaw,0],scale=[1,1,1])
  if identifier in known:
   assert next(r for r in rows if r['id']==identifier)==row
  else:rows.append(row);known.add(identifier)
  added.append(identifier)
 for side in (-1,1):
  for z in (-62,-54,-46,-38,-30):put('Entrance %s %s'%(side,z),side*1.4,0,z,180 if side<0 else 0)
 for i,z in enumerate((-26,-18,-10,-2,6)):put('Market lane %02d'%i,3.4,0,z,4 if i%2 else 176)
 for floor in (0,5.5):
  for side in (-1,1):
   zs=(-22,-10,2) if floor==0 or side<0 else (-16,0)
   for z in zs:put('Room approach %s %s %s'%(floor,side,z),side*28.5,floor,z,side*90)
 count=0
 for row in list(rows):
  if not row['id'].startswith('Hub8 ') or row['prop'] not in ('KH_KitchenRange','KH_WashStation','KH_RepairBench','KH_WaterTreatment'):continue
  if row['position'][1]>6:continue
  yaw=math.radians(row['rotation'][1]);x,y,z=row['position']
  put('Workplace %02d'%count,x-math.sin(yaw)*1.15,y,z-math.cos(yaw)*1.15,row['rotation'][1]);count+=1
 m['hub8FloorWear']=dict(placements=added,purpose='Tracked soil near entry lanes, room thresholds and actual work fronts; ceramics remain visible. Rugs cover the thin coating naturally.',noCollision=True)
 write(p,m);write(W/'floor_wear_layout8.json',m['hub8FloorWear']);print('AUDIT8_FLOOR_WEAR_PLACEMENTS',len(added))

if __name__=='__main__':
 args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:]
 {'textures':textures,'model':model,'placements':placements}[args[0]]()
