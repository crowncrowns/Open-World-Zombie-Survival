from pathlib import Path
import bpy,json,os
r=Path.cwd();script=r/'Tools/PostApocalypseBalconies/build.py';prefix=script.read_text(encoding='utf-8').split('TYPES=[')[0]
g={'__file__':str(script),'__name__':'balcony_functions'};exec(compile(prefix,str(script),'exec'),g)
asset=g['OUT'];blend=asset/'Models/Source~/Balconies_Master.blend';bpy.ops.wm.open_mainfile(filepath=str(blend))
g['MATS']={name:bpy.data.materials[name] for name in g['MAT_NAMES'] if bpy.data.materials.get(name) is not None};generation=json.loads((g['VER']/'generation.json').read_text())
for idx,state in enumerate('ABCD'):
 name='Balcony_08_Overgrown_'+state;collection=bpy.data.collections[name];location=collection.objects[0].location.copy()
 for ob in list(collection.objects):bpy.data.objects.remove(ob,do_unlink=True)
 seed=2401024+800+idx*17
 record=g['build_model'](name,'Balconies',lambda level,sd:g['add_balcony_geometry'](8,state,level,sd),lambda sd,depth:g['make_collision'](8,state,sd,depth),seed,asset/'Models/FBX/Balconies/Balcony_08_Overgrown')
 for j,ob in enumerate(g['SOURCE_MODELS'][-1][2]):
  for previous in list(ob.users_collection):previous.objects.unlink(ob)
  collection.objects.link(ob);ob.location=location;ob.hide_render=j!=0;ob.hide_set(j!=0)
 old=next(x for x in generation['balconies'] if x['name']==name);old.update(record)
(g['VER']/'generation.json').write_text(json.dumps(generation,indent=2),encoding='utf-8')
plan=json.loads((g['VER']/'prop_support_plan.json').read_text())
for new in g['PROP_SUPPORT']:
 old=next(x for x in plan['balconies'] if x['type']==new['type'] and x['state']==new['state']);old.update(new)
text=json.dumps(plan,indent=2);(g['VER']/'prop_support_plan.json').write_text(text,encoding='utf-8');(asset/'Editor/BalconyPropSupport.json').write_text(text,encoding='utf-8')
temp=blend.with_name('Balconies_Master_vines.blend');bpy.ops.wm.save_as_mainfile(filepath=str(temp));os.replace(temp,blend)
print('VINES_ATTACHED_4_MODELS',flush=True)
