"""In-memory source preflight. This does not export or modify Unity assets."""
import sys, json, math, hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).resolve().parent))
from mathutils import Vector, Matrix, Euler
from models_01_10 import build


def box_overlap(a,b):
    lengths=[max(0,min(a['center'][i]+a['size'][i]/2,b['center'][i]+b['size'][i]/2)
                       -max(a['center'][i]-a['size'][i]/2,b['center'][i]-b['size'][i]/2)) for i in range(3)]
    return math.prod(lengths)


def inside(p,c,margin=.004):
    q=Vector(p)-Vector(c['center'])
    angles=c.get('rotation',[0,0,0])
    if any(abs(x)>1e-6 for x in angles):
        m=Euler(tuple(math.radians(x) for x in angles),'XYZ').to_matrix()
        q=m.inverted()@q
    return all(abs(q[i])<c['size'][i]/2-margin for i in range(3))


def sweep(g,name):
    p=g.parts[name];axis=Vector(p['axis']);axis.normalize();pivot=Vector(p['pivot'])
    vertices=[Vector(v) for m in g.meshes if m['part']==name for v in m['v']]
    # Interior points of the static body boards only. Existing at-rest padded joinery
    # contact is excluded; reporting is a conservative modelling aid, not physics proof.
    boxes=[c for c in g.colliders if c['part']=='Body' and 'center'in c and 'size'in c]
    baseline={(j,k) for j,v in enumerate(vertices) for k,c in enumerate(boxes) if inside(v,c)}
    penetrations=[]
    for step in range(1,21):
        t=step/20
        rotation=Matrix.Rotation(math.radians(p['amount']*t),3,axis)
        count=0;hitboxes={};example=[]
        for j,v in enumerate(vertices):
            if p['kind'] in ('hinge','rotate'):q=pivot+rotation@(v-pivot)
            else:q=v+axis*(p['amount']*t)
            for k,c in enumerate(boxes):
                if (j,k) not in baseline and inside(q,c):
                    count+=1;hitboxes[k]=hitboxes.get(k,0)+1
                    if len(example)<3:example.append({'initial':list(v),'moved':list(q)})
        if count:penetrations.append({'fraction':t,'new_vertices_inside_body_boxes':count,
                                     'body_boxes':[dict(center=boxes[k]['center'],size=boxes[k]['size'],vertices=n)for k,n in hitboxes.items()]})
        if count:penetrations[-1]['examples']=example
    return penetrations


def floor_sweep(g,name):
    p=g.parts[name];axis=Vector(p['axis']);axis.normalize();pivot=Vector(p['pivot'])
    vertices=[Vector(v)for m in g.meshes if m['part']==name for v in m['v']]
    minimum=1e9;worst_fraction=0
    for step in range(41):
        t=step/40;rotation=Matrix.Rotation(math.radians(p['amount']*t),3,axis)
        parent_matrix=Matrix.Identity(4)
        if g.row['id']==19:
            prerequisites={'BedExtension':{},'BedFootExtension':{'BedExtension':1},'BedRaise':{'BedExtension':1,'BedFootExtension':1},'BedFootRaise':{'BedExtension':1,'BedFootExtension':1},'BedFootLegs':{'BedExtension':1,'BedFootExtension':1,'BedRaise':1,'BedFootRaise':1}}
            poses=prerequisites.get(name,{})
            def world(part):
                record=g.parts[part];progress=poses.get(part,0);origin=Vector(record['pivot']);direction=Vector(record['axis']).normalized()
                own=Matrix.Translation(origin)@Matrix.Rotation(math.radians(record['amount']*progress),4,direction)@Matrix.Translation(-origin) if record['kind'] in ['hinge','rotate'] else Matrix.Translation(direction*record['amount']*progress)
                return (world(record['parent']) if record.get('parent') else Matrix.Identity(4))@own
            if p.get('parent'):parent_matrix=world(p['parent'])
        for v in vertices:
            q=pivot+rotation@(v-pivot) if p['kind']in('hinge','rotate')else v+axis*(p['amount']*t)
            q=parent_matrix@q
            if q.y<minimum:minimum=q.y;worst_fraction=t
    return {'minimum_y':minimum,'fraction':worst_fraction,'context':'parent/prerequisite pose' if g.row['id']==19 else 'standalone motion'}


report={'kind':'in_memory_recipe_preflight','models':[],'errors':[],'source_sweep_warnings':[],
        'floor_sweep_samples':41,'critical_body_sweep_samples':20,
        'limits':'Source meshes and conservative primitive-box tests; not Unity physics or visual acceptance.'}
for asset_id in range(1,101):
    row={'id':asset_id,'variant':(asset_id-1)%10,'category':str((asset_id-1)//10+1),'name':str(asset_id),'slug':str(asset_id)}
    try:
        g=build(row)
        allv=[v for m in g.meshes for v in m['v']]
        raw_triangles=sum(max(0,len(f)-2) for m in g.meshes for f in m['f'])
        triangles=0
        for mesh in g.meshes:
            for face in mesh['f']:
                points=[Vector(mesh['v'][i]) for i in face]
                triangles+=sum((points[k]-points[0]).cross(points[k+1]-points[0]).length*.5>1e-11 for k in range(1,len(points)-1))
        finite=all(math.isfinite(q) for v in allv for q in v)
        minimum=[min(v[i] for v in allv) for i in range(3)]
        maximum=[max(v[i] for v in allv) for i in range(3)]
        zone_crossings=[]
        for z in g.zones:
            for c in g.colliders:
                if c['part'] not in ('Body',z['part']) or 'size'not in c or any(c.get('rotation',[0,0,0])):continue
                ratio=box_overlap(z,c)/max(1e-9,math.prod(z['size']))
                if ratio>.015:
                    zone_crossings.append({'zone':z['name'],'part':z['part'],'fraction':round(ratio,4),'body_center':c['center'],'body_size':c['size']})
        entry={'id':asset_id,'triangles':triangles,'rawFaceFanTrianglesBeforePoleCleanup':raw_triangles,'mesh_primitives':len(g.meshes),'bounds_min':minimum,'bounds_max':maximum,
               'moving_parts':len([p for p in g.parts.values() if p['kind']not in('static','damage')]),
               'placing_zones':len(g.zones),'colliders':len(g.colliders),'finite':finite,'zone_solid_crossings':zone_crossings,
               'geometry_hash':hashlib.sha256(repr([(m['v'],m['f'],m['part']) for m in g.meshes]).encode()).hexdigest()}
        if triangles>18000:report['errors'].append({'id':asset_id,'reason':'LOD0 triangle limit','triangles':triangles})
        if not finite:report['errors'].append({'id':asset_id,'reason':'Non finite vertices'})
        if zone_crossings:report['errors'].append({'id':asset_id,'reason':'Placement zone crosses board','count':len(zone_crossings)})
        entry['moving_part_floor_sweeps']={}
        for name,p in g.parts.items():
            if p['kind'] not in ('hinge','slide','rotate','lift'):continue
            floor=floor_sweep(g,name)
            entry['moving_part_floor_sweeps'][name]=floor
            if floor['minimum_y']<-.005:
                report['errors'].append({'id':asset_id,'part':name,'reason':'Moving part below floor','sample':floor})
            if any(token in name for token in ('Fold','DropLeaf','GateLeg','LinenSeat','BedExtension','SlidingTop','ExtensionLeaf','KeyboardTray')):
                hits=sweep(g,name)
                if hits:report['source_sweep_warnings'].append({'id':asset_id,'part':name,'hits':hits})
        report['models'].append(entry)
        print('PREFLIGHT_MODEL',asset_id,triangles,'parts',entry['moving_parts'],'zones',len(g.zones),'zoneCross',len(zone_crossings),flush=True)
    except Exception as error:
        report['errors'].append({'id':asset_id,'reason':repr(error)})
        import traceback;traceback.print_exc()
report['summary']={'count':len(report['models']),'unique_geometry':len({m['geometry_hash']for m in report['models']}),
                   'max_triangles':max(m['triangles']for m in report['models']),'error_count':len(report['errors']),
                   'sweep_warning_count':len(report['source_sweep_warnings'])}
dest=ROOT/'Verification'/'PostApocalypticFurniture'/'preflight_01_10.json';dest.parent.mkdir(parents=True,exist_ok=True)
dest.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('PREFLIGHT_SUMMARY',report['summary'],flush=True)
