"""Publish only the exact target-engine-tested assets, preserving concurrent scene edits."""
from pathlib import Path
import hashlib,json,re,shutil,os,argparse
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit7';T=R/'work/KursHub/UnityProject';A=W/'authoring';B=W/'baseline';F=R/'FINAL_ASSETS';D=Path('Assets/Environment/KursHub');SC=Path('Assets/CharacterTesting/CharacterTest.unity')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return p.read_text(encoding='utf-8-sig')
def js(p):return json.loads(read(p))
def guid(p):return re.search(r'^guid: ([a-f0-9]+)',read(p),re.M)[1]
def ids(p):return set(re.findall(r'^--- !u!\d+ &(-?\d+)',read(p),re.M))
def library_hash():
    # Match C# StringComparer.Ordinal used by the actual Play/static proof;
    # WindowsPath's default sort folds case and changes e.g. Drape_/DrapeBlue.
    paths=sorted((p for p in (T/D).rglob('*') if p.is_file() and p.suffix!='.meta' and p.relative_to(T/D).as_posix() not in ('Models/KursHubShell.fbx','KursHub.prefab','KursHubManifest.json')),key=lambda p:p.relative_to(T).as_posix())
    return hashlib.sha256('\n'.join(p.relative_to(T).as_posix()+':'+sha(p) for p in paths).encode()).hexdigest()
def verify():
    play=read(W/'play_validation.txt');scene=read(W/'scene_validation.txt');h=library_hash()
    assert 'KURS_PLAY_OK:' in play and '\nFAIL ' not in play
    assert 'KURS_AUDIT7_SCENE_CHECK_OK' in scene
    for text in (play,scene):
        assert 'VERIFIED_LIBRARY_SHA256='+h in text,'Stale library verification'
        for key,path in [('FBX','Models/KursHubShell.fbx'),('PREFAB','KursHub.prefab'),('MANIFEST','KursHubManifest.json')]:assert 'VERIFIED_'+key+'_SHA256='+sha(T/D/path) in text,'Stale '+key
    assert js(W/'release_geometry/fbx_roundtrip.json')['status']=='AUDIT7_PROP_ROUNDTRIP_OK'
    assert js(W/'release_sources.json')['status']=='AUDIT7_NATIVE_SOURCES_OK'
    assert js(W/'architecture_roundtrip.json')['status']=='AUDIT7_ARCHITECTURE_ROUNDTRIP_OK'
    assert js(W/'reports/PBR_Readback.json')['status']=='PASS'
    imported=js(W/'package_import_validation.json');assert imported['status']=='PASS'
    assert imported['packageSha256']==sha(F/'KursHub_Final.unitypackage')
    source_proof={v['file']:v['sha256'] for v in js(W/'release_sources.json')['files']}
    assert all(sha(A/'Sources/KursHub'/name)==expected for name,expected in source_proof.items())
    assert sha(R/SC)==sha(T/SC)==sha(W/'scene_snapshot_after_user_edit'/SC),'Concurrent scene edit: recheck the current scene without overwriting it'
    assert ids(B/D/'KursHub.prefab')<=ids(T/D/'KursHub.prefab'),'Original prefab fileID lost'
    refs=set(re.findall(r'\{fileID: (-?\d+), guid: ff5cdff5a433ef342ad7bf24aeafd8b8, type: 3\}',read(R/SC)))-{'100100000'}
    effective=set(read(W/'effective_prefab_ids.txt').splitlines())
    assert refs<=(ids(T/D/'KursHub.prefab')|effective),'Current scene target is not a native prefab object'
    for p in (R/'Assets/Editor').glob('KursHub*.cs'):assert sha(p)==sha(T/'Assets/Editor'/p.name),'Untested editor change '+p.name
    baseline=js(B/'hashes.json')
    for path,expected in baseline.items():
        if path.startswith(('Assets/Environment/KursHub/','Sources/KursHub/')):
            assert sha(R/path)==expected,'Concurrent authored asset change '+path
    return h,len(refs)
def deploy():
    h,refcount=verify();pending=[]
    for src in (T/D).rglob('*'):
        if not src.is_file():continue
        dst=R/src.relative_to(T)
        if src.suffix=='.meta' and dst.exists():assert guid(src)==guid(dst),'Changed GUID '+str(dst)
        if not dst.exists() or sha(src)!=sha(dst):pending.append((src,dst))
    for name in ('KursHub.blend','KursHubProps.blend'):pending.append((A/'Sources/KursHub'/name,R/'Sources/KursHub'/name))
    def order(pair):
        p=pair[1].as_posix();return (4 if p.endswith('/KursHub.prefab') else 3 if '/Props/Prefabs/' in p else 2 if '/Materials/' in p else 1 if p.endswith('Manifest.json') else 0,p)
    temp=W/'atomic_publish';temp.mkdir(exist_ok=True)
    for i,(src,dst) in enumerate(sorted(pending,key=order)):
        q=temp/(str(i)+src.suffix);shutil.copy2(src,q);dst.parent.mkdir(parents=True,exist_ok=True);os.replace(q,dst)
    assert all(sha(s)==sha(t) for s,t in pending)
    assert sha(R/SC)==sha(W/'scene_snapshot_after_user_edit'/SC)
    report=dict(status='AUDIT7_DEPLOYED',librarySha256=h,packageSha256=sha(F/'KursHub_Final.unitypackage'),copiedFiles=len(pending),sceneRewritten=False,sceneSha256=sha(R/SC),sceneNativeTargetsPreserved=refcount,prefabSha256=sha(R/D/'KursHub.prefab'),oldPrefabIdsRetained=len(ids(B/D/'KursHub.prefab')),blenderSourcesReadback=True)
    (W/'deployment.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('AUDIT7_DEPLOYED',len(pending),'files;',refcount,'current scene targets retained')
def organize():
    architecture={'KH_Partition4','KH_Shopfront6','KH_DoorPartition4','KH_MeshPartition4','KH_DirectionBoard','KH_MedicalSign'}
    def cp(src,dst):
        dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dst)
        meta=Path(str(src)+'.meta')
        if meta.exists():shutil.copy2(meta,Path(str(dst)+'.meta'))
    for name,dst in [('KursHub.blend','Buildings_Final.blend'),('KursHubProps.blend','Props_Final.blend')]:cp(A/'Sources/KursHub'/name,F/'BLENDER'/dst)
    for model in (T/D/'Models').rglob('*.fbx'):
        rel=model.relative_to(T/D/'Models');cp(model,F/'FBX/Buildings'/rel);cp(model,F/'UNITY/Models/Buildings'/rel)
    for model in (T/D/'Props/Models').rglob('*.fbx'):
        name=re.sub(r'_LOD[12]$','',model.stem);kind='Architecture' if name in architecture else 'Props';rel=Path('LOD')/model.name if model.parent.name=='LOD' else Path(model.name)
        cp(model,F/'FBX'/kind/rel);cp(model,F/'UNITY/Models'/kind/rel)
    for prefab in (T/D/'Props/Prefabs').glob('*.prefab'):cp(prefab,F/'UNITY/Prefabs'/('Architecture' if prefab.stem in architecture else 'Props')/prefab.name)
    cp(T/D/'KursHub.prefab',F/'UNITY/Prefabs/Buildings/KursHub.prefab')
    for folder,target in [('Materials','Materials'),('Textures','Textures'),('Scenes','Scenes'),('InteriorMeshes','Models/Architecture/DerivedFloors')]:
        for p in (T/D/folder).rglob('*'):
            if p.is_file() and p.suffix!='.meta':cp(p,F/'UNITY'/target/p.relative_to(T/D/folder))
    for p in (T/D/'Textures').glob('*.png'):
        family=p.name.split('_')[0]
        kind='Wood' if family in ('TimberRaw','WornWood') else 'Metal' if family in ('BluePaint','RustedSteel','Copper') else 'Concrete' if family in ('CreamConcrete','FloorConcrete') else 'Glass' if family=='GlassGrime' else 'Other'
        cp(p,F/'TEXTURES'/kind/p.name)
    (F/'TEXTURES/Brick').mkdir(parents=True,exist_ok=True)
    (F/'TEXTURES/Brick/README.txt').write_text('No exposed brick material is present in the supplied Kurs references; no artificial brickwork was invented.\n',encoding='utf8')
    for folder in ('Reference_Comparisons','Final_Renders'):(F/'PREVIEWS'/folder).mkdir(parents=True,exist_ok=True)
    for p in (W/'after').glob('*.png'):cp(p,F/'PREVIEWS/Final_Renders'/p.name)
    for p in (W/'previews').glob('*.png'):
        if p.name not in ('front.png','left.png','rear.png','market.png','checkpoint.png','gallery.png'):cp(p,F/'PREVIEWS/Final_Renders'/('extra_'+p.name))
    for p in [W/'deployment.json',W/'package_import_validation.json',W/'play_validation.txt',W/'scene_validation.txt',W/'release_sources.json',W/'architecture_roundtrip.json',W/'architecture_text_fixes.json',W/'release_geometry/fbx_roundtrip.json',W/'soft_shading/soft_shading.json',W/'printed_cloth/printed_cloth.json',W/'architecture_before.json',W/'architecture_fixes.json',W/'geometry/props_before.json',W/'geometry/lod_export_repairs.json',W/'geometry/prop_source_properties.json',W/'source_assembly.json']:
        if p.exists():cp(p,F/'REPORTS'/p.name)
    for p in (W/'reports').glob('*'):
        if p.is_file():cp(p,F/'REPORTS'/p.name)
    print('FINAL_ASSETS_ORGANIZED')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--organize',action='store_true');args=parser.parse_args()
    organize() if args.organize else deploy()
