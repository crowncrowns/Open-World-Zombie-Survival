"""Check prop tags, actual FBX contact geometry and surviving floor support."""
from pathlib import Path
import json
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[2]
ASSET=ROOT/'work/PostApocalypseBalconies/UnityProject/Assets/Environment/PostApocalypse/Balconies'
VERIFY=ROOT/'Verification/PostApocalypseBalconies'
plans=json.loads((VERIFY/'prop_support_plan.json').read_text())['balconies']
report={'passed':True,'balconies':[],'errors':[],'lodChecks':0,'props':0}

def unity(ob,p):
    p=ob.matrix_world@p
    return Vector((p.x,p.z,p.y))

def height(tree,x,z,origin=2):
    p,n,_,_=tree.ray_cast(Vector((x,origin,z)),Vector((0,-1,0)),3)
    return p.y if p is not None and abs(n.y)>.7 else None

for plan in plans:
    path=next((ASSET/'Models/FBX/Balconies').glob('Balcony_'+plan['type']+'_*/*_'+plan['state']+'.fbx'))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    objects={o.name.split('.')[0]:o for o in bpy.context.scene.objects if o.type=='MESH'}
    col=objects['COLLISION'];vertices=[unity(col,v.co) for v in col.data.vertices]
    tree=BVHTree.FromPolygons(vertices,[list(p.vertices) for p in col.data.polygons])
    entry={'name':path.stem,'props':[],'passed':True}
    if not plan['props']:report['errors'].append(path.stem+': no supported decoration was retained')
    for prop in plan['props']:
        record={'name':prop['name'],'support':prop['support'],'lods':[]}
        for lod in range(4):
            ob=objects['LOD'+str(lod)];mesh=ob.data;mesh.calc_loop_triangles()
            attr=mesh.color_attributes.get('SupportLabel')
            if attr is None:raise RuntimeError(path.stem+': FBX lost SupportLabel colors')
            tags={}
            for loop in mesh.loops:
                color=attr.data[loop.index if attr.domain=='CORNER' else loop.vertex_index].color
                tags[loop.index]=sum((1<<i) for i in range(3) if color[i]>.5)
            tagged={mesh.loops[li].vertex_index for li,tag in tags.items() if tag==prop['label']}
            points=[unity(ob,mesh.vertices[i].co) for i in tagged]
            errors=[];contacts=0;lowest=None;coverage=0
            if not points:errors.append('prop geometry tag missing')
            else:
                lowest=min(p.y for p in points)
                if abs(lowest-prop['surface'])>.004:errors.append('base is not on the support plane')
                probes=[p for p in points if p.y<lowest+.0015]
                for tri in mesh.loop_triangles:
                    if any(tags[li]!=prop['label'] for li in tri.loops):continue
                    pts=[unity(ob,mesh.vertices[i].co) for i in tri.vertices]
                    if max(p.y for p in pts)>lowest+.0015:continue
                    for i in range(9):
                        for j in range(9-i):probes.append(pts[0]*(i/8)+pts[1]*(j/8)+pts[2]*(1-(i+j)/8))
                for p in probes:
                    y=height(tree,p.x,p.z,lowest+.02)
                    if y is not None and -.002<=p.y-y<=.004:contacts+=1
                if contacts==0:errors.append('no actual underside contact with exported floor/crossbeam')
                x0,x1,z0,z1=prop['box']
                if min(p.x for p in points)<x0-.005 or max(p.x for p in points)>x1+.005 or min(p.z for p in points)<z0-.005 or max(p.z for p in points)>z1+.005:
                    errors.append('geometry escaped its reserved footprint')
                if prop['support']=='surviving floor':
                    samples=[height(tree,x0+(x1-x0)*(i+.5)/7,z0+(z1-z0)*(j+.5)/7) for i in range(7) for j in range(7)]
                    coverage=sum(y is not None and abs(y-prop['surface'])<=.004 for y in samples)/49
                    if coverage<.88:errors.append('footprint is above a missing floor section')
                    for u in (.06,.5,.94):
                        for v in (.06,.5,.94):
                            y=height(tree,x0+(x1-x0)*u,z0+(z1-z0)*v)
                            if y is None or abs(y-prop['surface'])>.004:errors.append('corner or edge support is missing')
                else:coverage=None
            record['lods'].append({'lod':lod,'baseY':lowest,'actualContactSamples':contacts,'floorCoverage':coverage,'errors':sorted(set(errors))})
            report['lodChecks']+=1
            report['errors'].extend(path.stem+'/'+prop['name']+'/LOD'+str(lod)+': '+e for e in sorted(set(errors)))
        entry['props'].append(record);report['props']+=1
    entry['passed']=not any(e.startswith(path.stem+'/') or e.startswith(path.stem+':') for e in report['errors'])
    report['balconies'].append(entry)
    print('PROP_CONTACT',path.stem,'PASS' if entry['passed'] else 'FAIL',len(entry['props']),flush=True)
report['passed']=not report['errors'] and len(report['balconies'])==40
(VERIFY/'prop_support_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('PROP_SUPPORT_'+('PASS' if report['passed'] else 'FAIL'),len(report['balconies']),report['props'],report['lodChecks'],len(report['errors']),flush=True)
