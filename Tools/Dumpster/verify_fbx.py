import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'work/Dumpster/UnityProject/Assets/Environment/Props/Dumpster'
report={'exporter':'Blender 5.2.2 FBX exporter','files':[],'passed':True}
for path in sorted(ASSET.rglob('*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=True)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    item={'file':str(path.relative_to(ASSET)),'meshCount':len(meshes),'uvMeshes':0,'triangles':0,'boundaryEdges':0,'nonManifoldEdges':0,'bounds':None}
    lo=Vector((1e9,1e9,1e9));hi=-lo
    for ob in meshes:
        me=ob.data;me.calc_loop_triangles();item['triangles']+=len(me.loop_triangles)
        item['uvMeshes']+=int(len(me.uv_layers)>0)
        if not len(me.uv_layers):raise RuntimeError('No UV '+ob.name)
        if any(not all(math.isfinite(c) for c in v.co) for v in me.vertices):raise RuntimeError('Non-finite coordinates')
        for v in me.vertices:
            p=ob.matrix_world@v.co
            for k in range(3):lo[k]=min(lo[k],p[k]);hi[k]=max(hi[k],p[k])
        bm=bmesh.new();bm.from_mesh(me)
        if not ob.name.startswith('Sticker'):
            item['boundaryEdges']+=sum(1 for e in bm.edges if e.is_boundary);item['nonManifoldEdges']+=sum(1 for e in bm.edges if not e.is_manifold)
        bm.free()
    item['bounds']=list(hi-lo)
    if 'Lid_' in path.name:
        p=bpy.data.objects.get('Lid_Pivot')
        if not p or not p.animation_data or not p.animation_data.action:raise RuntimeError('Missing animated hinge')
        action=p.animation_data.action;start,end=action.frame_range
        vals=[]
        for frame in [start,(start+end)*.5,end]:
            bpy.context.scene.frame_set(int(frame));vals.append(list(p.rotation_euler))
        item['frames']=[start,end];item['hingeRotationsRadians']=vals
        if sum(abs(vals[0][k]-vals[-1][k]) for k in range(3))<1:raise RuntimeError('No meaningful FBX hinge animation')
    if item['nonManifoldEdges']!=0:raise RuntimeError('Non-manifold FBX '+path.name+' '+str(item))
    report['files'].append(item)
    print('FBX_REIMPORT_PASS',path.name,flush=True)
(ROOT/'Verification/Dumpster/blender_reimport.json').write_text(json.dumps(report,indent=2))
