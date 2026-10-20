"""Exercise actual failed-FBX repair in a separate three-file fixture."""
import pathlib,json,shutil,importlib.util,hashlib
OUT=pathlib.Path(__file__).resolve().parent
PROJECT=OUT.parents[3]
def helper(name):
    path=PROJECT/'Tools/KursHub'/name
    spec=importlib.util.spec_from_file_location(path.stem,path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    return module
verifier=helper('audit7_verify_props.py');repair=helper('audit7_repair_lods.py')
manifest=json.loads((OUT/'output/Assets/Environment/KursHub/Props/KursHubPropsManifest.json').read_text(encoding='utf8'))
prop=next(p for p in manifest['props'] if p['id']=='KH_WindowBoards')
fix=next(f for f in json.loads((OUT/'lod_export_repairs.json').read_text(encoding='utf8'))['fixes'] if f['id']==prop['id'])
fixture=OUT/'guarded_workflow_fixture'
for level in prop['lods']:
    destination=fixture/'output'/level['model'];destination.parent.mkdir(parents=True,exist_ok=True)
    source=OUT/'output'/level['model'] if level['level']!=2 else OUT/'rejected_fbx'/pathlib.Path(level['model']).name
    shutil.copy2(source,destination)
    if level['level']==2:level['triangles']=fix['oldTriangles']
path=fixture/'output/Assets/Environment/KursHub/Props/KursHubPropsManifest.json'
path.write_text(json.dumps(dict(props=[prop]),ensure_ascii=False,indent=2),encoding='utf8')
before=verifier.run(fixture,raise_on_failure=False)
assert len(before['errors'])==1 and before['errors'][0]['level']==2,before['errors']
repair.run(fixture)
after=verifier.run(fixture,raise_on_failure=True)
assert not after['errors'] and after['exports']==3
result=dict(status='GUARDED_WORKFLOW_FIXTURE_OK',failedBefore=len(before['errors']),failedAfter=len(after['errors']),verifiedExports=after['exports'],prototype=prop['id'],onlyTemporaryFixtureWritten=True,
    toolSHA256={n:hashlib.sha256((PROJECT/'Tools/KursHub'/n).read_bytes()).hexdigest() for n in ('audit7_verify_props.py','audit7_repair_lods.py','audit7_geometry.py')})
(OUT/'guarded_workflow_test.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print(result['status'],flush=True)
