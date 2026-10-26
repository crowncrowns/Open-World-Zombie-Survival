import bpy,sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ASSET/'Models/Interior/Light_Interior_Pendant_Damaged.fbx'))
for o in bpy.context.scene.objects:
    if o.type=='MESH':print(o.name,'verts',len(o.data.vertices),'polys',len(o.data.polygons),'mats',[m.name if m else None for m in o.data.materials],'used',sorted({p.material_index for p in o.data.polygons}),flush=True)
