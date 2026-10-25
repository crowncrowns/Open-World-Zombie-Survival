from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np
import json

root=Path(__file__).resolve().parents[2]
folder=root/"work/Benches_RU/Verification"
names=[f"Bench_RU_{kind:02d}_Wear{wear:02d}" for kind in range(1,6) for wear in range(1,4)]
paths=[folder/(prefix+name+".png") for prefix in ("","LOD1_","LOD2_") for name in names]
paths += [folder/f"Detail_Bench_RU_{kind:02d}_Wear02.png" for kind in range(1,6)]
paths += [folder/"Demo_Benches_RU.png"]
errors=[]; results=[]
for path in paths:
    if not path.exists(): errors.append("Missing capture: "+path.name); continue
    with Image.open(path) as im:
        pixels=np.asarray(im.convert("RGB"))
        magenta=int(np.count_nonzero((pixels[:,:,0]>220)&(pixels[:,:,1]<50)&(pixels[:,:,2]>220)))
        variation=float(np.std(pixels.astype(np.float32)))
        results.append(dict(file=path.name,width=im.width,height=im.height,magentaPixels=magenta,colorStd=round(variation,3)))
        if magenta>50: errors.append(path.name+": error-shader magenta pixels")
        if variation<5: errors.append(path.name+": empty or uniform capture")
report=dict(result="PASS" if not errors else "FAIL",captureCount=len(results),captures=results,errors=errors)
(folder/"ImageAudit.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
font=ImageFont.truetype("C:/Windows/Fonts/arial.ttf",20)
sheet=Image.new("RGB",(1500,5*355+45),(32,36,39)); draw=ImageDraw.Draw(sheet)
for wear in range(1,4): draw.text(((wear-1)*500+190,8),f"Wear{wear:02d}",font=font,fill=(235,235,230))
for kind in range(1,6):
    for wear in range(1,4):
        with Image.open(folder/f"Bench_RU_{kind:02d}_Wear{wear:02d}.png") as im:
            tile=im.convert("RGB").resize((500,333),Image.Resampling.LANCZOS)
            point=((wear-1)*500,(kind-1)*355+45); sheet.paste(tile,point)
            draw.text((point[0]+12,point[1]+310),f"Bench {kind:02d}",font=font,fill=(240,240,240))
sheet.save(folder/"Contact_Benches_RU.png")
compare=Image.new("RGB",(1500,3*355+45),(32,36,39)); draw=ImageDraw.Draw(compare)
for lod in range(3): draw.text((lod*500+210,8),f"LOD{lod}",font=font,fill=(235,235,230))
for row,name in enumerate(("Bench_RU_01_Wear01","Bench_RU_03_Wear03","Bench_RU_04_Wear02")):
    for lod in range(3):
        with Image.open(folder/(("" if lod==0 else f"LOD{lod}_")+name+".png")) as im:
            compare.paste(im.convert("RGB").resize((500,333),Image.Resampling.LANCZOS),(lod*500,row*355+45))
compare.save(folder/"LOD_Comparison.png")
print("IMAGE_AUDIT",report["result"],len(results),"Unity captures")
if errors: raise RuntimeError(errors)
