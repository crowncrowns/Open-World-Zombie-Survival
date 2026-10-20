"""Weld font cap/side seams and retriangulate zero-area cap triangles in place."""
import bmesh

def repair_authored_text(obj):
    assert obj.type=='MESH' and obj.get('is_text'),obj.name
    bm=bmesh.new();bm.from_mesh(obj.data)
    before=dict(vertices=len(bm.verts),nonManifoldEdges=sum(not e.is_manifold for e in bm.edges),
        degenerateFaces=sum(f.calc_area()<=1e-12 for f in bm.faces))
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
    # Font conversion sometimes generates a collinear triangle inside a cap.
    # Join it to an adjacent cap triangle and select the other quad diagonal.
    # No point is moved or deleted, so the outline and holes stay unchanged.
    iterations=0
    for _ in range(80):
        bad=[f for f in bm.faces if f.calc_area()<=1e-12 or f.normal.length<.99]
        if not bad:break
        face=bad[0]
        candidates=[]
        for edge in face.edges:
            linked=[f for f in edge.link_faces if f!=face]
            if len(linked)!=1:continue
            neighbour=linked[0]
            if neighbour.calc_area()<=1e-12 or neighbour.normal.length<.99:continue
            point=neighbour.verts[0].co
            if max(abs((v.co-point).dot(neighbour.normal)) for v in face.verts)>1e-5:continue
            candidates.append((edge.calc_length(),edge))
        assert candidates,(obj.name,'Cannot find planar cap neighbour',len(bad))
        edge=max(candidates,key=lambda item:item[0])[1]
        result=bmesh.ops.dissolve_edges(bm,edges=[edge],use_verts=False,use_face_split=False)
        faces=[f for f in result['region'] if isinstance(f,bmesh.types.BMFace) and f.is_valid]
        bmesh.ops.triangulate(bm,faces=faces,quad_method='BEAUTY',ngon_method='BEAUTY')
        bm.normal_update();iterations+=1
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
    after=dict(vertices=len(bm.verts),faces=len(bm.faces),nonManifoldEdges=sum(not e.is_manifold for e in bm.edges),
        degenerateFaces=sum(f.calc_area()<=1e-12 for f in bm.faces),invalidNormals=sum(f.normal.length<.99 for f in bm.faces),
        volume=bm.calc_volume(signed=True))
    assert after['nonManifoldEdges']==0 and after['degenerateFaces']==0 and after['invalidNormals']==0 and after['volume']>0,(obj.name,after)
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    obj['Audit7TextRepair']='Weld cap and side vertices <=0.01 mm; retriangulate zero-area cap faces without moving the outline'
    return dict(name=obj.name,before=before,after=after,retriangulatedCaps=iterations)

if __name__=='__main__':
    import bpy,pathlib,json
    W=pathlib.Path(__file__).resolve().parent.parent
    bpy.ops.wm.open_mainfile(filepath=str(W/'authoring/Sources/KursHub/KursHub.blend'))
    records=[]
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH' and obj.get('is_text'):
            record=repair_authored_text(obj);records.append(record);print('TEXT_FIXED',record,flush=True)
    (W/'text_repair_probe.json').write_text(json.dumps(dict(status='TEXT_REPAIR_ALL_OK',objects=records),ensure_ascii=False,indent=2),encoding='utf8')
    print('TEXT_REPAIR_ALL_OK',len(records),flush=True)
