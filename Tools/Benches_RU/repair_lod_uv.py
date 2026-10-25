from pathlib import Path
import sys,json,math,os
import bpy

root=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from build_benches import export_fbx
from verify_fbx import inspect_fbx,uv_overlap_audit
asset=root/"work/Benches_RU/UnityProject/Assets/Environment/Props/Benches_RU"
verify=root/"work/Benches_RU/Verification"
master=asset/"Source~/Benches_RU_Master.blend"
names=("Bench_RU_01_Wear01","Bench_RU_05_Wear03")
bpy.ops.wm.open_mainfile(filepath=str(master))
for name in names:
    collection=bpy.data.collections[name]
    lod_collection=next(c for c in collection.children if c.name.startswith("Export_LODs"))
    lods=sorted((o for o in lod_collection.objects if o.type=="MESH" and o.name.startswith("LOD")),key=lambda o:o.name)
    obj=next(o for o in lods if o.name.startswith("LOD2"))
    bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True); bpy.context.view_layer.objects.active=obj
    obj.data.uv_layers.active_index=obj.data.uv_layers.find("LightmapUV")
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(10),island_margin=0.004,area_weight=0.1,rotate_method="AXIS_ALIGNED")
    bpy.ops.uv.pack_islands(rotate=True,margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.data.uv_layers.active_index=obj.data.uv_layers.find("UVMap"); obj.data.uv_layers.active.active_render=True
    export_fbx(lods,asset/"Models"/(name+".fbx"))
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(master))
base=json.loads((verify/"BlenderReimport.json").read_text(encoding="utf-8"))
lod_report=json.loads((verify/"LodUvAudit.json").read_text(encoding="utf-8"))
for name in names:
    new=inspect_fbx(asset/"Models"/(name+".fbx"))
    base["results"]=[new if item["file"]==new["file"] else item for item in base["results"]]
    for lod in (1,2):
        obj=next(o for o in bpy.context.scene.objects if o.type=="MESH" and f"LOD{lod}" in o.name.upper())
        audit=uv_overlap_audit(obj.data,"LightmapUV")
        updated=dict(file=name+".fbx",lod=lod,audit=audit)
        lod_report["results"]=[updated if item["file"]==updated["file"] and item["lod"]==lod else item for item in lod_report["results"]]
        print("REPAIRED_REIMPORT",name,lod,audit,flush=True)
base["errors"]=[]
for item in base["results"]:
    if not item["allFinite"] or not item["groundContact"] or not item["dimensionsPlausible"] or item["missingMaterialSlots"] or item["lod0Uv2OverlapAudit"]["meaningfulOverlapPairsAt1e6"]:
        base["errors"].append(item["file"])
base["result"]="PASS" if not base["errors"] else "FAIL"
lod_report["errors"]=[f"{item['file']} LOD{item['lod']}" for item in lod_report["results"] if not item["audit"] or item["audit"]["meaningfulOverlapPairsAt1e6"]]
lod_report["result"]="PASS" if not lod_report["errors"] else "FAIL"
(verify/"BlenderReimport.json").write_text(json.dumps(base,indent=2),encoding="utf-8")
(verify/"LodUvAudit.json").write_text(json.dumps(lod_report,indent=2),encoding="utf-8")
if base["errors"] or lod_report["errors"]: raise RuntimeError((base["errors"],lod_report["errors"]))
print("ALL_LOD_UV_PASS_AFTER_REIMPORT",flush=True)
os._exit(0)
