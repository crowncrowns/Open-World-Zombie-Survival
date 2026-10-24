from pathlib import Path

import bpy

fbx=Path(__file__).resolve().parents[2]/"work"/"Benches_RU"/"UnityProject"/"Assets"/"Environment"/"Props"/"Benches_RU"/"Models"/"Bench_RU_01_Wear01.fbx"
bpy.ops.import_scene.fbx(filepath=str(fbx),axis_forward="-Z",axis_up="Y")
obj=next(o for o in bpy.context.scene.objects if o.type=="MESH" and "LOD0" in o.name.upper())
layer=obj.data.uv_layers.get("LightmapUV")
print("OBJ",obj.name,"UV",layer.name if layer else None,"poll",bpy.ops.uv.select_overlap.poll(),flush=True)
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active=obj
if layer:
    obj.data.uv_layers.active=layer
try:
    bpy.context.scene.tool_settings.use_uv_select_sync=True
    bpy.context.tool_settings.mesh_select_mode=(False,False,True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    print("EDIT_POLL",bpy.ops.uv.select_overlap.poll(),flush=True)
    result=bpy.ops.uv.select_overlap(extend=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    selected=sum(1 for poly in obj.data.polygons if poly.select)
    by_mat={}
    for poly in obj.data.polygons:
        if poly.select:
            key=obj.data.materials[poly.material_index].name if poly.material_index<len(obj.data.materials) else "<none>"
            by_mat[key]=by_mat.get(key,0)+1
    print("OVERLAP_OPERATOR",result,"selected_faces",selected,"by_material",by_mat,flush=True)
except Exception as e:
    print("OVERLAP_OPERATOR_ERROR",repr(e),flush=True)
