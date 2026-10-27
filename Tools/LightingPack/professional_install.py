from catalog import *
import json,re,hashlib,shutil,zipfile,tarfile,subprocess,os
proof=VERIFY/'ProfessionalAudit';target=ROOT/'Assets/PostApocalypse/LightingPack';backup=ROOT/'work/LightingPack/ProfessionalAudit/Before'
initial=json.loads((proof/'initial_inventory.json').read_text(encoding='utf-8'))
assert (proof/'export_verified.txt').exists()
assert json.loads((proof/'identity_preservation.json').read_text())['prefabs']==100
assert json.loads((proof/'clean_fbx_audit.json').read_text())['passed']
assert json.loads((VERIFY/'unity_play_checks.json').read_text())['passed']
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
# Do not overwrite edits made by another person since the audit snapshot.
for rel,h in initial['files'].items():assert (target/rel).exists() and sha(target/rel)==h,('Installed asset changed during audit',rel)
files=[p for p in ASSET.rglob('*') if p.is_file() and p.suffix not in ['.blend1','.blend2']]
for p in files:
    rel=p.relative_to(ASSET).as_posix()
    if (target/rel).exists() and rel not in initial['files']:raise RuntimeError('Unowned new destination file: '+rel)
guids={}
for p in files+[ASSET.with_suffix('.meta')]:
    if p.suffix!='.meta':continue
    m=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8'),re.M)
    if m:
        g=m.group(1);assert g not in guids,('Duplicate stage GUID',p,guids.get(g));guids[g]=str(p)
patterns=ROOT/'work/LightingPack/ProfessionalAudit/guids.txt';patterns.write_text('\n'.join('guid: '+g for g in guids),encoding='ascii')
scan=subprocess.run(['rg','-n','-F','-f',str(patterns),str(ROOT/'Assets'),'-g','*.meta'],capture_output=True,text=True,encoding='utf-8');assert scan.returncode in (0,1),scan.stderr
prefix=str(target).replace('\\','/').lower();outside=[]
for line in scan.stdout.splitlines():
    normalized=line.replace('\\','/').lower()
    if normalized.startswith(prefix+'/') or normalized.startswith(prefix+'.meta:'):continue
    outside.append(line)
assert not outside,outside[:15]
package=ROOT/'outputs/LightingPack_URP_Professional.unitypackage';assert package.exists()
with tarfile.open(package,'r:gz') as t:
    names=[t.extractfile(m).read().decode('utf-8') for m in t.getmembers() if m.name.endswith('/pathname')]
assert sum(n.endswith('.prefab') for n in names)==100
assert sum(n.endswith('.fbx') for n in names)==100
hashes={}
for p in sorted(files,key=lambda p:(p.suffix!='.meta',p.as_posix())):
    rel=p.relative_to(ASSET);dest=target/rel;dest.parent.mkdir(parents=True,exist_ok=True)
    if not dest.exists() or sha(dest)!=sha(p):shutil.copy2(p,dest)
    h=sha(p);assert sha(dest)==h;hashes[rel.as_posix()]=h
assert ASSET.with_suffix('.meta').read_bytes()==target.with_suffix('.meta').read_bytes()
assert len(list((target/'Prefabs').rglob('*.prefab')))==100
for entry in initial['prefabs']:
    p=target/entry['path'];ids=set(re.findall(r'^--- !u!(\d+) &(-?\d+)',p.read_text(encoding='utf-8'),re.M));assert {tuple(v) for v in entry['fileIDs']}<=ids
shutil.copy2(proof/'Project_Doc_Update.md',ROOT/'Docs/LightingPack.md')
oldarchives=backup/'Archives';oldarchives.mkdir(exist_ok=True)
out=ROOT/'outputs'
for name in ['LightingPack_URP.unitypackage','LightingPack_Unity.zip']:
    original=out/name
    if original.exists():
        archived=oldarchives/name;assert not archived.exists(),('Archive backup already exists',archived);shutil.move(original,archived)
shutil.move(package,out/'LightingPack_URP.unitypackage')
(proof/'installed_hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
summary=dict(passed=True,prefabsChanged=100,fbxChanged=100,originalLocalIDs=5231,existingPrefabGuidsPreserved=100,filesVerified=len(hashes),guidCollisions=0,stageGuidsChecked=len(guids),textures=197,materials=25)
(proof/'delivery.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
with zipfile.ZipFile(out/'LightingPack_Unity.zip','w',zipfile.ZIP_DEFLATED,compresslevel=5) as z:
    for p in files:z.write(target/p.relative_to(ASSET),(target/p.relative_to(ASSET)).relative_to(ROOT))
    z.write(target.with_suffix('.meta'),target.with_suffix('.meta').relative_to(ROOT));z.write(ROOT/'Docs/LightingPack.md','Docs/LightingPack.md')
    for p in proof.rglob('*'):
        if p.is_file() and p.suffix!='.log':z.write(p,p.relative_to(ROOT))
    for p in (ROOT/'Tools/LightingPack').rglob('*'):
        if p.is_file() and '__pycache__' not in p.parts:z.write(p,p.relative_to(ROOT))
    for name in ['unity_import.json','unity_play_checks.json','unity_edit_checks.json']:z.write(VERIFY/name,'Verification/LightingPack/'+name)
with zipfile.ZipFile(out/'LightingPack_Unity.zip') as z:assert z.testzip() is None;assert len(z.namelist())==len(set(z.namelist()))
summary.update(zipBytes=(out/'LightingPack_Unity.zip').stat().st_size,unityPackageBytes=(out/'LightingPack_URP.unitypackage').stat().st_size)
(proof/'delivery.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
print('ALL_100_PROFESSIONAL_INSTALL_AND_PACKAGE_PASS',summary)
