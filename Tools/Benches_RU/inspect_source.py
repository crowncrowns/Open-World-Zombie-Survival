from pathlib import Path

import bpy


ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/"work"/"Benches_RU"/"UnityProject"/"Assets"/"Environment"/"Props"/"Benches_RU"/"Source"/"Benches_RU_Master.blend"

for kind in ("Bench_RU_01_Wear01","Bench_RU_01_Wear02","Bench_RU_02_Wear02","Bench_RU_03_Wear02","Bench_RU_04_Wear01"):
    col=bpy.data.collections.get(kind)
    if not col:
        print("MISSING",kind)
        continue
    lod_col=col.children.get("Export_LODs")
    lod0=next((o for o in lod_col.objects if o.name=="LOD0"),None) if lod_col else None
    if lod0:
        z=[(lod0.matrix_world@v.co).z for v in lod0.data.vertices]
        print("LOD_BOUNDS",kind,"min",min(z),"max",max(z),"origin",tuple(lod0.location),"tri",sum(len(p.vertices)-2 for p in lod0.data.polygons), flush=True)
    negative=[]
    parts=col.children.get("Authoring_Parts")
    if parts:
        counts=[]
        for o in parts.objects:
            if o.type!="MESH": continue
            tri=sum(len(p.vertices)-2 for p in o.data.polygons)
            counts.append((tri,o.name))
            z=min((o.matrix_world@v.co).z for v in o.data.vertices) if len(o.data.vertices) else 0
            if z < -0.001:
                negative.append((round(z,5),o.name,tri))
        for target in ("Seat board 1","Cast leaf boss","Cast iron side cheek"):
            o=next((obj for obj in parts.objects if obj.name==target),None)
            if o:
                print("DEBUG_TRANSFORM",kind,target,"loc",tuple(o.location),"matrix",tuple(tuple(round(c,3) for c in row) for row in o.matrix_world),"localMin",min(v.co.z for v in o.data.vertices),flush=True)
        print("TOP_PARTS",kind,sorted(counts,reverse=True)[:12],"NEGATIVE",sorted(negative)[:12],flush=True)
