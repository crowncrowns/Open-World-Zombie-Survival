from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools" / "Benches_RU"))
import verify_fbx

verify_fbx.UV_BINS = 1024

asset_dir = ROOT / "work" / "Benches_RU" / "UnityProject" / "Assets" / "Environment" / "Props" / "Benches_RU" / "Models"
name = "Bench_RU_01_Wear01.fbx"
result = verify_fbx.inspect_fbx(asset_dir / name)
print("AUDIT_ONE", name, "uv2", result["lod0Uv2OverlapAudit"], "uv_layers", result["uvLayers"][0], flush=True)
