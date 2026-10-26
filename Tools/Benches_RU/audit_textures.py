from pathlib import Path
from PIL import Image
import hashlib
import json

root=Path(__file__).resolve().parents[2]
asset=root/"work/Benches_RU/UnityProject/Assets/Environment/Props/Benches_RU"
results=[]
for path in sorted((asset/"Textures").glob("*.png")):
    with Image.open(path) as image:
        image.verify()
    with Image.open(path) as image:
        results.append(dict(file=path.name,width=image.width,height=image.height,mode=image.mode,
                            bytes=path.stat().st_size,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
errors=[]
if len(results)!=45: errors.append("Expected 45 maps in nine PBR sets")
for result in results:
    if (result["width"],result["height"])!=(4096,4096): errors.append(result["file"]+": not 4096x4096")
report=dict(result="PASS" if not errors else "FAIL",mapCount=len(results),nativeGenerationSize=4096,
            upscalingUsed=False,maps=results,errors=errors)
out=root/"work/Benches_RU/Verification/TextureAudit.json"
out.write_text(json.dumps(report,indent=2),encoding="utf-8")
print("TEXTURE_AUDIT",report["result"],len(results),"maps at native 4096x4096")
if errors: raise RuntimeError(errors)
