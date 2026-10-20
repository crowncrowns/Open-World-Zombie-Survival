"""Reference-led settlement: dense layered stalls, housing, cloth and shared space.

Every item is a linked reusable prefab, with stable IDs and metre coordinates.
This is environmental presentation, without NPC, inventory or checkpoint logic.
"""
import math, random

def layout6():
    rng=random.Random(6162);items=[];lights=[]
    def put(id,prop,p,yaw=0,scale=(1,1,1)):
        items.append(dict(id=id,prop=prop,position=list(p),rotation=[0,yaw,0],scale=list(scale)))
    def light(id,p,intensity=14,range=6):
        lights.append(dict(id=id,position=list(p),color=[1,.56,.25],intensity=intensity,range=range))
    def local(p,offset,yaw):
        a=math.radians(yaw);x,y,z=offset
        return (p[0]+x*math.cos(a)+z*math.sin(a),p[1]+y,p[2]-x*math.sin(a)+z*math.cos(a))
    def stall(id,p,yaw,kind,index,lit=True):
        put(id,kind,p,yaw)
        goods=['KH_GoodsFood','KH_GoodsTools','KH_GoodsGear'][index%3]
        put(id+' Counter goods',goods,local(p,(0,1.055,-.75),yaw),yaw)
        put(id+' Upper stock','KH_GoodsFood' if index%2 else 'KH_GoodsGear',local(p,(0,1.56,.98),yaw),yaw, (.8,.8,.55))
        put(id+' Back crates','KH_PalletCrates',local(p,(-1.23,0,.82),yaw),yaw, (.6,.6,.65))
        sign=['KH_SignWeapons','KH_SignMedicalTrade','KH_SignGear','KH_SignRepair'][index%4]
        put(id+' Shop cloth sign',sign,local(p,(0,2.05,-1.49),yaw),yaw)
        put(id+' Rough rug',['KH_RugRed','KH_RugBlue','KH_RugWoven'][index%3],local(p,(.2,.004,-2.02),yaw),yaw+rng.uniform(-9,9))
        put(id+' Seller seat','KH_Stool',local(p,(.90,0,.05),yaw),yaw)
        if lit:light(id+' Lamp',local(p,(0,2.65,-.25),yaw),18,6.5)
        if lit:light(id+' Sign light',local(p,(0,2.7,-1.9),yaw),5,3.2)
    # Narrow the vast bare atrium with hand-built trading fronts. Space at the
    # foot of both stairs (Z>=8) remains open, as do the two public side lanes.
    for side,wing in ((-1,'West'),(1,'East')):
        yaw=side*90
        for i,z in enumerate((-30,-25.2,-20.4,-15.6,-10.8,-6, -1.2,3.6)):
            stall(f'{wing} atrium stall {i:02}',(side*7.5,0,z),yaw,
                  'KH_StallTimber' if (i+side)%2 else 'KH_StallPipe',i+(side>0))
        # Shops behind the market lend depth instead of a single thin facade.
        for i,z in enumerate((-25,-14,-3)):
            stall(f'{wing} outer stall {i:02}',(side*21,0,z),yaw,'KH_StallPipe' if i%2 else 'KH_StallTimber',i+2,False)
            put(f'{wing} outer lamp string {i:02}','KH_StringLights',(side*19.5,3.6,z),yaw)
            light(f'{wing} outer light {i:02}',(side*19.5,3.05,z),16,6)
        for i,z in enumerate((-30,-20,-9,2)):
            put(f'{wing} market floor wares {i:02}','KH_LowDisplay',(side*13.1,0,z),yaw)
            put(f'{wing} market floor debris {i:02}','KH_FloorDebris',(side*11.5,.003,z+1.8),rng.uniform(0,360))
        # Inhabited backs of the old permanent shop cells: main cell routes
        # at X=33 and the real front doorways are deliberately kept clear.
        for i,z in enumerate((-22,-6,10)):
            put(f'{wing} store back crates {i:02}','KH_PalletCrates',(side*37.7,0,z-3.7),yaw)
            put(f'{wing} store sacks {i:02}','KH_SupplySackStack',(side*37.5,0,z+3.4),yaw)
            put(f'{wing} store workbench {i:02}','KH_WorkbenchStocked',(side*35.5,0,z+4.6),180)
            put(f'{wing} store goods rug {i:02}','KH_RugWoven',(side*36,.004,z),rng.uniform(-10,10))
    # Festoon cables follow the fronts and their bulbs retain visible wire,
    # pendants and insulators; only selected pools use real point lights.
    for side,wing in ((-1,'West'),(1,'East')):
        for i,z in enumerate((-28,-22,-16,-10,-4,2)):
            put(f'{wing} market festoon {i:02}','KH_StringLights',(side*5.8,3.65,z),90)
    # Shared hearth and eating place. A continuous 3m lane remains along X=3.4.
    put('Common hearth','KH_FireBarrel',(0,0,-9));light('Common hearth glow',(0,1.15,-9),26,6)
    put('Common tea table','KH_RestTable',(-.9,0,-4),8)
    put('Common west bench','KH_Bench',(-2.2,0,-7),90)
    put('Common east bench','KH_Bench',(1.65,0,-6.1),-90)
    for i,(x,z,yaw) in enumerate(((-1.5,-10.6,-145),(1.6,-10.5,140),(-2.0,-2.6,0),(1.0,-2.4,0))):
        put(f'Common folding chair {i}','KH_CampChair',(x,0,z),yaw)
    for i,(x,z) in enumerate(((-1.8,-9.8),(1.1,-7.5),(-1.0,-1.8))):
        put(f'Common personal bag {i}','KH_ResidentBundle',(x,0,z),rng.uniform(-20,20),(.65,.65,.65))
    put('Common carpet red','KH_RugRed',(-.1,.006,-8.1),13,(1.6,1,1.7))
    put('Common carpet blue','KH_RugBlue',(-.7,.009,-3.5),-7,(1.4,1,1.4))
    put('Atrium hanging Kurs','KH_KursAtriumBanner',(0,4.15,-3.7))
    # Checkpoint: two real walk-through portals, inspection table, mesh fences,
    # bags, sandbags and a cloth header, rather than a generic floating board.
    for side,label in ((-1,'Left'),(1,'Right')):
        put(f'{label} checkpoint detector','KH_MetalDetector',(side*1.4,0,-48))
        put(f'{label} checkpoint fence','KH_SecurityFence',(side*4.3,0,-48.2))
        put(f'{label} checkpoint sacks','KH_SupplySackStack',(side*3.05,0,-49.25),side*18,(.8,.8,.8))
        put(f'{label} checkpoint stock','KH_PalletCrates',(side*5,0,-46.7),side*13,(.8,.8,.8))
        light(f'{label} checkpoint glow',(side*3,3.5,-49),14,7)
    put('Checkpoint inspection desk','KH_StallCounter',(0,0,-48.1),0,(.32,1,1))
    put('Checkpoint desk effects','KH_GoodsGear',(0,1.06,-48.1),0,(.45,.55,.7))
    put('Checkpoint cloth header','KH_CheckpointHeader',(0,2.8,-48.35))
    put('Checkpoint rules','KH_CheckpointRules',(4.35,2.55,-48.38))
    put('Checkpoint lighting cable','KH_StringLights',(0,4.7,-48.4),0,(1.7,1,1))
    put('Checkpoint floor runner','KH_RugWoven',(-1.4,.004,-48),90,(1.7,1,.8))
    # Upper floor: close sleeping fronts, cloth partitions, an untidy rear
    # alley of floor traders/belongings, then the existing permanent rooms.
    for side,wing in ((-1,'West'),(1,'East')):
        for i,z in enumerate((-27,-22.8,-18.6,-14.4,-10.2,-6,-1.8,2.4,6.6,10.8)):
            yaw=90 if side<0 else -90
            put(f'{wing} upper sleeping {i:02}','KH_BunkMakeshift' if i%3 else 'KH_Bedroll',(side*18.6,5.5,z),yaw)
            put(f'{wing} sleeping privacy {i:02}','KH_LivingScreen',(side*20.1,5.5,z),side*90)
            put(f'{wing} sleeping bags {i:02}','KH_ResidentBundle',(side*19,5.5,z+1.45),yaw)
            put(f'{wing} sleeping rug {i:02}',['KH_RugRed','KH_RugBlue','KH_RugWoven'][i%3],(side*17.6,5.507,z+.3),yaw+rng.uniform(-12,12),(.75,1,.70))
            if i%2==0:
                put(f'{wing} rear alley display {i:02}','KH_LowDisplay',(side*25.2,5.5,z+.7),side*90)
                put(f'{wing} rear alley rug {i:02}','KH_RugRed' if i%4 else 'KH_RugWoven',(side*23.9,5.51,z-.7),rng.uniform(-30,30))
            else:put(f'{wing} rear alley belongings {i:02}','KH_PalletCrates',(side*25.7,5.5,z),side*90,(.65,.65,.65))
            put(f'{wing} rear alley debris {i:02}','KH_FloorDebris',(side*22.1,5.502,z+.7),rng.uniform(0,360))
        for i,z in enumerate((-29,-25.8,-22.6,-19.4,-16.2,-13,-9.8,-6.6,-3.4,-.2,3,6.2,9.4,12.6)):
            put(f'{wing} rail cloth {i:02}','KH_RailDrapeBlue' if i%3 else 'KH_RailDrape',(side*15.10,5.56,z),side*90)
        for i,z in enumerate((-27,-21,-15,-9,-3,3,9)):
            put(f'{wing} upper festoon {i:02}','KH_StringLights',(side*17.2,8.5,z),90)
            if i%2==0:light(f'{wing} sleeping warm light {i:02}',(side*19,8.0,z),12,6)
        for i,z in enumerate((-25,-8,8)):
            put(f'{wing} upper plant {i:02}','KH_PottedPlant',(side*16.4,5.5,z),rng.uniform(0,360))
    for i,x in enumerate((-12,-8,-4,4,8,12)):
        put(f'Front gallery drape {i:02}','KH_RailDrapeBlue' if i%2 else 'KH_RailDrape',(x,5.56,-31.94))
    for i,x in enumerate((-5,0,5)):
        put(f'Rear gallery drape {i:02}','KH_RailDrapeBlue',(x,5.56,18.05),180)
    # Extra floor-level details do not occupy doorways or roof access.
    for side,wing in ((-1,'West'),(1,'East')):
        for i,z in enumerate((-35,-18,-1,17)):
            put(f'{wing} greenery {i:02}','KH_PottedPlant',(side*12.5,0,z),rng.uniform(0,360))
        for i,z in enumerate((-23,-8,9)):
            put(f'{wing} storage rugs {i:02}','KH_RugWoven',(side*42.5,.006,z),rng.uniform(-40,40))
    # Break the parade-like symmetry with small floor traders and loose stock;
    # the chosen public lane at X=3.4 stays clear.
    for item in items:
        if item['id'].startswith('Common '):item['position'][2]-=10
    for item in lights:
        if item['id'].startswith('Common '):item['position'][2]-=10
    for i,(x,z,yaw) in enumerate(((-2.4,-30.5,18),(-2.7,-24,-12),(-2.9,-8.0,14))):
        put(f'Aisle ground trader {i}','KH_LowDisplay',(x,0,z),yaw)
        put(f'Aisle mixed crates {i}','KH_PalletCrates',(x-.9,0,z+1.1),yaw,(.5,.65,.5))
        put(f'Aisle scattered rug {i}',['KH_RugWoven','KH_RugRed','KH_RugBlue'][i],(x+.3,.009,z),yaw-16,(1.2,1,1.2))
        put(f'Aisle litter {i}','KH_FloorDebris',(x+1.4,.003,z+.8),yaw+50)
    for i,(x,z) in enumerate(((-23,27),(-15,27),(0,27),(15,27),(23,27),(-26,42),(-13,42),(13,42),(26,42))):
        stall(f'Rear service arcade {i:02}',(x,0,z),rng.uniform(-5,5),'KH_StallTimber' if i%2 else 'KH_StallPipe',i,False)
        if i<5:light(f'Rear arcade light {i}',(x,3.2,z-1.6),12,6)
    for room_z in (-22,-6,10):
        for j,dz in enumerate((-3,2.8)):
            put(f'Back residence {room_z} bunk {j}','KH_BunkMakeshift',(-36.5,5.5,room_z+dz),0)
            put(f'Back residence {room_z} bundle {j}','KH_ResidentBundle',(-38.5,5.5,room_z+dz),90)
        put(f'Back residence {room_z} communal table','KH_RestTable',(-35.5,5.5,room_z),90)
        put(f'Back residence {room_z} carpet','KH_RugWoven',(-33,5.51,room_z),8)
    for room_z in (-16,0):
        for j,dz in enumerate((-3,3)):
            put(f'Medical room {room_z} bedding {j}','KH_Bedroll',(36.7,5.5,room_z+dz),0)
        put(f'Medical room {room_z} supplies','KH_WorkbenchStocked',(37.5,5.5,room_z),90)
        put(f'Medical room {room_z} carpet','KH_RugBlue',(33.7,5.51,room_z),-8)
    assert len(items)==len({p['id'] for p in items})
    return dict(settlementInstances=items,settlementLights=lights)

if __name__=='__main__':
    d=layout6();print(len(d['settlementInstances']),'placements;',len(d['settlementLights']),'shadowless light pools')
