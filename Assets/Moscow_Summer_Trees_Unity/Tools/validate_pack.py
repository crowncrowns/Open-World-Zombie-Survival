from pathlib import Path
import json, trimesh, numpy as np
root=Path(__file__).resolve().parent.parent
manifest=json.loads((root/'TreeCatalog.json').read_text(encoding='utf8'))
errors=[]
expected={0:32,1:32,2:32}
for lod,num in expected.items():
 files=list((root/'Models'/f'LOD{lod}').glob('*.glb'))
 if len(files)!=num:errors.append(f'LOD{lod} has {len(files)}, expected {num}')
 for f in files:
  s=trimesh.load_scene(f)
  if not np.isfinite(s.bounds).all():errors.append(f'Invalid coordinates: {f.name}')
  if 'Wind_Trunk' not in s.graph.nodes:errors.append(f'Missing trunk pivot: {f.name}')
  if lod in (0,1) and not any(n.startswith('Wind_Branch_') for n in s.graph.nodes):errors.append(f'Missing branch wind nodes: {f.name}')
  if lod in (0,1) and not any('Leaves' in name for name in s.geometry):errors.append(f'Missing leaves: {f.name}')
  if lod == 2 and not any('Foliage_Cross' in name for name in s.graph.nodes):errors.append(f'Missing LOD2 foliage: {f.name}')
print('Models:',sum(expected.values()),'Errors:',len(errors))
for e in errors:print('ERROR:',e)
if errors:raise SystemExit(1)
