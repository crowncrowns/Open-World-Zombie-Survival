import json,hashlib,shutil,zipfile,tarfile,tempfile,os
from pathlib import Path
from catalog import *
target=ROOT/'Assets/PostApocalypse/LightingPack'
assert (VERIFY/'unity_play_checks.json').exists()
assert json.loads((VERIFY/'unity_play_checks.json').read_text())['passed']
assert json.loads((VERIFY/'blender_reimport.json').read_text())['count']==100
assert json.loads((VERIFY/'unity_import.json').read_text())['fixtures']==90
assert (ASSET/'Demo/LightingPack_Showcase.unity').exists()
# Never replace pre-existing independent files in a package claimed by another task.
state=VERIFY/'installed_hashes.json'
prior=json.loads(state.read_text()) if state.exists() else {}
if target.exists():
    for p in target.rglob('*'):
        if not p.is_file():continue
        rel=p.relative_to(target).as_posix()
        if rel not in prior:raise RuntimeError('Untracked existing destination: '+rel)
        if hashlib.sha256(p.read_bytes()).hexdigest()!=prior[rel]:raise RuntimeError('Destination changed since install: '+rel)
updating=target.exists()
if not updating and target.with_suffix('.meta').exists():raise RuntimeError('Existing destination root meta without owned folder')
install=target if updating else Path(tempfile.mkdtemp(prefix='install_ready_',dir=ROOT/'work/LightingPack'))/'LightingPack'
install.mkdir(parents=True,exist_ok=True)
manifest={}
for p in ASSET.rglob('*'):
    if not p.is_file() or p.suffix in ['.blend1','.blend2']:continue
    rel=p.relative_to(ASSET);dest=install/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
    h=hashlib.sha256(p.read_bytes()).hexdigest();assert hashlib.sha256(dest.read_bytes()).hexdigest()==h
    manifest[rel.as_posix()]=h
# Copy the root folder GUID too. Parent folder GUIDs remain owned by the main project.
if ASSET.with_suffix('.meta').exists():shutil.copy2(ASSET.with_suffix('.meta'),target.with_suffix('.meta'))
if not updating:os.replace(install,target)
for rel,h in manifest.items():assert hashlib.sha256((target/rel).read_bytes()).hexdigest()==h
state.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
out=ROOT/'outputs';out.mkdir(exist_ok=True)
with zipfile.ZipFile(out/'LightingPack_Unity.zip','w',zipfile.ZIP_DEFLATED,compresslevel=5) as z:
    for p in target.rglob('*'):
        if p.is_file():z.write(p,p.relative_to(ROOT))
    z.write(target.with_suffix('.meta'),target.with_suffix('.meta').relative_to(ROOT))
    z.write(ROOT/'Docs/LightingPack.md','Docs/LightingPack.md')
    for p in VERIFY.rglob('*'):
        if p.is_file() and p.suffix not in ['.log']:z.write(p,p.relative_to(ROOT))
    for p in (ROOT/'Tools/LightingPack').rglob('*'):
        if p.is_file() and '__pycache__' not in str(p):z.write(p,p.relative_to(ROOT))
with zipfile.ZipFile(out/'LightingPack_Unity.zip') as z:assert z.testzip() is None
with tarfile.open(out/'LightingPack_URP.unitypackage','r:gz') as t:
    paths=[t.extractfile(m).read().decode() for m in t.getmembers() if m.name.endswith('/pathname')]
    assert len([p for p in paths if p.endswith('.prefab')])==100
    assert len([p for p in paths if p.endswith('.fbx')])==100
result=dict(passed=True,filesInstalled=len(manifest),fbx=100,fixtures=90,decor=10,zipBytes=(out/'LightingPack_Unity.zip').stat().st_size,unityPackageBytes=(out/'LightingPack_URP.unitypackage').stat().st_size)
(VERIFY/'delivery.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(result)
