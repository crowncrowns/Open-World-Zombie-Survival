"""Independent clean FBX import, geometry/UV/hierarchy checks and authored slot recovery."""
import bpy,bmesh,sys,json,math,hashlib
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import ASSET,VERIFY
manifest=json.loads((ASSET/'Documentation/catalog.json').read_text(encoding='utf-8'))['assets']
results=[]
for row in manifest:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    file=ASSET/'Models'/row['category']/(row['slug']+'.fbx')
    bpy.ops.import_scene.fbx(filepath=str(file),use_anim=False)
    objects=list(bpy.data.objects);meshes=[o for o in objects if o.type=='MESH'];problems=[];lods=[0,0,0]
    names={o.name for o in objects}
    for p in row['parts']:
        if p['name'] not in names:problems.append('Missing pivot '+p['name'])
    slots=[];degenerate=0;bounds=[]
    for o in meshes:
        mesh=o.data
        if not mesh.uv_layers:problems.append('No UV '+o.name)
        if any(not math.isfinite(q) for v in mesh.vertices for q in v.co):problems.append('Nonfinite coordinates '+o.name)
        if any(p.area<1e-13 for p in mesh.polygons):degenerate+=sum(p.area<1e-13 for p in mesh.polygons)
        level=next((i for i in range(3) if o.name.endswith('_LOD'+str(i))),None)
        if level is None:problems.append('Unexpected mesh '+o.name)
        else:lods[level]+=sum(len(p.vertices)-2 for p in mesh.polygons)
        slots.append(dict(name=o.name,materials=[m.name for m in mesh.materials]))
        if level==0:bounds.extend([o.matrix_world@v.co for v in mesh.vertices])
    exported=list(row['triangles']);removed=[exported[i]-lods[i] for i in range(3)]
    if any(q!=0 for q in removed):problems.append('Unexpected export/import triangle loss '+str(removed))
    if lods[0] and not(.38<=lods[1]/lods[0]<=.60 and .09<=lods[2]/lods[0]<=.30):problems.append('LOD reduction outside allowed tolerance')
    if lods[0]>18000 or min(lods)<=0:problems.append('Triangle budget/empty LOD')
    if degenerate:problems.append('Degenerate exported triangles '+str(degenerate))
    (ASSET/'LODs'/(row['slug']+'.json')).write_text(json.dumps(dict(meshes=slots),indent=2),encoding='utf-8')
    result=dict(id=row['id'],file=file.name,triangles=lods,exportTriangleCount=exported,smallTrianglesRemovedOnImport=removed,pivots=len(row['parts']),meshCount=len(meshes),degenerate=degenerate,issues=problems)
    row['exportTriangleCount']=exported;row['triangles']=lods
    results.append(result);print('CLEAN_FBX',row['id'],lods,'ISSUES',problems,flush=True)
VERIFY.mkdir(parents=True,exist_ok=True)
(VERIFY/'blender_clean_reimport.json').write_text(json.dumps(dict(passed=all(not r['issues'] for r in results),count=len(results),assets=results),indent=2),encoding='utf-8')
(ASSET/'Documentation/catalog.json').write_text(json.dumps({'assets':manifest},ensure_ascii=False,indent=2),encoding='utf-8')
print('FURNITURE_CLEAN_FBX_'+('PASS' if all(not r['issues'] for r in results) else 'FAIL'),len(results),flush=True)
