"""Recover the coherent verified hub library without rewriting user scenes."""
from pathlib import Path
import hashlib, json, os, re, shutil, datetime

R = Path(__file__).resolve().parents[2]
B = R / 'work/KursHub/audit8/baseline'
D = Path('Assets/Environment/KursHub')
SC = Path('Assets/CharacterTesting/CharacterTest.unity')
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
read = lambda p: p.read_text(encoding='utf-8-sig')
ids = lambda p: set(re.findall(r'^--- !u!\d+ &(-?\d+)', read(p), re.M))
guid = lambda p: re.search(r'^guid: ([a-f0-9]+)', read(p), re.M)[1]

def run():
    expected = json.loads(read(B / 'hashes.json'))
    sources = [B / p for p in expected if p.startswith(('Assets/Environment/KursHub/', 'Sources/KursHub/'))]
    assert sources and all(sha(p) == expected[p.relative_to(B).as_posix()] for p in sources), 'Backup integrity failure'
    before_scene = sha(R / SC)
    before_ids = ids(R / D / 'KursHub.prefab')
    assert before_ids <= ids(B / D / 'KursHub.prefab'), 'Current authored prefab IDs would be lost'
    refs = set(re.findall(r'\{fileID: (-?\d+), guid: ff5cdff5a433ef342ad7bf24aeafd8b8, type: 3\}', read(R / SC))) - {'100100000'}
    assert refs <= ids(B / D / 'KursHub.prefab'), 'Current scene references require a native Unity check'
    changed = [(s, R / s.relative_to(B)) for s in sources if not (R / s.relative_to(B)).exists() or sha(s) != sha(R / s.relative_to(B))]
    assert changed, 'The verified hub is already installed'
    for s, t in changed:
        if t.suffix == '.meta' and t.exists():
            assert guid(s) == guid(t), 'GUID conflict: ' + str(t)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%d-%H%M%S')
    recovery = R / 'work/KursHub/restoration' / stamp
    recovery.mkdir(parents=True, exist_ok=False)
    shutil.copy2(R / SC, recovery / 'CharacterTest.before.unity')
    entries = []
    for s, t in changed:
        rel = t.relative_to(R)
        if t.exists():
            q = recovery / 'before' / rel
            q.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(t, q)
        entries.append(dict(path=rel.as_posix(), before=sha(t) if t.exists() else None, restored=sha(s)))
    (recovery / 'changes.json').write_text(json.dumps(entries, indent=2), encoding='utf8')
    # Dependencies first, building prefab last: the editor never sees a new root
    # pointing at models that have not yet been installed.
    for s, t in sorted(changed, key=lambda pair: (pair[1].name == 'KursHub.prefab', pair[1].as_posix())):
        if t.exists():
            assert sha(t) == next(e['before'] for e in entries if e['path'] == t.relative_to(R).as_posix()), 'Concurrent asset edit'
        t.parent.mkdir(parents=True, exist_ok=True)
        temp = t.with_name(t.name + '.kurs-recovery-tmp')
        shutil.copy2(s, temp)
        os.replace(temp, t)
    assert before_scene == sha(R / SC), 'Scene changed concurrently; never overwrite it'
    assert all(sha(s) == sha(t) for s, t in changed)
    report = dict(status='VERIFIED_REVISION7_FILES_RESTORED', backup=str(recovery), restoredFiles=len(changed), sceneRewritten=False, sceneSha256=before_scene, previousPrefabIdsPreserved=len(before_ids), currentSceneTargetsPreserved=len(refs), prefabSha256=sha(R / D / 'KursHub.prefab'), files=entries)
    out = R / 'Verification/KursHub/Restoration20261021'
    out.mkdir(parents=True, exist_ok=True)
    (out / 'restoration.json').write_text(json.dumps(report, indent=2), encoding='utf8')
    print(json.dumps({k: v for k, v in report.items() if k != 'files'}, indent=2))

if __name__ == '__main__':
    run()
