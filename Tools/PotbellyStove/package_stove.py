from pathlib import Path
import shutil,hashlib,json,zipfile
ROOT=Path(__file__).resolve().parents[2]
source=ROOT/'work/PotbellyStove/UnityProject/Assets/Environment/Props/PotbellyStove'
target=ROOT/'Assets/Environment/Props/PotbellyStove'
if not (source/'Prefabs/PF_PotbellyStove_Burning.prefab').exists():raise RuntimeError('Native prefab not built')
if not (ROOT/'Verification/PotbellyStove/Runtime.json').exists():raise RuntimeError('Runtime proof missing')
if not json.loads((ROOT/'Verification/PotbellyStove/Runtime.json').read_text())['passed']:raise RuntimeError('Runtime failed')
shutil.copytree(source,target,dirs_exist_ok=True)
shutil.copy2(source.with_suffix('.meta'),target.with_suffix('.meta'))
files={str(p.relative_to(target)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in target.rglob('*') if p.is_file()}
assert all(hashlib.sha256((source/p).read_bytes()).hexdigest()==h for p,h in files.items())
(ROOT/'Verification/PotbellyStove/InstalledHashes.json').write_text(json.dumps(files,indent=2))
archive=ROOT/'outputs/PotbellyStove_Unity.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=4) as z:
    for folder in [target,ROOT/'Sources/PotbellyStove',ROOT/'Verification/PotbellyStove',ROOT/'Tools/PotbellyStove']:
        for p in folder.rglob('*'):
            if p.is_file():z.write(p,str(p.relative_to(ROOT)))
    z.write(target.with_suffix('.meta'),str(target.with_suffix('.meta').relative_to(ROOT)))
    z.write(ROOT/'Docs/PotbellyStove.md','Docs/PotbellyStove.md')
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
proof={'archive':str(archive),'sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'bytes':archive.stat().st_size,'installedFiles':len(files),'byteIdenticalToValidatedAssets':True,'zipCRC':True}
(ROOT/'Verification/PotbellyStove/Delivery.json').write_text(json.dumps(proof,indent=2));print(json.dumps(proof,indent=2))
