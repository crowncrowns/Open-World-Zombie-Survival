import bpy,json
from pathlib import Path
R=Path(__file__).resolve().parents[2];A=R/'work/ModularRoofKit/UnityProject/Assets/Environment/ModularRoofKit'
bpy.ops.wm.open_mainfile(filepath=str(A/'Source~/ModularRoofKit_Master.blend'))
catalog=json.loads((A/'catalog.json').read_text());meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(meshes)==len(catalog['items'])*3
for item in catalog['items']:
    for lod in range(3):
        ob=bpy.data.objects.get(item['name']+f'_LOD{lod}');assert ob and ob.data.uv_layers and ob.data.polygons
for image in bpy.data.images:
    if image.source=='FILE':assert Path(bpy.path.abspath(image.filepath)).is_file(),image.filepath
for name in ['M_RoofProps_Brick','M_RoofProps_Organic']:assert bpy.data.materials.get(name)
result={'passed':True,'source':'Source~/ModularRoofKit_Master.blend','blender':bpy.app.version_string,'editableMeshes':len(meshes),'modules':len(catalog['items']),'materialCount':len([m for m in bpy.data.materials if m.users]),'allReferencedImagesResolve':True}
(R/'Verification/ModularRoofKit/source_validation.json').write_text(json.dumps(result,indent=2));print(result)
