import pathlib,runpy,json,shutil
W=pathlib.Path(__file__).resolve().parent
PROJECT=W.parents[2]
module=runpy.run_path(str(PROJECT/'Tools/KursHub/audit7_soft_shading.py'),run_name='soft_probe')
manifest=json.loads((W/'authoring/Assets/Environment/KursHub/Props/KursHubPropsManifest.json').read_text(encoding='utf8'))
prop=next(p for p in manifest['props'] if p['id']=='KH_RailDrape')
path=W/'soft_shading/fixture/KH_RailDrape.fbx';path.parent.mkdir(parents=True,exist_ok=True)
shutil.copy2(W/'authoring'/prop['model'],path)
result=module['patch_fbx'](path,{c['name']:c['material'] for c in prop['chunks']},path.with_suffix('.before.fbx'))
(W/'soft_shading/probe.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print('SOFT_PROBE_OK',len(result['changedChunks']),result['actualImportChunks'],flush=True)
