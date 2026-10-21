"""Revision-8 occupied districts: compact workflows and purposeful furniture.

Data-only, deterministic authoring layer.  Existing stage-7 IDs never move.
The CLI writes a REVIEW CANDIDATE under work/audit8, never the shared manifest.
Imported colliders, player traversal and rendered review remain authority.
"""
from copy import deepcopy
from math import cos, hypot, radians, sin

from audit8_utilities import transform

DECOR = {'KH_RugRed','KH_RugBlue','KH_RugWoven','KH_FloorDebris','KH_GrimePatch',
         'KH_RailDrape','KH_RailDrapeBlue','KH_TarpWide','KH_TarpNarrow',
         'KH_KursAtriumBanner','KH_SignWeapons','KH_SignMedicalTrade','KH_SignGear',
         'KH_SignRepair','KH_CheckpointHeader','KH_CheckpointRules','KH_DirectionBoard',
         'KH_MedicalSign','KH_CableSag','KH_StringLights','KH_Floodlight','KH_FloorWearPatch'}
GROUPS = ('propInstances','interiorInstances','settlementInstances')


def aabb(placement, prop):
    lo, hi = prop['boundsMin'], prop['boundsMax']
    points = [transform((x,y,z), placement) for x in (lo[0],hi[0])
              for y in (lo[1],hi[1]) for z in (lo[2],hi[2])]
    return [min(p[i] for p in points) for i in range(3)]+[max(p[i] for p in points) for i in range(3)]


def _overlap(a, b, margin=.025):
    # A box resting on a real counter top shares a support plane.  Do not turn
    # that intentional contact into a false collision by padding vertical Y.
    return (min(a[4],b[4])-max(a[1],b[1]) > .0001
            and all(min(a[i+3],b[i+3])-max(a[i],b[i]) > -margin for i in (0,2)))


def _point_segment(p,a,b):
    dx,dz=b[0]-a[0],b[1]-a[1]
    t=max(0.,min(1.,((p[0]-a[0])*dx+(p[1]-a[1])*dz)/(dx*dx+dz*dz))) if dx*dx+dz*dz else 0.
    return hypot(p[0]-a[0]-t*dx,p[1]-a[1]-t*dz)


def _segment_rect(a,b,rect):
    x0,x1,z0,z1=rect
    t0,t1=0.,1.
    for start,delta,lo,hi in ((a[0],b[0]-a[0],x0,x1),(a[1],b[1]-a[1],z0,z1)):
        if abs(delta)<1e-12:
            if not lo<=start<=hi:break
        else:
            u,v=(lo-start)/delta,(hi-start)/delta
            if u>v:u,v=v,u
            t0,t1=max(t0,u),min(t1,v)
            if t0>t1:break
    else:return 0.
    def point_rect(p):return hypot(max(x0-p[0],0,p[0]-x1),max(z0-p[1],0,p[1]-z1))
    return min(point_rect(a),point_rect(b),*(_point_segment((x,z),a,b) for x in (x0,x1) for z in (z0,z1)))


def augment(manifest, prop_manifest):
    """Apply to a caller-owned copy and return before/after/proof records.

    True FBX local bounds are transformed through every current placement.  All
    51 route segments, their corner disks, inspection positions and camera-floor
    positions retain a standing corridor of radius >=.85 m.  New furniture is
    tested against original/current furniture, partition bounds and real .65 m
    concrete columns.  Rugs, wear films and overhead decoration are not blockers.
    """
    previous=manifest.get('hub8Density')
    if previous:
        by_id={p['id']:p for key in GROUPS for p in manifest[key]}
        for row in previous['managedState']:
            if by_id.get(row['id'])!=row:raise ValueError('Preserve manually changed density object: '+row['id'])
        return previous
    original={key:deepcopy(manifest[key]) for key in GROUPS}
    props={p['id']:p for p in prop_manifest['props']}
    by_id={p['id']:p for key in GROUPS for p in manifest[key]}
    spatial=manifest['hub8Spatial'];zones={z['id']:z for z in spatial['zones']}
    moves=[];additions=[];accepted=[];rejected=[];roles={};clusters=[];light_fixtures=[]
    def move(identifier, position, yaw=None):
        row=by_id[identifier]
        if not identifier.startswith('Hub8 ') or identifier.startswith('Hub8 utility '):
            raise AssertionError('Only new owned service rows may move: '+identifier)
        before=deepcopy(row);row['position']=list(position)
        if yaw is not None:row['rotation']=[0,yaw,0]
        previous_move=next((entry for entry in moves if entry['id']==identifier),None)
        if previous_move:previous_move['after']=deepcopy(row)
        else:moves.append(dict(id=identifier,before=before,after=deepcopy(row)))
    def move_cluster(parent, children, position, yaw=None):
        before=deepcopy(by_id[parent]);after=deepcopy(before);after['position']=list(position)
        if yaw is not None:after['rotation']=[0,yaw,0]
        angle=radians(after['rotation'][1]-before['rotation'][1]);c,s=cos(angle),sin(angle)
        for identifier in (parent,*children):
            row=by_id[identifier];dx,dy,dz=[row['position'][i]-before['position'][i] for i in range(3)]
            target=[position[0]+dx*c+dz*s,position[1]+dy,position[2]-dx*s+dz*c]
            move(identifier,target,row['rotation'][1]+after['rotation'][1]-before['rotation'][1])

    # One compact cooking -> wash -> preparation -> portioning line.  The
    # protected delivery route remains at Z55, north of every work front.
    prefix='Hub8 Ground rear Kitchen '
    move(prefix+'Cooking line',(-42.8,0,52),-90)
    move_cluster(prefix+'Preparation counter',[prefix+'Counted preparation food'],(-36.2,0,52),180)
    move_cluster(prefix+'Portioning counter',[prefix+'Portioned food'],(-32.7,0,52),180)
    for child,parent in ((prefix+'Counted preparation food',prefix+'Preparation counter'),
                         (prefix+'Portioned food',prefix+'Portioning counter')):
        row,owner=by_id[child],by_id[parent]
        height=aabb(owner,props[owner['prop']])[4]-props[row['prop']]['boundsMin'][1]*row['scale'][1]+.002
        move(child,(row['position'][0],height,row['position'][2]))
    move(prefix+'Clean wash line',(-39.5,0,51.5),180)
    move(prefix+'Dry food rack',(-34,0,61.0))
    move(prefix+'Produce containers',(-31.8,0,61.1),0)
    move(prefix+'Food sacks',(-26.7,0,60.8),0)
    move(prefix+'Maintenance stool',(-36.2,0,53.4),180)
    move(prefix+'Task mat',(-39,.008,52.6),0)
    move('Hub8 Ground rear mess Meal issue reserve',(-14,0,62.3),0)
    move('Hub8 Ground rear mess Returned containers',(14,0,62.0),0)
    for side,label in ((-1,'Mending and quiet work'),(1,'Laundry and household care')):
        prefix='Hub8 Upper rear '+label+' '
        target_x=-26.1 if side<0 else 25.0
        move_cluster(prefix+'Shared work table',[prefix+'South work chair',prefix+'North work chair',prefix+'Workplace rug'],
                     (target_x,5.5,35),0)
        if side>0:move(prefix+'Household work station',(28.6,5.5,35),180)
        else:move(prefix+'Sorted cloth bundle',(-29.7,5.5,41.9),-17)

    # Main standing corridors include both route segments and requested sample
    # disks.  Segment/rectangle distance protects diagonal turns as well.
    routes=[]
    for row in spatial['routes']:
        radius=max(.85,row.get('clearWidth',1.7)/2)
        for a,b in zip(row['points'],row['points'][1:]):
            routes.append((row['id'],a,b,radius))
    for zone in spatial['zones']:
        for index,point in enumerate(zone.get('inspectionPoints',[])):
            routes.append((zone['id']+' inspection '+str(index),point,point,.85))
    for camera in spatial['cameras']:
        p=camera['position'];floor=min((0.,5.5,11.),key=lambda y:abs(p[1]-1.65-y))
        if abs(p[1]-1.65-floor)<.15:
            point=[p[0],floor,p[2]];routes.append((camera['id']+' camera standing',point,point,.85))
    column_boxes=[]
    for x in (-36,-24,-16,16,24,36):
        for z in (-40,-28,-16,-4,8,20,32,44,56):
            if x==-36 and z==44:continue
            column_boxes.append(('Concrete column '+str((x,z)),[x-.325,0,z-.325,x+.325,10.65,z+.325]))
    for x in (-15.35,15.35):
        for z in (-32,-20,-8,4,18):
            column_boxes.append(('Atrium column '+str((x,z)),[x-.325,0,z-.325,x+.325,10.65,z+.325]))
    def solid(row):
        return row['prop'] not in DECOR and not row['prop'].startswith('KH_FloorWear')
    occupied=[(row['id'],aabb(row,props[row['prop']])) for row in by_id.values() if solid(row)]
    occupied+=column_boxes
    def problems(row,zone,pending=()):
        box=aabb(row,props[row['prop']]);floor=zone['floorY'];issues=[]
        if not solid(row):return [],box
        x0,x1,z0,z1=zone['bounds']
        if box[0]<x0+.08 or box[3]>x1-.08 or box[2]<z0+.08 or box[5]>z1-.08:
            issues.append('District boundary / wall-window setback')
        for identifier,other in (*occupied,*pending):
            if identifier==row['id']:continue
            # Surface stock has an actual supporting top, so the shared face
            # at Y.66/1.055 is allowed; floor bodies keep a 25 mm tolerance.
            if _overlap(box,other,0.001 if row.get('_role')=='surface stock' else .025):
                issues.append('Occupied AABB: '+identifier)
        if box[1]<floor+2.05 and box[4]>floor+.08:
            rect=[box[0],box[3],box[2],box[5]]
            for identifier,a,b,radius in routes:
                if abs(a[1]-floor)>.1 or abs(b[1]-floor)>.1:continue
                if _segment_rect((a[0],a[2]),(b[0],b[2]),rect)<radius+.045:
                    issues.append('Protected standing corridor: '+identifier)
            for clear in spatial['circulationExclusions']:
                if abs(clear.get('floorY',-99)-floor)>.1:continue
                if clear['type'] not in ('clear footprint','inaccessible retained volume','solid retained construction'):continue
                footprints=clear.get('footprints',[clear.get('bounds')])
                for bound in footprints:
                    if bound and min(box[3],bound[1])-max(box[0],bound[0])>.001 and min(box[5],bound[3])-max(box[2],bound[2])>.001:
                        issues.append('Protected construction/approach: '+clear['id'])
        return issues,box
    def offer(zone_id,label,members,centre,variants=((0,0),),purpose=''):
        zone=zones[zone_id];attempts=[]
        for dx,dz in variants:
            proposed=[];pending=[];errors=[]
            for name,prop,offset,yaw,scale,role in members:
                row=dict(id='Hub8 density '+zone_id+' '+label+' '+name,prop=prop,
                         position=[round(centre[0]+dx+offset[0],5),round(zone['floorY']+offset[1],5),round(centre[1]+dz+offset[2],5)],
                         rotation=[0,float(yaw),0],scale=list(scale),_role=role)
                if role=='surface stock':
                    row['position'][1]-=props[prop]['boundsMin'][1]*scale[1]
                issue,box=problems(row,zone,pending)
                errors.extend(issue);proposed.append((row,box))
                if solid(row):pending.append((row['id'],box))
            if errors:
                attempts.append(dict(offset=[dx,dz],issues=sorted(set(errors))))
                continue
            for row,box in proposed:
                role=row.pop('_role');manifest['settlementInstances'].append(row);by_id[row['id']]=row
                additions.append(row['id']);roles[row['id']]=role
                if solid(row):occupied.append((row['id'],box))
                accepted.append(dict(id=row['id'],zone=zone_id,role=role,bounds=box))
            clusters.append(dict(id=zone_id+' '+label,purpose=purpose,placements=[row['id'] for row,box in proposed],
                                 acceptedOffset=[dx,dz],rejectedAlternatives=len(attempts)))
            return [row for row,box in proposed]
        rejected.append(dict(zone=zone_id,label=label,purpose=purpose,attempts=attempts))
        return []
    unit=(1,1,1)
    def member(name,prop,offset=(0,0,0),yaw=0,scale=unit,role='furniture'):
        return name,prop,offset,yaw,scale,role
    def table(zone,label,x,z,seating='bench',yaw=0):
        return offer(zone,label,[member('Table','KH_RestTable',yaw=yaw),
                                member('South seat','KH_Bench',(0,0,-1.1),yaw),
                                member('North seat','KH_Bench' if seating=='bench' else 'KH_CampChair',(0,0,1.1),180+yaw)],
                     (x,z),((0,0),(.15,0),(-.15,0),(0,.2),(0,-.2)),
                     'Complete shared table with two supported seating faces and access from the retained aisle')

    # Twelve refectory tables, twenty-four benches: about 72 seated adults at
    # 0.60 m/seat.  This is furniture capacity, not a claim of NPC load/FPS.
    for x in (-13,-5,5,13):
        for z in (52.5,59.):table('Ground rear mess','Meal extension '+str((x,z)),x,z)
    offer('Ground rear Kitchen','Serving station',[member('Issue counter','KH_StallCounter'),
          member('Portioned tray stock','KH_GoodsFood',(0,1.056,0),180,(.65,.65,.65),'surface stock')],(-29,52),
          purpose='Issue line continues directly from preparation instead of a 9.5 m gap')
    offer('Ground rear Kitchen','Meal pickup front',[member('Pickup counter','KH_StallCounter'),
          member('Ready portions','KH_GoodsFood',(0,1.056,0),180,(.55,.55,.55),'surface stock')],(-25.4,52),
          purpose='A separate pickup end prevents customers entering the stove and wash workflow')
    offer('Ground rear Kitchen','Larder extension',[member('Counted dry rack','KH_UtilityRack')],(-29,60.8),
          purpose='Dry reserve stored next to produce and sacks, with delivery aisle retained')
    offer('Ground rear Kitchen','Returned clean containers',[member('Cases','KH_PalletCrates')],(-23.5,61.2),
          purpose='Separate clean container return beside the issue side')
    offer('Ground rear Kitchen','Portioning stool',[member('Worker seat','KH_Stool')],(-32.7,53.4),
          purpose='One worker position on the real preparation front')

    # Clinical four-bed wards, linen reserve and independent canvas privacy.
    for zone_id in ('Upper East Medical 01','Upper East Medical 02'):
        zone=zones[zone_id];c=sum(zone['bounds'][2:])/2
        for index,dz in enumerate((-3.1,3.1),1):
            offer(zone_id,'Recovery bay '+str(index),[member('Clinical cot','KH_MedicalCot')],(34.7,c+dz),
                  ((0,0),(-.1,0),(0,.15),(0,-.15)), 'Second row of actual clinical cots beside the protected central aisle')
        for index,dz in enumerate((-3.2,3.2),1):
            offer(zone_id,'Bedside privacy '+str(index),[member('Freestanding screen','KH_LivingScreen',yaw=90)],(31.65,c+dz),
                  ((0,0),(-.1,0)), 'Canvas separates treatment from the public aisle; passage around ends remains')
        offer(zone_id,'Clean linen reserve',[member('Supply rack','KH_UtilityRack',yaw=90)],(38.8,c-2.1),
              ((0,0),(.1,0)), 'Dense rear wall stores support the actual treatment bays')

    # Two extra berths and six new personal locker doors per back residence.
    for number in (1,2,3):
        zone_id='Upper West Residence %02d'%number;zone=zones[zone_id];c=sum(zone['bounds'][2:])/2
        offer(zone_id,'Rear sleeping bay',[member('Second row bunk','KH_BunkMakeshift'),
              member('Privacy canvas','KH_LivingScreen',(0,0,-1.05)),
              member('Personal floor kit','KH_ResidentBundle',(1.75,0,.1),13,(.45,.45,.45),'personal storage')],
              (-35.7,c+4.9),((0,0),(-.15,0)), 'An inhabited rear sleeping niche with its own possessions and screening')
        for sign in (-1,1):
            offer(zone_id,'Personal locker bank '+str(sign),[member('Three locker doors','KH_PersonalLocker',yaw=90)],
                  (-28.65,c+sign*1.7),((0,0),(-.08,0)), 'Per-resident storage between the actual doorway and existing front bunks')

    # Smaller workshop/store clusters use the real filled racks and a purposeful
    # category display, not generic loose rubble spread across floor squares.
    for zone_id in ('Ground West 01','Ground West 02','Ground West 03','Ground East 01','Ground East 02','Ground East 03'):
        zone=zones[zone_id];side=-1 if zone['bounds'][1]<0 else 1;c=sum(zone['bounds'][2:])/2
        for index,dz in enumerate((-2.6,2.6),1):
            offer(zone_id,'Rear reserve shelf '+str(index),[member('Counted stock rack','KH_UtilityRack',yaw=side*90)],
                  (side*38.8,c+dz),((0,0),(-side*.1,0),(0,side*.15)), 'Stock joins the existing rear shelf in a coherent wall line')
        goods='KH_GoodsFood' if zone_id=='Ground West 01' else 'KH_GoodsGear' if zone_id=='Ground West 02' else 'KH_GoodsTools'
        offer(zone_id,'Working stock island',[member('Sorting surface','KH_LowDisplay'),
              member('Specialist stock',goods,(0,.662,0),0,(.62,.62,.62),'surface stock')],
              (side*35.7,c+.6),((0,0),(0,-.5),(-side*.25,0)), 'Sorted goods at hand behind the trading face, away from doors')

    # Shared front room: two denser reading/tea islands and nearby reserve.
    for side in (-1,1):
        table('Upper front common room','Bay shared table '+str(side),side*8.5,-52)
        table('Upper front common room','Bay communal work '+str(side),side*9,-48.4,'chair')
        table('Upper front common room','Gallery shared table '+str(side),side*5.5,-39)
        offer('Upper front common room','Bay personal storage '+str(side),
              [member('Resident lockers','KH_PersonalLocker')],(side*11,-55.8),
              purpose='Belongings stay close to the people using the glazed common room')
        offer('Upper front common room','Neighbourhood information '+str(side),
              [member('Shared notes board','KH_CommunityBoard')],(side*6.8,-56.3),
              purpose='Freestanding notices and maps give the daylight bay a current shared use')
    for x in (-6,6):table('Upper rear community study','Group work '+str(x),x,55.5,'chair')
    for side in (-1,1):
        offer('Upper rear community study','Study reserve '+str(side),[member('Shared supply rack','KH_UtilityRack',yaw=side*90)],
              (side*9.1,55.5),purpose='Group-work material is held close to the shared tables')
    offer('Upper rear community study','Second information board',[member('Community board','KH_CommunityBoard')],(3.5,62.5),
          purpose='A paired rear information wall, without obstructing the central assembly route')

    for side,label in ((-1,'Mending and quiet work'),(1,'Laundry and household care')):
        zone_id='Upper rear '+label
        if side>0:
            offer(zone_id,'Second communal wash',[member('Washing point','KH_WashStation',yaw=180)],(31.5,35),
                  purpose='Adjacent bowls form a practical communal wash line rather than isolated props')
        else:
            offer(zone_id,'Textile cutting and sorting',[member('Cutting counter','KH_StallCounter'),
                  member('Sorted clothing','KH_GoodsGear',(0,1.056,0),0,(.65,.65,.65),'surface stock')],(-30,39),
                  purpose='Textile repair has a second working surface within four metres of the tool bench')
        for index,x in enumerate((25,28.6) if side>0 else (-26,-22),1):
            offer(zone_id,'Household stock bay '+str(index),[member('Linen and counted supply rack','KH_UtilityRack')],(x,40.5),
                  purpose='Sorted repair/linen reserves sit behind the compact workstations')

    # High-output local task pools.  Values are optional instance overrides on
    # KH_Floodlight only; its shared prefab defaults remain unchanged.
    def local_light(zone_id,label,position,yaw,backing,intensity=18.,radius=7.,existing_id=None):
        if existing_id:
            row=by_id[existing_id];before=deepcopy(row)
            row['lightIntensity']=intensity;row['lightRange']=radius
            moves.append(dict(id=existing_id,before=before,after=deepcopy(row)))
            light_fixtures.append(dict(id=existing_id,backing=backing,ownNativeSpot=True,intensity=intensity,range=radius,
                                      unchangedMount=True))
            return row
        row=dict(id='Hub8 density '+zone_id+' '+label,prop='KH_Floodlight',position=list(position),
                 rotation=[0,float(yaw),0],scale=[1,1,1],lightIntensity=intensity,lightRange=radius)
        manifest['settlementInstances'].append(row);by_id[row['id']]=row;additions.append(row['id']);roles[row['id']]='backed task fixture'
        light_fixtures.append(dict(id=row['id'],backing=backing,ownNativeSpot=True,intensity=intensity,range=radius))
        return row
    # Real outer-wall and column faces, within 2-4 m of the task clusters.
    local_light('Ground rear Kitchen','Cooking task pool',(-46.32,3.2,52),-90,
                'Existing native fixture on the original west outer wall',22.,8.,'Hub8 utility Kitchen working light')
    local_light('Ground rear Kitchen','Preparation task pool',(-36,3.1,55.65),0,
                'South face of existing concrete column X-36/Z56',22.,8.)
    local_light('Ground rear Kitchen','Plating task pool',(-24,3.1,55.65),0,
                'South face of existing concrete column X-24/Z56',22.,8.)
    for index,zone_id in enumerate(('Upper East Medical 01','Upper East Medical 02'),1):
        c=sum(zones[zone_id]['bounds'][2:])/2
        local_light(zone_id,'Clinical south task pool',(34,8.15,c-5.874),180,
                    'Existing north-facing bracket on the original south partition',18.,8.,
                    'Hub8 '+zone_id+' South wall task lamp')
        local_light(zone_id,'Clinical north task pool',(34,8.4,c+5.88),0,
                    'Existing south-facing bracket on the original north partition',18.,8.,
                    'Hub8 utility Clinical treatment light %02d'%index)
    # Cables follow partition faces or existing column heads above the routes.
    for zone_id,xc,z,y in [('Ground rear Kitchen',-30,55.65,4.0),
                            *[(name,34.5,sum(zones[name]['bounds'][2:])/2-5.88,9.4)
                              for name in ('Upper East Medical 01','Upper East Medical 02')]]:
        row=dict(id='Hub8 density '+zone_id+' Local task feeder',prop='KH_CableSag',position=[xc,y,z],
                 rotation=[0,0 if zone_id=='Ground rear Kitchen' else 180,0],scale=[1.2 if zone_id=='Ground rear Kitchen' else 1.,.10,.10])
        manifest['settlementInstances'].append(row);by_id[row['id']]=row;additions.append(row['id']);roles[row['id']]='overhead supply cable'
    for x in (-36,-24):
        row=dict(id='Hub8 density Ground rear Kitchen Column lamp lead '+str(x),prop='KH_CableSag',
                 position=[x,3.55,55.65],rotation=[0,0,90],scale=[.09,.05,.05])
        manifest['settlementInstances'].append(row);by_id[row['id']]=row;additions.append(row['id']);roles[row['id']]='column supply lead'
    for zone_id in ('Upper East Medical 01','Upper East Medical 02'):
        c=sum(zones[zone_id]['bounds'][2:])/2
        row=dict(id='Hub8 density '+zone_id+' South lamp lead',prop='KH_CableSag',
                 position=[34,8.725,c-5.88],rotation=[0,180,90],scale=[.115,.05,.05])
        manifest['settlementInstances'].append(row);by_id[row['id']]=row;additions.append(row['id']);roles[row['id']]='wall supply lead'

    # Recheck EVERY changed rigid body against every final current placement,
    # while recording any old/pre-existing intersections separately.
    changed=set(additions)|{row['id'] for row in moves};final_overlap=[];final_clearance=[]
    for identifier in changed:
        row=by_id[identifier]
        if not solid(row):continue
        zone_id=next((z for z in zones if identifier.startswith('Hub8 density '+z+' ') or identifier.startswith('Hub8 '+z+' ')),None)
        if zone_id:
            issue,box=problems(row,zones[zone_id])
            final_overlap.extend(dict(id=identifier,issue=i) for i in issue if i.startswith('Occupied'))
            final_clearance.extend(dict(id=identifier,issue=i) for i in issue if not i.startswith('Occupied'))
    # Existing layer rows are preserved in their original order, including IDs.
    for key in GROUPS:
        prior_ids=[p['id'] for p in original[key]]
        if [p['id'] for p in manifest[key]][:len(prior_ids)]!=prior_ids:
            raise AssertionError('Existing placement IDs/order changed: '+key)
    fixed={row['id']:row for rows in original.values() for row in rows if not row['id'].startswith('Hub8 ')}
    if any(by_id[identifier]!=row for identifier,row in fixed.items()):
        raise AssertionError('A pre-revision8 placement was modified')
    kitchens={name:by_id['Hub8 Ground rear Kitchen '+name]['position'] for name in
              ('Cooking line','Clean wash line','Preparation counter','Portioning counter')}
    sequence=list(kitchens);work_distances=[dict(a=a,b=b,metres=round(hypot(kitchens[a][0]-kitchens[b][0],kitchens[a][2]-kitchens[b][2]),3))
                                               for a,b in zip(sequence,sequence[1:])]
    managed=[deepcopy(by_id[i]) for i in sorted(changed)]
    overhead=[]
    for identifier in additions:
        if roles[identifier]!='overhead supply cable':continue
        row=by_id[identifier];floor=0 if row['position'][1]<5 else 5.5
        clearance=aabb(row,props[row['prop']])[1]-floor
        if clearance<2.7:raise AssertionError('New overhead feeder below safe clearance: '+identifier)
        overhead.append(dict(id=identifier,minimumSourceBoundsClearanceMetres=clearance))
    result=dict(revision=8,status='CANDIDATE_REQUIRES_ROOT_REVIEW',
                beforePlacements=sum(len(v) for v in original.values()),afterPlacements=sum(len(manifest[k]) for k in GROUPS),
                addedPlacementCount=len(additions),movedPlacementCount=len(moves),moves=moves,
                preservedEarlierPlacementCount=len(fixed),
                relocatedServiceCount=sum(row['before']['position']!=row['after']['position'] or row['before']['rotation']!=row['after']['rotation'] for row in moves),
                lightOverrideCount=len(light_fixtures),
                accepted=accepted,clusters=clusters,rejectedClusters=rejected,placementRoles=roles,
                protectedRouteCount=len(spatial['routes']),protectedSegmentsAndSamples=len(routes),
                minimumRouteRadiusMetres=.85,columnContract='Authored .65 m columns and preserved atrium columns',
                afterChangedBodyOverlaps=final_overlap,afterChangedBodyClearanceIssues=final_clearance,
                kitchenWorkingSequence=work_distances,taskFixtures=light_fixtures,
                overheadSourceBoundsClearance=overhead,
                cafeteriaTableCount=sum(p['prop']=='KH_RestTable' and (p['id'].startswith('Hub8 Ground rear mess ') or p['id'].startswith('Hub8 density Ground rear mess ')) for p in manifest['settlementInstances']),
                cafeteriaBenchCount=sum(p['prop']=='KH_Bench' and (p['id'].startswith('Hub8 Ground rear mess ') or p['id'].startswith('Hub8 density Ground rear mess ')) for p in manifest['settlementInstances']),
                managedState=managed,
                limits=['Candidate uses source FBX bounding boxes; convex/concave collider details are deliberately conservative',
                        'Chair seating capacity is a furniture estimate, not simulated NPC capacity/FPS',
                        'Existing stage-7 rows never move; pre-existing intersections require separate owner review',
                        'Actual imported geometry, fixture backing, final day/night renders and player/NavMesh routes still require review'])
    result['cafeteriaNominalSeats']=result['cafeteriaBenchCount']*3
    manifest['hub8Density']=result
    return result


if __name__=='__main__':
    from pathlib import Path
    import argparse,hashlib,json,sys
    project=Path(__file__).resolve().parents[2];work=project/'work/KursHub/audit8'
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,default=work/'authoring')
    parser.add_argument('--out',type=Path,default=work/'density_candidate')
    args=parser.parse_args();root,out=args.root.resolve(),args.out.resolve()
    assert root.is_relative_to(work.resolve()) and out.is_relative_to(work.resolve()) and not out.is_relative_to(root)
    path=root/'Assets/Environment/KursHub/KursHubManifest.json';pp=root/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json'
    source=path.read_bytes();source_props=pp.read_bytes();manifest=json.loads(source);props=json.loads(source_props)
    result=augment(manifest,props);twice=deepcopy(manifest);assert augment(twice,props)==result and twice==manifest
    assert source==path.read_bytes() and source_props==pp.read_bytes()
    out.mkdir(parents=True,exist_ok=True)
    (out/'KursHubManifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    result['inputSha256']=hashlib.sha256(source).hexdigest();result['propsInputSha256']=hashlib.sha256(source_props).hexdigest()
    (out/'density_review.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print('DENSITY_CANDIDATE',result['addedPlacementCount'],'added;',result['movedPlacementCount'],'moved;',
          len(result['afterChangedBodyOverlaps']),'body conflicts;',len(result['afterChangedBodyClearanceIssues']),'clearance issues;',
          result['cafeteriaTableCount'],'tables;',result['cafeteriaNominalSeats'],'nominal seats')
