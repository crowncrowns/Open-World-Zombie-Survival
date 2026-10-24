import json,re,tarfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
session=Path(r'C:\Users\Alex\.codex\sessions\2026\10\23\rollout-2026-10-23T21-58-14-01a16a20-c0a6-7831-b783-ea4bd0a7550e.jsonl')
files={}
for line in session.open(encoding='utf8'):
    row=json.loads(line);p=row.get('payload',{})
    if row.get('type')!='response_item' or p.get('type')!='custom_tool_call':continue
    code=p.get('input','')
    for match in re.finditer(r'tools\.apply_patch\(',code):
        try:patch,_=json.JSONDecoder().raw_decode(code[match.end():])
        except ValueError:continue
        lines=patch.splitlines();i=0
        while i<len(lines):
            l=lines[i]
            if not l.startswith(('*** Add File: ','*** Update File: ')):i+=1;continue
            op='add' if l.startswith('*** Add') else 'update';path=Path(l.split(': ',1)[1]);i+=1
            section=[]
            while i<len(lines) and not lines[i].startswith('*** '):section.append(lines[i]);i+=1
            if not ('Tools/PotbellyStove/' in str(path).replace('\\','/') or str(path).replace('\\','/').endswith('Docs/PotbellyStove.md')):continue
            key=str(path)
            if op=='add':files[key]='\n'.join(x[1:] for x in section if x.startswith('+'))+'\n'
            else:
                content=files[key];chunks=[];chunk=[]
                for x in section:
                    if x.startswith('@@'):
                        if chunk:chunks.append(chunk)
                        chunk=[]
                    else:chunk.append(x)
                if chunk:chunks.append(chunk)
                for chunk in chunks:
                    old='\n'.join(x[1:] for x in chunk if x.startswith((' ','-')))
                    new='\n'.join(x[1:] for x in chunk if x.startswith((' ','+')))
                    if old not in content:raise RuntimeError('Cannot recover hunk '+key)
                    content=content.replace(old,new,1)
                files[key]=content
for path,content in files.items():
    p=Path(path);p.parent.mkdir(parents=True,exist_ok=True);p.write_text(content,encoding='utf8')
print('Recovered source files',len(files))
# Restore only this asset into the isolated project, preserving all Unity GUIDs.
project=ROOT/'work/PotbellyStove/UnityProject'
with tarfile.open(ROOT/'outputs/PotbellyStove_Unity.unitypackage') as tar:
    names=set(tar.getnames())
    for n in names:
        if not n.endswith('/pathname'):continue
        rel=tar.extractfile(n).read().decode();assert rel.startswith('Assets/Environment/Props/PotbellyStove') and '..' not in rel
        dest=project/rel;folder=n.rsplit('/',1)[0];asset=folder+'/asset';meta=folder+'/asset.meta'
        if asset in names:
            dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(tar.extractfile(asset).read())
        else:dest.mkdir(parents=True,exist_ok=True)
        if meta in names:dest.with_name(dest.name+'.meta').write_bytes(tar.extractfile(meta).read())
print('Recovered native Unity package')
