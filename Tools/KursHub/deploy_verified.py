"""Deploy verified authored assets and append only the two new scene roots.

Never replace the scene wholesale after Unity has normalized unrelated content.
The baseline check protects concurrent editing of the main checkout.
"""
from pathlib import Path
import hashlib
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / 'work/KursHub'
TEST = WORK / 'UnityProject'
SCENE = Path('Assets/CharacterTesting/CharacterTest.unity')

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def guid(path):
    match = re.search(r'^guid: ([a-f0-9]+)', path.read_text(encoding='utf-8'), re.M)
    assert match, str(path)
    return match[1]

def parts(text):
    result = re.split(r'(?=^--- !u!)', text, flags=re.M)
    data = {}
    for block in result:
        match = re.match(r'^--- !u!\d+ &(-?\d+)', block)
        if match:
            assert match[1] not in data, 'Duplicate scene ID ' + match[1]
            data[match[1]] = block
    return result, data

report = (WORK / 'play_validation.txt').read_text(encoding='utf-8')
assert 'KURS_PLAY_OK:' in report and '\nFAIL ' not in report, 'A complete passing Play run is required'
assert json.loads((WORK / 'geometry.json').read_text())['sourceSolidsValidated']
assert digest(ROOT / SCENE) == digest(WORK / 'backup/CharacterTest.unity'), 'Main scene changed during authoring; merge deliberately'
for name in ('Models/KursHubShell.fbx', 'KursHubManifest.json'):
    assert digest(ROOT / 'Assets/Environment/KursHub' / name) == digest(TEST / 'Assets/Environment/KursHub' / name), 'Tested geometry is stale'

original = (ROOT / SCENE).read_bytes().decode('utf-8')
verified = (TEST / SCENE).read_bytes().decode('utf-8')
assert not re.search(r'^(<<<<<<<|=======|>>>>>>>)', original, re.M), 'Resolve scene conflicts before deployment'
base_parts, base = parts(original)
test_parts, test = parts(verified)
roots_id = '9223372036854775807'
refs = lambda block: re.findall(r'\{fileID: (-?\d+)\}', block)
new_roots = [ref for ref in refs(test[roots_id]) if ref not in refs(base[roots_id])]
assert len(new_roots) == 2, f'Expected hub and access path, got {new_roots}'
pending = list(new_roots)
added = set()
while pending:
    ref = pending.pop()
    if ref == '0' or ref in base or ref in added:
        continue
    assert ref in test, 'Missing local reference ' + ref
    added.add(ref)
    pending.extend(refs(test[ref]))

# This is the sole existing object changed: open the lab boundary for the path.
boundary_id = '483497979'
assert 'm_LocalPosition: {x: 0, y: 1.5, z: -32}' in base[boundary_id]
assert 'm_LocalPosition: {x: -4, y: 1.5, z: -32}' in test[boundary_id]
assert 'm_LocalScale: {x: 57, y: 3, z: 1}' in test[boundary_id]
newline = '\r\n' if '\r\n' in original else '\n'
roots = base[roots_id] + ''.join('  - {fileID: ' + ref + '}' + newline for ref in new_roots)
combined = ''
for block in base_parts:
    match = re.match(r'^--- !u!\d+ &(-?\d+)', block)
    if match and match[1] == roots_id:
        continue
    combined += test[boundary_id] if match and match[1] == boundary_id else block
for block in test_parts:
    match = re.match(r'^--- !u!\d+ &(-?\d+)', block)
    if match and match[1] in added:
        combined += block
combined += roots
_, final_blocks = parts(combined)
assert all(ref in final_blocks or ref == '0' for ref in refs(roots))

asset_folder = Path('Assets/Environment/KursHub')
for source in (TEST / asset_folder).rglob('*'):
    if not source.is_file():
        continue
    target = ROOT / source.relative_to(TEST)
    if source.suffix == '.meta' and target.exists():
        assert guid(source) == guid(target), 'GUID change: ' + str(target)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)

(ROOT / SCENE).write_bytes(combined.encode('utf-8'))
deployment = dict(scene_sha256=digest(ROOT / SCENE), new_roots=new_roots,
                  new_scene_blocks=len(added), changed_existing_blocks=[boundary_id],
                  fbx_sha256=digest(ROOT / asset_folder / 'Models/KursHubShell.fbx'),
                  blend_sha256=digest(ROOT / 'Sources/KursHub/KursHub.blend'))
(WORK / 'deployment.json').write_text(json.dumps(deployment, indent=2), encoding='utf-8')
print('KURS_DEPLOY_OK:', len(added), 'new blocks, one boundary transform; original scene content preserved')
