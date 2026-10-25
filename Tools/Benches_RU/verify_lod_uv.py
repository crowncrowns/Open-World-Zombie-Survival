from pathlib import Path
import sys
import json
import bpy

root=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from verify_fbx import uv_overlap_audit
models=root/"work/Benches_RU/UnityProject/Assets/Environment/Props/Benches_RU/Models"
results=[]
errors=[]
for path in sorted(models.glob("Bench_RU_*.fbx")):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path),axis_forward="-Z",axis_up="Y")
    for lod in (1,2):
        obj=next(o for o in bpy.context.scene.objects if o.type=="MESH" and f"LOD{lod}" in o.name.upper())
        audit=uv_overlap_audit(obj.data,"LightmapUV")
        results.append(dict(file=path.name,lod=lod,audit=audit))
        if not audit or audit["meaningfulOverlapPairsAt1e6"]: errors.append(f"{path.name} LOD{lod}: UV2 overlap")
    print("LOD_UV_CHECKED",path.name,flush=True)
out=root/"work/Benches_RU/Verification/LodUvAudit.json"
out.write_text(json.dumps(dict(result="PASS" if not errors else "FAIL",results=results,errors=errors),indent=2),encoding="utf-8")
print("LOD_UV_"+("PASS" if not errors else "FAIL"),flush=True)
if errors: raise RuntimeError(errors)
