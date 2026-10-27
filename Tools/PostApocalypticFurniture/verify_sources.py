import bpy,sys,json,math
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import ASSET,VERIFY
results=[]
for path in sorted((ASSET/'SourceBlender/Source~').glob('*.blend')):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    roots=[o for o in bpy.data.objects if o.parent is None and o.name.startswith('F')]
    meshes=[o.data for o in bpy.data.objects if o.type=='MESH'];issues=[]
    if len(roots)!=10:issues.append('Expected ten independently editable roots')
    if any(len(m.uv_layers)!=2 for m in meshes):issues.append('Expected UV0 and separate lightmap UV')
    for image in bpy.data.images:
        if image.source=='FILE' and not Path(bpy.path.abspath(image.filepath)).is_file():issues.append('Missing relative texture '+image.name)
    duplicate_faces=0
    for mesh in meshes:
        faces=[tuple(sorted(p.vertices)) for p in mesh.polygons];duplicate_faces+=len(faces)-len(set(faces))
    if duplicate_faces:issues.append('Duplicate source triangles '+str(duplicate_faces))
    results.append(dict(file=path.name,editableRoots=len(roots),meshes=len(meshes),duplicateFaces=duplicate_faces,issues=issues))
    print('SOURCE_BLEND',path.name,len(roots),issues,flush=True)
report=dict(passed=len(results)==20 and all(not r['issues'] for r in results),files=results)
(VERIFY/'blender_source_audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('EDITABLE_BLEND_'+('PASS' if report['passed'] else 'FAIL'),len(results),flush=True)
