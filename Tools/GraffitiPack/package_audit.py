from pathlib import Path
import hashlib,json,tarfile
from PIL import Image
import numpy as np

root=Path(__file__).resolve().parents[2]
pack=root/'Assets/PostApocalypse/GraffitiPack'
output=root/'Verification/GraffitiPack'
package=root/'outputs/GraffitiPack_URP.unitypackage'
expected=len(json.loads((pack/'Documentation/catalog.json').read_text(encoding='utf-8'))['items'])
def sha(data):return hashlib.sha256(data).hexdigest()
with tarfile.open(package,'r:gz') as archive:
    members={m.name:m for m in archive.getmembers()}
    paths={}
    for name,m in members.items():
        if name.endswith('/pathname'):
            path=archive.extractfile(m).read().decode('utf-8').strip('\x00\r\n')
            key=name.rsplit('/',1)[0]+'/asset'
            if key in members:paths[path]=sha(archive.extractfile(members[key]).read())
    mismatch=[]
    for path,digest in paths.items():
        local=root/path
        if not local.exists() or sha(local.read_bytes())!=digest:mismatch.append(path)
    texture_paths=[p for p in paths if '/Textures/' in p and p.endswith('.png')]
    prefab_paths=[p for p in paths if '/Prefabs/' in p and p.endswith('.prefab')]
    assert len(texture_paths)==expected and len(prefab_paths)==expected,(len(texture_paths),len(prefab_paths))
    assert not mismatch,mismatch
texture_report=json.loads((output/'texture_validation.json').read_text(encoding='utf-8'))
assert texture_report['count']==expected and texture_report['uniqueHashes']==expected
for row in texture_report['items']:
    p=root/row['file'];im=Image.open(p);a=np.asarray(im)[:,:,3]
    assert im.mode=='RGBA' and max(im.size)==2048
    assert a.min()==0 and a.max()>100
    assert all(x.max()==0 for x in [a[0],a[-1],a[:,0],a[:,-1]])
files=[dict(path=str(p.relative_to(root)).replace('\\','/'),bytes=p.stat().st_size,sha256=sha(p.read_bytes())) for p in sorted(pack.rglob('*')) if p.is_file()]
(output/'asset_manifest.json').write_text(json.dumps(dict(files=files),ensure_ascii=False,indent=2),encoding='utf-8')
result=dict(package=str(package.relative_to(root)),packageSha256=sha(package.read_bytes()),packageBytes=package.stat().st_size,packagedAssets=len(paths),sourcePNGs=expected,prefabs=expected,allPackagePayloadsMatchCheckout=True,allPNGsRGBA2048AndZeroAlphaBorders=True)
(output/'package_integrity.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
