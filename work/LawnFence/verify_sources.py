import bpy,json
from pathlib import Path
r=Path(r'E:/git/Open-World-Zombie-Survival');checks=[]
for p in (r/'Sources/LawnFence').glob('*.blend'):
 bpy.ops.wm.open_mainfile(filepath=str(p))
 linked=[im for im in bpy.data.images if '/Textures/' in im.filepath.replace('\\','/')]
 assert linked
 for im in linked:assert Path(bpy.path.abspath(im.filepath)).is_file(),(p,im.filepath)
 checks.append({'source':p.name,'linkedMapsFound':len(linked)})
(r/'Verification/LawnFence/source_links.json').write_text(json.dumps(checks,indent=2))
print('ALL_SIX_BLEND_TEXTURE_LINKS_VALID')
