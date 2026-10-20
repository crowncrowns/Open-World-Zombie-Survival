"""Restore the reference's layered settlement perimeter from reusable props."""
from pathlib import Path
import json,random
R=Path(__file__).resolve().parents[2];p=R/'work/KursHub/audit7/authoring/Assets/Environment/KursHub/KursHubManifest.json'
d=json.loads(p.read_text(encoding='utf8'));items=d['propInstances'];old={x['id'] for x in items};rng=random.Random(730)
assert not any(n.startswith('Audit7 perimeter') for n in old),'Already applied'
def put(name,prop,xyz,yaw=0,scale=(1,1,1)):
    items.append(dict(id='Audit7 perimeter '+name,prop=prop,position=list(xyz),rotation=[0,yaw,0],scale=list(scale)))
# Central 16m arrival remains open from the existing test-scene access path.
for side in (-1,1):
    for i in range(13):
        x=side*(10+i*2.5);z=-73.5+rng.uniform(-.10,.10)
        put(f'front fence {side} {i}','KH_SecurityFence',(x,0,z),rng.uniform(-1.5,1.5))
        if i%3==0:
            put(f'front panel {side} {i}','KH_WindowBoards',(x,.20,z-.16),rng.uniform(-2,2),(.8,.78,1))
            put(f'front barricade {side} {i}','KH_Barricade',(x,0,z-1.2),rng.uniform(-4,4))
        if i in (2,7,11):
            put(f'front stock {side} {i}','KH_PalletCrates',(x,0,z+2.1),rng.uniform(-20,20),(.8,.8,.8))
    for i in range(7):
        put(f'side fence {side} {i}','KH_SecurityFence',(side*50.5,0,-71+i*2.5),side*90+rng.uniform(-2,2))
    put(f'entrance supply {side}','KH_SupplySackStack',(side*9.1,0,-69.8),side*12)
    put(f'entrance cloth {side}','KH_RailDrapeBlue',(side*14,1.2,-73.7),0,(1.6,1,1))
    put(f'watch post {side}','KH_WatchPost',(side*44.5,0,-68),side*8)
    put(f'watch sacks {side}','KH_SupplySackStack',(side*42,0,-68.3),side*20)
for i,(x,z) in enumerate(((-49,3),(-49,36),(49,9),(49,42),(-30,68),(-13,68),(10,68))):
    put(f'yard stock {i}','KH_PalletCrates',(x,0,z),rng.uniform(-25,25))
assert old<={x['id'] for x in items}
d['qualityExterior']='Reference-led perimeter of reusable mesh fence, boards, barricades and stocks; 16m open arrival; no entrance canopy'
p.write_text(json.dumps(d,indent=2),encoding='utf8');print('AUDIT7_PERIMETER_OK',len(items),'instances;',len(old),'prior IDs retained')
