"""Break the market's repeated silhouette while retaining native instance IDs."""
from pathlib import Path
from copy import deepcopy
import json,math
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit8';A=W/'authoring'
path=A/'Assets/Environment/KursHub/KursHubManifest.json'
p=json.loads(path.read_text(encoding='utf8'))
if p.get('hub8Market'):
 print('AUDIT8_MARKET_ALREADY_AUTHORED')
 raise SystemExit(0)
rows=p['settlementInstances'];byid={r['id']:r for r in rows}
oldids=set(byid);changes=[]
# Recessed, narrower bays create storage pockets and separate the four trades.
# Only outward motion is used; the retained centre and stair routes stay clear.
designs=[('West',1,.85,.91),('West',3,1.35,.87),('West',4,.40,.96),
         ('West',6,1.05,.90),('East',0,.55,.94),('East',2,1.20,.88),
         ('East',5,.95,.90),('East',7,.45,.96)]
for side,index,retreat,width in designs:
 key=f'{side} atrium stall {index:02d}';anchor=byid[key];origin=anchor['position'][:]
 yaw=math.radians(anchor['rotation'][1]);cs,sn=math.cos(yaw),math.sin(yaw)
 target=origin[:];target[0]+=(-1 if side=='West' else 1)*retreat
 members=[r for r in rows if r['id']==key or r['id'].startswith(key+' ')]
 # The independent revision-7 firearms display belongs to its nearest booth.
 for r in rows:
  if r['prop']=='KH_WeaponsDisplay' and abs(r['position'][1]-1.06)<.01:
   if math.hypot(r['position'][0]-origin[0],r['position'][2]-origin[2])<.75:members.append(r)
 lights=[r for r in p['settlementLights'] if r['id'].startswith(key+' ')]
 for r in members+lights:
  before=deepcopy(r);dx,dz=r['position'][0]-origin[0],r['position'][2]-origin[2]
  lx=(cs*dx-sn*dz)*width;lz=sn*dx+cs*dz
  r['position'][0]=target[0]+cs*lx+sn*lz;r['position'][2]=target[2]-sn*lx+cs*lz
  if 'scale' in r:r['scale'][0]*=width
  changes.append(dict(id=r['id'],before=before,after=deepcopy(r)))
added=[]
def put(id,prop,pos,yaw=0,scale=(1,1,1)):
 row=dict(id='Hub8 market '+id,prop=prop,position=list(pos),rotation=[0,yaw,0],scale=list(scale))
 assert row['id'] not in byid
 rows.append(row);byid[row['id']]=row;added.append(row['id'])
put('Medical provisions shelving','KH_UtilityRack',(-12.1,0,-25.0),-90)
put('Medical provisions sacks','KH_SupplySackStack',(-12.4,0,-27.0),-76,(.8,.8,.8))
put('Mending work station','KH_RepairBench',(-12.0,0,-15.2),-90)
put('Mending personal bundle','KH_ResidentBundle',(-12.6,0,-17.1),18)
put('Tool repair work station','KH_RepairBench',(12.0,0,-20.4),90)
put('Tool repair side stock','KH_LowDisplay',(12.3,0,-22.3),84,(.8,1,.8))
put('Clothing side stock','KH_ResidentBundle',(11.8,0,-5.9),-30)
put('Clothing personal locker','KH_PersonalLocker',(12.4,0,-7.3),90)
put('Rear shared bulletin','KH_CommunityBoard',(-12.5,0,4.0),-90)
assert oldids<={r['id'] for r in rows}
for side,index,_,_ in designs:
 row=byid[f'{side} atrium stall {index:02d}']
 assert abs(row['position'][0])>7
p['stage']=8;p['qualityRevision']=8
p['hub8Market']=dict(retainedOldInstanceIds=True,changedAssemblies=len(designs),
                     changes=changes,addedPlacementIds=added,
                     design='Recessed repair/provision bays, narrower booth widths, side stock and shelving; no inward move into retained centre lane',
                     validation='Authoring only; imported collision, NPC scene positions and player-height views still require checks')
path.write_text(json.dumps(p,ensure_ascii=False,indent=2),encoding='utf8')
(W/'market_layout8.json').write_text(json.dumps(p['hub8Market'],ensure_ascii=False,indent=2),encoding='utf8')
print('AUDIT8_MARKET_AUTHORED',len(designs),'retained assemblies;',len(added),'new support placements')
