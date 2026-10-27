from catalog import *
import re,subprocess,json
guids={};collisions=[]
for f in list(ASSET.rglob('*.meta'))+[ASSET.with_suffix('.meta')]:
    if not f.exists():continue
    m=re.search(r'^guid: ([0-9a-f]{32})$',f.read_text(encoding='utf-8'),re.M)
    if not m:continue
    guid=m.group(1)
    if guid in guids:collisions.append([guid,guids[guid],str(f)])
    guids[guid]=str(f)
assert not collisions,collisions
patterns=ROOT/'work/LightingPack/guid_patterns.txt';patterns.write_text('\n'.join('guid: '+g for g in guids),encoding='ascii')
result=subprocess.run(['rg','-n','-F','-f',str(patterns),str(ROOT/'Assets'),'-g','*.meta'],capture_output=True,text=True,encoding='utf-8')
assert result.returncode in (0,1),result.stderr
lines=[l for l in result.stdout.splitlines() if '\\PostApocalypse\\LightingPack\\' not in l and '/PostApocalypse/LightingPack/' not in l]
assert not lines,lines[:20]
(VERIFY/'guid_check.json').write_text(json.dumps(dict(passed=True,checked=len(guids),collisions=0),indent=2),encoding='utf-8')
print('GUID_CHECK_PASS',len(guids))
