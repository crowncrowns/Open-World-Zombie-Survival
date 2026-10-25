import bpy
import json
import math
import mathutils
from pathlib import Path

ROOT_DIR=Path(__file__).resolve().parents[1]
MODEL_DIR=ROOT_DIR/"UnityProject"/"Assets"/"Environment"/"Windows"/"MetalGrilles"/"Models"
OUT=ROOT_DIR/"Verification"/"blender_reimport.json"
NAMES=["Classic","Forged","Reinforced","Survivor","Hinged"]
errors=[]
records=[]

for typ,name in enumerate(NAMES,1):
    for state in ("A","B","C"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        fbx=MODEL_DIR/f"SM_Grille_{typ:02d}_{name}_{state}.fbx"
        bpy.ops.import_scene.fbx(filepath=str(fbx),use_anim=False,ignore_leaf_bones=True)
        meshes=[o for o in bpy.context.scene.objects if o.type=="MESH"]
        empties=[o for o in bpy.context.scene.objects if o.type=="EMPTY"]
        root=next((o for o in empties if o.name.startswith(f"SM_Grille_{typ:02d}_{name}_{state}")),None)
        if root is None: errors.append(f"{fbx.name}: root object not reimported")
        if not meshes: errors.append(f"{fbx.name}: no meshes")
        bounds_min=mathutils.Vector((float("inf"),)*3)
        bounds_max=mathutils.Vector((float("-inf"),)*3)
        lod_tris=[0,0,0,0]
        mesh_count_by_lod=[0,0,0,0]
        uv_count=0
        finite=True
        for obj in meshes:
            for v in obj.data.vertices:
                co=obj.matrix_world@v.co
                if not all(math.isfinite(c) for c in co): finite=False
            for corner in obj.bound_box:
                p=obj.matrix_world@mathutils.Vector(corner)
                bounds_min.x=min(bounds_min.x,p.x); bounds_min.y=min(bounds_min.y,p.y); bounds_min.z=min(bounds_min.z,p.z)
                bounds_max.x=max(bounds_max.x,p.x); bounds_max.y=max(bounds_max.y,p.y); bounds_max.z=max(bounds_max.z,p.z)
            if obj.data.uv_layers:
                uv_count += len(obj.data.uv_layers[0].data)
            lod_index=None
            parent=obj.parent
            while parent:
                for i in range(4):
                    if parent.name==f"LOD{i}": lod_index=i
                parent=parent.parent
            if lod_index is None:
                for i in range(4):
                    if obj.name.startswith(f"LOD{i}"): lod_index=i
            if lod_index is not None:
                lod_tris[lod_index] += sum(max(1,len(poly.vertices)-2) for poly in obj.data.polygons)
                mesh_count_by_lod[lod_index] += 1
        size=bounds_max-bounds_min
        if not finite: errors.append(f"{fbx.name}: non-finite vertex")
        if uv_count==0: errors.append(f"{fbx.name}: missing UV")
        if size.x>1.11 or size.y>.16 or not(.90<=size.z<=.98):
            errors.append(f"{fbx.name}: source-space bounds unexpected {tuple(round(x,4) for x in size)}")
        # Blender clean-import check retains the actual LOD hierarchy and all four levels.
        if any(c==0 for c in mesh_count_by_lod): errors.append(f"{fbx.name}: missing LOD mesh groups {mesh_count_by_lod}")
        if not (lod_tris[0]>lod_tris[1]>lod_tris[2]>lod_tris[3]):
            errors.append(f"{fbx.name}: LOD triangle count not descending {lod_tris}")
        if typ==5:
            names={o.name for o in empties}
            for i in range(4):
                for side in ("Left","Right"):
                    p=next((o for o in empties if o.name==f"{side}LeafPivot_LOD{i}"),None)
                    if p is None or p.parent is None or p.parent.name!=f"LOD{i}":
                        errors.append(f"{fbx.name}: missing {side} leaf pivot at LOD{i}")
        records.append({"id":fbx.stem,"meshCount":len(meshes),"emptyCount":len(empties),"uvLoopCount":uv_count,
                        "boundsMin":list(bounds_min),"boundsMax":list(bounds_max),"boundsSize":list(size),
                        "lodTriangles":lod_tris,"meshesPerLod":mesh_count_by_lod,"finiteVertices":finite})

report={"status":"BLENDER_REIMPORT_PASS" if not errors else "FAIL","blenderVersion":bpy.app.version_string,
        "fbxCount":len(records),"errors":errors,"items":records}
OUT.parent.mkdir(parents=True,exist_ok=True)
OUT.write_text(json.dumps(report,indent=2),encoding="utf-8")
print(f"{report['status']} fbx={len(records)} errors={len(errors)} report={OUT}")
