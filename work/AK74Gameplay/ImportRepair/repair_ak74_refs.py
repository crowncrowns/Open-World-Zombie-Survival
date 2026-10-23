import pathlib,tarfile,re,hashlib,json,subprocess,collections
ROOT=pathlib.Path.cwd()
DEST=ROOT/'Assets/Weapons/AK74_Reference'
MIRROR=ROOT/'Assets/AK74_Reference/Unity/Assets/Weapons/AK74_Reference'
PACKAGE=ROOT/'Assets/AK74_Reference/Unity/AK74_Reference.unitypackage'
PREFIX='Assets/Weapons/AK74_Reference'
BACKUP=ROOT/'work/AK74Gameplay/ImportRepair'
REPORT=ROOT/'Verification/AK74Gameplay/ImportRepair.json'
HEADER=re.compile(rb'^guid: ([0-9a-fA-F]{32})\s*$',re.M)
REF=re.compile(rb'\bguid:\s*([0-9a-fA-F]{32})\b')
def digest(data):return hashlib.sha256(data).hexdigest()
def guid(data):
    found=HEADER.findall(data)
    if len(found)!=1:raise RuntimeError('Expected one GUID header')
    return found[0].decode().lower()
def rel(path):return path.relative_to(ROOT).as_posix()
archive={}
with tarfile.open(PACKAGE,'r:gz') as t:
    by={m.name:m for m in t.getmembers()}
    for m in t.getmembers():
        if not m.name.endswith('/pathname'):continue
        pathname=t.extractfile(m).read().decode('utf-8').strip()
        if pathname!=PREFIX and not pathname.startswith(PREFIX+'/'):continue
        entry=m.name.rsplit('/',1)[0]
        raw=t.extractfile(by[entry+'/asset.meta']).read()
        archive[pathname[len(PREFIX):].lstrip('/')]=(guid(raw),raw)
if not archive:raise RuntimeError('Package contains no target entries')
rows=[];mapping={}
for relative,(old,origmeta) in sorted(archive.items()):
    path=DEST/relative if relative else DEST
    meta=pathlib.Path(str(path)+'.meta')
    if not meta.exists():raise RuntimeError('Missing installed '+str(meta))
    current=guid(meta.read_bytes())
    mirror_meta=pathlib.Path(str(MIRROR/relative if relative else MIRROR)+'.meta')
    mirror_guid=guid(mirror_meta.read_bytes()) if mirror_meta.exists() else None
    if old!=current:
        if old in mapping and mapping[old]!=current:raise RuntimeError('Ambiguous old GUID '+old)
        mapping[old]=current
    rows.append({'relativePath':relative,'archiveGuid':old,'installedGuid':current,'mirrorGuid':mirror_guid,'installedMetaSha256':digest(meta.read_bytes()),'archiveMetaSha256':digest(origmeta)})
paths=[ROOT/path for path in subprocess.check_output(['rg','--files','-uuu','Assets','-g','*.meta'],text=True,encoding='utf-8').splitlines()]
owners=collections.defaultdict(list)
for path in paths:
    try:owners[guid(path.read_bytes())].append(rel(path))
    except RuntimeError:pass
conflicts=[]
for old,current in mapping.items():
    if old in owners:conflicts.append({'kind':'old_guid_live_owner','guid':old,'paths':owners[old]})
    if len(owners[current])!=1:conflicts.append({'kind':'current_guid_not_unique','guid':current,'paths':owners[current]})
if conflicts:
    print(json.dumps({'conflicts':conflicts},indent=2));raise RuntimeError('GUID ownership conflict; no mutation')
SUFFIXES={'.meta','.prefab','.unity','.mat','.controller','.asset','.overridecontroller','.rendertexture','.playable','.mask','.anim','.physicmaterial','.lighting','.terrainlayer'}
alltext=[ROOT/path for path in subprocess.check_output(['rg','--files','-uuu','Assets'],text=True,encoding='utf-8').splitlines() if pathlib.Path(path).suffix.lower() in SUFFIXES]
consumers=[];changes=[]
for path in alltext:
    data=path.read_bytes()
    if b'\0' in data[:2048]:continue
    refs=[]
    for match in REF.finditer(data):
        key=match.group(1).decode().lower()
        if key not in mapping:continue
        if path.suffix.lower()=='.meta' and HEADER.match(data,match.start()):continue
        # HEADER.match begins at guid token; plain top-level metadata header is never a reference.
        line_start=data.rfind(b'\n',0,match.start())+1
        if path.suffix.lower()=='.meta' and data[line_start:match.start()]==b'':continue
        refs.append({'oldGuid':key,'newGuid':mapping[key],'line':data[:match.start()].count(b'\n')+1})
    if not refs:continue
    in_installed=path.is_relative_to(DEST)
    consumers.append({'path':rel(path),'scope':'installed' if in_installed else 'sourceMirror' if path.is_relative_to(MIRROR) else 'external','references':refs})
    if not in_installed:continue
    def replace(match):
        key=match.group(1).decode().lower()
        line_start=data.rfind(b'\n',0,match.start())+1
        if path.suffix.lower()=='.meta' and data[line_start:match.start()]==b'':return match.group(0)
        if key not in mapping:return match.group(0)
        return match.group(0)[:-32]+mapping[key].encode('ascii')
    after=REF.sub(replace,data)
    if data==after:continue
    if path.suffix.lower()=='.meta' and guid(after)!=guid(data):raise RuntimeError('Would change GUID header '+rel(path))
    changes.append((path,data,after,refs))
external=[c for c in consumers if c['scope']=='external']
report={'status':'AUDIT_ONLY','package':rel(PACKAGE),'packageSha256':digest(PACKAGE.read_bytes()),'basis':'Original GUIDs read from preserved unitypackage asset.meta by relative pathname; both extracted folders already have regenerated headers.','mapping':rows,'mappingChanges':len(mapping),'ownershipConflicts':conflicts,'externalConsumers':external,'consumers':consumers,'plannedFiles':[{'path':rel(p),'beforeSha256':digest(b),'afterSha256':digest(a),'references':r} for p,b,a,r in changes],'preserves':'Current GUID metadata headers, original package and extracted source mirror, external refs and all fileIDs remain unchanged.'}
BACKUP.mkdir(parents=True,exist_ok=True)
(BACKUP/'Audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'mapRows':len(rows),'changedGuidMappings':len(mapping),'plannedFiles':len(changes),'plannedReferenceCount':sum(len(r) for _,_,_,r in changes),'externalConsumers':external,'installedFiles':[rel(p) for p,_,_,_ in changes]},ensure_ascii=False,indent=2))


# Apply only after the complete path/ownership/reference audit above has succeeded.
import sys, shutil, datetime
if '--apply' in sys.argv:
    if external:raise RuntimeError('External consumers require explicit review; no mutations')
    mirror_before={rel(p):digest(p.read_bytes()) for p in MIRROR.rglob('*') if p.is_file()}
    package_before=digest(PACKAGE.read_bytes())
    for path,before,after,refs in changes:
        if path.read_bytes()!=before:raise RuntimeError('Concurrent edit before backup '+rel(path))
        backup_path=BACKUP/'files'/path.relative_to(ROOT)
        if not backup_path.resolve().is_relative_to(BACKUP.resolve()):raise RuntimeError('Backup escapes root')
        backup_path.parent.mkdir(parents=True,exist_ok=True)
        if backup_path.exists() and backup_path.read_bytes()!=before:raise RuntimeError('Existing backup differs '+str(backup_path))
        shutil.copy2(path,backup_path)
    for path,before,after,refs in changes:
        if path.read_bytes()!=before:raise RuntimeError('Concurrent edit before write '+rel(path))
        path.write_bytes(after)
    checks=[]
    for row in rows:
        path=DEST/row['relativePath'] if row['relativePath'] else DEST
        current=guid(pathlib.Path(str(path)+'.meta').read_bytes())
        if current!=row['installedGuid']:raise RuntimeError('GUID header changed '+row['relativePath'])
    checks.append('All 46 current installed meta GUID headers preserved exactly')
    for path,before,after,refs in changes:
        actual=path.read_bytes()
        if actual!=after:raise RuntimeError('Post-write mismatch '+rel(path))
        fileids=lambda data:re.findall(rb'fileID:\s*-?\d+',data)
        if fileids(before)!=fileids(after):raise RuntimeError('fileID changed '+rel(path))
        external_guids=lambda data:[m.group(1) for m in REF.finditer(data) if m.group(1).decode().lower() not in mapping and m.group(1).decode().lower() not in mapping.values()]
        if external_guids(before)!=external_guids(after):raise RuntimeError('External GUID changed '+rel(path))
        if any(m.group(1).decode().lower() in mapping for m in REF.finditer(actual)):raise RuntimeError('Stale original GUID remains '+rel(path))
    checks.append('All fileID values and GUID references outside mapped package assets preserved')
    checks.append('No mapped stale original GUID reference remains in any changed installed serialization')
    mirror_after={rel(p):digest(p.read_bytes()) for p in MIRROR.rglob('*') if p.is_file()}
    if mirror_before!=mirror_after:raise RuntimeError('Source mirror changed')
    if package_before!=digest(PACKAGE.read_bytes()):raise RuntimeError('Source package changed')
    checks.append('Original source mirror byte-identical and source unitypackage SHA256 unchanged')
    checks.append('Mapped old GUIDs have no live Assets meta owner; all replacement GUIDs have exactly one installed owner')
    checks.append('No stale mapped GUID consumer outside installed/source mirror package folders found')
    report['status']='REPAIRED_STATIC_CHECKS_PASS'
    report['appliedUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    report['backupRoot']=rel(BACKUP/'files')
    report['checks']=checks
    report['changedFileCount']=len(changes)
    report['changedReferenceCount']=sum(len(r) for _,_,_,r in changes)
    report['sourceMirrorHashesBeforeAndAfter']=mirror_before
    report['unityValidation']='Not run by this repair; root agent performs Recovery import and Play separately.'
    REPORT.parent.mkdir(parents=True,exist_ok=True)
    REPORT.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'status':report['status'],'files':len(changes),'references':report['changedReferenceCount'],'report':rel(REPORT),'checks':checks},ensure_ascii=False,indent=2))
