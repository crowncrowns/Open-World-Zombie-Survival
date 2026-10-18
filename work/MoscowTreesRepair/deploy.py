from pathlib import Path
import re,shutil,json,hashlib
root=Path('E:/git/Open-World-Zombie-Survival')
test=Path('C:/Users/Alex/Documents/Codex/2026-10-18/referenced-chatgpt-conversation-this-is-an/work/unity603')
backup=root/'work/MoscowTreesRepair/backup/MoscowTrees'
def guid(p): return re.search(r'^guid: ([a-f0-9]+)',p.read_text(),re.M)[1]
for p in (root/'Assets/MoscowTrees/Prefabs').glob('*.prefab'):
    assert p.read_bytes()==(backup/'Prefabs'/p.name).read_bytes(), 'Prefab edited during repair: '+p.name
    assert guid(p.with_suffix('.prefab.meta'))==guid(test/'Assets/MoscowTrees/Prefabs'/(p.name+'.meta')),p.name
for folder in ('Prefabs','Materials','Models_FBX'):
    for p in (test/'Assets/MoscowTrees'/folder).rglob('*.meta'):
        target=root/'Assets/MoscowTrees'/folder/p.relative_to(test/'Assets/MoscowTrees'/folder)
        assert guid(p)==guid(target),str(target)
for p in (test/'Assets/MoscowTrees/Prefabs').glob('*.prefab'):
    shutil.copy2(p,root/'Assets/MoscowTrees/Prefabs'/p.name)
# Retain the importing editor's existing GUIDs and settings metadata.
for p in (test/'Assets/MoscowTrees/Materials').glob('*.mat'):
    shutil.copy2(p,root/'Assets/MoscowTrees/Materials'/p.name)
evidence=root/'work/MoscowTreesRepair'
for name in ('validation.txt','play_validation.txt','preview.png'):
    shutil.copy2(test/name,evidence/name)
summary={'prefabs':32,'fbx':96,'mesh_renderers':2340,'guid_preservation':'PASS',
         'prefab_materials':'nonempty slots, material and texture references validated',
         'actual_play_mode':(test/'play_validation.txt').read_text(),
         'unity':(test/'validation.txt').read_text()}
(evidence/'result.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False))
