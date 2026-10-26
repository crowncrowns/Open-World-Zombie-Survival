import bpy
import json
import math
from pathlib import Path

root=Path(__file__).resolve().parents[1]
asset=root/'UnityProject'/'Assets'/'Environment'/'Windows'/'MetalGrilles'
bpy.ops.wm.open_mainfile(filepath=str(root/'Source'/'MetalGrilles_Master.blend'))
records=[]
errors=[]

def ancestor(obj,prefix):
    parent=obj.parent
    while parent:
        if parent.name.startswith(prefix):return parent
        parent=parent.parent
    return None

for collection in bpy.data.collections:
    if not collection.name.startswith('SM_Grille_'):continue
    collection.hide_viewport=collection.name!='SM_Grille_01_Classic_A'
    collection.hide_render=collection.name!='SM_Grille_01_Classic_A'
    for obj in collection.objects:
        if obj.type=='MESH':
            visible=ancestor(obj,'LOD0') is not None
            obj.hide_set(not visible);obj.hide_render=not visible
    if '_05_Hinged_' not in collection.name:continue
    for side in ('Left','Right'):
        vertices=[]
        pivot=None
        for obj in collection.objects:
            if obj.type!='MESH' or ancestor(obj,'LOD0') is None:continue
            p=ancestor(obj,side+'LeafPivot')
            if p:
                pivot=p
                vertices.extend(obj.matrix_world@v.co for v in obj.data.vertices)
        if pivot is None:errors.append(collection.name+' missing '+side);continue
        origin=pivot.matrix_world.translation
        lo,hi=(-math.radians(105),0) if side=='Left' else (0,math.radians(105))
        max_back=-1e9
        for point in vertices:
            dx,dy=point.x-origin.x,point.y-origin.y
            angles=[lo,hi]
            critical=math.atan2(dx,dy)
            for k in range(-2,3):
                theta=critical+k*math.pi
                if lo<=theta<=hi:angles.append(theta)
            max_back=max(max_back,max(dx*math.sin(t)+dy*math.cos(t)+origin.y for t in angles))
        # Main demo wall front Z=.17m, grille pivot Z=.24m. Every vertex and,
        # by convexity, every triangle remains in front of this wall plane.
        clearance=.24-max_back-.17
        if clearance<.001:errors.append(collection.name+' wall sweep clearance '+str(clearance))
        records.append({'id':collection.name,'leaf':side,'angleDegrees':105,'continuousSweep':True,
                        'wallPlaneClearanceMetres':clearance,'vertices':len(vertices)})

source_folder=asset/'Source~'
source_folder.mkdir(parents=True,exist_ok=True)
for image in bpy.data.images:
    if image.source=='FILE' and image.filepath:
        image.filepath='//../Textures/'+Path(image.filepath).name
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(source_folder/'MetalGrilles_Master.blend'),compress=True,relative_remap=False)
missing=[]
for image in bpy.data.images:
    if image.source=='FILE' and image.filepath and not Path(bpy.path.abspath(image.filepath)).is_file():missing.append(image.filepath)
errors.extend('Missing portable texture '+p for p in missing)
report={'status':'SOURCE_AND_SWEEP_PASS' if not errors else 'FAIL','assetCollections':len([c for c in bpy.data.collections if c.name.startswith('SM_Grille_')]),
        'portableImagePaths':len([i for i in bpy.data.images if i.source=='FILE']), 'hingeSweep':records,'errors':errors}
(root/'Verification'/'source_scene_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(report['status'],report)
