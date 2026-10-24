"""Re-import every generated FBX into a clean Blender scene and record evidence."""
from pathlib import Path
import bpy, bmesh, hashlib, json, math
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'work/PostApocalypseBalconies/UnityProject/Assets/Environment/PostApocalypse/Balconies'
OUT=ROOT/'Verification/PostApocalypseBalconies'
report={'passed':True,'blender':'5.2.2 LTS','sourceBlend':{},'models':[],'errors':[],'stateGeometry':{}}

blend=ASSET/'Models/Source~/Balconies_Master.blend'
bpy.ops.wm.open_mainfile(filepath=str(blend))
source_models=[]
for parent in bpy.context.scene.collection.children:
    if parent.name in ('Balconies','Props','Accessories'):
        source_models.extend(list(parent.children))
expected=59
source_images=[image for image in bpy.data.images if image.source=='FILE' and image.filepath]
missing_source_images=[]
for image in source_images:
    if not Path(bpy.path.abspath(image.filepath)).is_file():missing_source_images.append(image.filepath)
source_entry={'path':str(blend.relative_to(ROOT)).replace('\\','/'),'collections':len(source_models),'expectedCollections':expected,'lodMeshes':0,'linkedTextures':len(source_images),'missingLinkedTextures':missing_source_images,'passed':True}
for collection in source_models:
    children=list(collection.objects)
    names={o.name.rsplit('_',1)[-1] for o in children}
    source_entry['lodMeshes']+=sum(1 for o in children if '_LOD' in o.name)
    if len(children)!=5 or not any(o.name.endswith('_LOD0') for o in children):
        source_entry['passed']=False;report['errors'].append(f'Source collection {collection.name} has {len(children)} objects.')
if len(source_models)!=expected:
    source_entry['passed']=False;report['errors'].append(f'Source has {len(source_models)} collections, expected {expected}.')
if missing_source_images:
    source_entry['passed']=False;report['errors'].append(f'Source has {len(missing_source_images)} missing relative texture links.')
report['sourceBlend']=source_entry

paths=sorted((ASSET/'Models/FBX').rglob('*.fbx'))
for path in paths:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system='METRIC'
    try:
        bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False,automatic_bone_orientation=False)
        objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
        by_name={o.name.split('.')[0]:o for o in objects}
        expected_names=['LOD0','LOD1','LOD2','LOD3','COLLISION']
        missing=[n for n in expected_names if n not in by_name]
        item={'name':path.stem,'path':str(path.relative_to(ASSET)).replace('\\','/'),'objects':len(objects),'lodTriangles':[],'uvLayers':{},'normalsValid':True,'bounds':None,'vertexHash':'','passed':True}
        if missing: item['passed']=False;report['errors'].append(f'{path.name}: missing mesh nodes {missing}')
        digest=hashlib.sha256()
        all_lo=[];all_hi=[]
        for lod in range(4):
            ob=by_name.get(f'LOD{lod}')
            if ob is None:continue
            mesh=ob.data;mesh.calc_loop_triangles()
            item['lodTriangles'].append(len(mesh.loop_triangles))
            item['uvLayers'][f'LOD{lod}']=len(mesh.uv_layers)
            if not mesh.uv_layers:item['passed']=False;report['errors'].append(f'{path.name}: LOD{lod} has no UV0')
            if len(mesh.polygons)==0:item['passed']=False;report['errors'].append(f'{path.name}: LOD{lod} is empty')
            for poly in mesh.polygons:
                if not all(math.isfinite(v) for v in poly.normal) or abs(poly.normal.length-1.0)>.02:
                    item['normalsValid']=False;break
            if not item['normalsValid']:
                item['passed']=False;report['errors'].append(f'{path.name}: invalid polygon normals in LOD{lod}')
            if lod==0:
                for v in mesh.vertices:
                    p=ob.matrix_world@v.co
                    # Blender source is Z-up; convert its re-imported coordinates
                    # back to the authored Unity X/Y/Z convention for bounds/hash.
                    p=Vector((p.x,p.z,p.y))
                    q=(round(p.x,5),round(p.y,5),round(p.z,5))
                    digest.update(('%0.5f,%0.5f,%0.5f;'%q).encode('ascii'))
                    all_lo.append(p);all_hi.append(p)
        col=by_name.get('COLLISION')
        if col is None or not col.data.polygons:
            item['passed']=False;report['errors'].append(f'{path.name}: missing/empty COLLISION mesh')
        else:
            if not col.data.uv_layers:item['passed']=False;report['errors'].append(f'{path.name}: collider has no UV0')
        if len(item['lodTriangles'])==4 and any(item['lodTriangles'][i]<item['lodTriangles'][i+1] for i in range(3)):
            item['passed']=False;report['errors'].append(f'{path.name}: triangle count rises at a lower LOD')
        if all_lo:
            lo=[min(p[i] for p in all_lo) for i in range(3)];hi=[max(p[i] for p in all_hi) for i in range(3)]
            item['bounds']={'min':[round(v,5) for v in lo],'max':[round(v,5) for v in hi]}
            item['vertexHash']=digest.hexdigest()
            if '/Balconies/' in item['path']:
                if lo[0]<-1.351 or hi[0]>1.351:item['passed']=False;report['errors'].append(f'{path.name}: x bounds cross the 2.7 m envelope {lo[0]:.4f}..{hi[0]:.4f}')
                if lo[1]<-.025 or hi[1]>2.24:item['passed']=False;report['errors'].append(f'{path.name}: y bounds outside floor/door envelope {lo[1]:.4f}..{hi[1]:.4f}')
                depth=1.5 if '04_ModernLoggia' in path.name else 1.4 if '05_Wooden' in path.name else 1.2
                if lo[2]<-.02 or hi[2]>depth+.03:item['passed']=False;report['errors'].append(f'{path.name}: z bounds outside 0..{depth:.2f} {lo[2]:.4f}..{hi[2]:.4f}')
        else:
            item['passed']=False;report['errors'].append(f'{path.name}: no LOD0 vertices')
        if len(item['lodTriangles'])!=4:item['passed']=False
        report['models'].append(item)
        if not item['passed']:report['passed']=False
        print('REIMPORT',path.name,item['lodTriangles'],item['bounds'],flush=True)
    except Exception as exc:
        report['passed']=False;report['errors'].append(f'{path.name}: re-import exception: {exc}')
        print('REIMPORT_FAIL',path.name,repr(exc),flush=True)

for parent in (ASSET/'Models/FBX'/'Balconies').iterdir():
    if not parent.is_dir():continue
    records=[r for r in report['models'] if r['path'].startswith('Models/FBX/Balconies/'+parent.name+'/')]
    unique=len(set(r['vertexHash'] for r in records if r['vertexHash']))
    report['stateGeometry'][parent.name]={'variants':len(records),'uniqueLOD0VertexHashes':unique,'passed':len(records)==4 and unique==4}
    if len(records)!=4 or unique!=4:
        report['passed']=False;report['errors'].append(f'{parent.name}: expected four geometry-distinct states, got {unique} unique.')

report['modelCount']=len(report['models']);report['expectedModels']=59
if len(report['models'])!=59:report['passed']=False;report['errors'].append(f'Expected 59 FBX files, found {len(report["models"])}')
(OUT/'blender_reimport.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('BLENDER_REIMPORT_'+('PASS' if report['passed'] else 'FAIL'),report['modelCount'],'models',len(report['errors']),'errors',flush=True)
