import bpy,bmesh,json,sys
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import *
bpy.ops.wm.open_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
manifest=json.loads((ASSET/'manifest.json').read_text(encoding='utf-8'));removed=[]
for f in manifest['fixtures']:
    if f['shape'] not in ['flood','adjustable','oldflood']:continue
    col=bpy.data.collections[f['name']];changed=False
    for o in col.objects:
        if not o.name.startswith('LOD') or '_Housing' not in o.name:continue
        bm=bmesh.new();bm.from_mesh(o.data);todo=set(bm.faces);discard=[]
        while todo:
            seed=todo.pop();group={seed};pending=[seed]
            while pending:
                face=pending.pop()
                for e in face.edges:
                    for other in e.link_faces:
                        if other in todo:todo.remove(other);group.add(other);pending.append(other)
            vs={v for p in group for v in p.verts};lo=Vector([min(v.co[k] for v in vs) for k in range(3)]);hi=Vector([max(v.co[k] for v in vs) for k in range(3)]);center=(lo+hi)/2;size=hi-lo
            if abs(abs(center.x)-f['width']*.36)<.007 and abs(center.y+.125)<.008 and abs(center.z-.02)<.008 and size.x<.036 and .06<size.y<.115 and f['height']*.55<size.z<f['height']*.82:
                discard.extend(group)
        if discard:
            bmesh.ops.delete(bm,geom=discard,context='FACES');loose=[v for v in bm.verts if not v.link_faces]
            if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
            bm.to_mesh(o.data);o.data.update();changed=True;removed.append(dict(asset=f['name'],mesh=o.name,removedFaces=len(discard)))
        bm.free()
    if changed:
        objects=[o for o in col.objects if o.name.startswith('LOD')];bpy.ops.object.select_all(action='DESELECT')
        for o in objects:o.hide_set(False);o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(ASSET/'Models'/f['category']/(f['name']+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False)
        for o in objects:o.hide_set(True)
        f['triangles']=[sum(len(o.data.polygons) for o in objects if o.name.startswith('LOD'+str(lod)+'_')) for lod in range(3)]
bpy.ops.wm.save_as_mainfile(filepath=str(ASSET/'Source~'/'LightingPack_Master.blend'))
(ASSET/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(VERIFY/'ProfessionalAudit/reflector_cleanup.json').write_text(json.dumps(dict(passed=True,repairs=removed),indent=2),encoding='utf-8')
print('REFLECTOR_INTERSECTION_CLEANUP_PASS',len(removed),flush=True)
