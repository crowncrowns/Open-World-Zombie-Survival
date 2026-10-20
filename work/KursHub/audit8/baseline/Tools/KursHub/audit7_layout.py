"""Preserve every existing placement ID while breaking market repetition."""
from pathlib import Path
import json,math,random
R=Path(__file__).resolve().parents[2];A=R/'work/KursHub/audit7/authoring'
path=A/'Assets/Environment/KursHub/KursHubManifest.json'
d=json.loads(path.read_text(encoding='utf8'));items=d['settlementInstances'];lights=d['settlementLights'];original={p['id'] for p in items}
if 'Audit7 weapons stand 00' in original:raise SystemExit('Layout already revised')
rng=random.Random(71020)
stalls=[p for p in items if p['prop'] in ('KH_StallTimber','KH_StallPipe')]
signs={p['id'].removesuffix(' Shop cloth sign'):p['prop'] for p in items if p['id'].endswith(' Shop cloth sign')}
for stall in stalls:
    prefix=stall['id'];origin=stall['position'][:];delta=rng.uniform(-2.4,2.4);a=math.radians(delta);dx=rng.uniform(-.15,.15);dz=rng.uniform(-.18,.18)
    for p in items+lights:
        if p['id']!=prefix and not p['id'].startswith(prefix+' '):continue
        x,y,z=p['position'];x-=origin[0];z-=origin[2]
        p['position']=[origin[0]+x*math.cos(a)+z*math.sin(a)+dx,y,origin[2]-x*math.sin(a)+z*math.cos(a)+dz]
        if 'rotation' in p:p['rotation'][1]+=delta
    # Stock matches the specialist sign. Prior goods are retained on the back
    # shelf; no original prop type or placement identity is removed.
    if signs.get(prefix)=='KH_SignWeapons':
        yaw=math.radians(stall['rotation'][1]);x,y,z=stall['position'];ox,oz=0,.35
        items.append(dict(id=f'Audit7 weapons stand {len([p for p in items if p["id"].startswith("Audit7 weapons stand")]):02}',prop='KH_WeaponsDisplay',position=[x+ox*math.cos(yaw)+oz*math.sin(yaw),1.06,z-ox*math.sin(yaw)+oz*math.cos(yaw)],rotation=stall['rotation'][:],scale=[1,1,1]))
        for p in items:
            if p['id']==prefix+' Counter goods':
                p['position'][0]+=math.cos(yaw)*1.15;p['position'][2]-=math.sin(yaw)*1.15;p['scale']=[v*.60 for v in p['scale']]

# Scattered waste lives at stall edges, with an intentional clear continuous
# movement lane at X=3.4 and all stair/KPP approaches untouched.
for i,z in enumerate((-33,-27,-18,-11,-3,4)):
    for side in (-1,1):
        x=side*rng.uniform(4.65,5.10)
        items.append(dict(id=f'Audit7 market edge debris {i} {side}',prop='KH_FloorDebris',position=[x,.002,z+rng.uniform(-.4,.4)],rotation=[0,rng.uniform(0,360),0],scale=[.7,.7,.7]))
assert original<={p['id'] for p in items} and len({p['id'] for p in items})==len(items)
d['qualityRevision']=7;d['qualityLayout']='31 irregular linked stall assemblies; weapons stock follows signs; existing IDs preserved'
path.write_text(json.dumps(d,indent=2),encoding='utf8')
print('AUDIT7_LAYOUT_OK',len(items),'placements',len(stalls),'clusters; all',len(original),'prior IDs retained')
