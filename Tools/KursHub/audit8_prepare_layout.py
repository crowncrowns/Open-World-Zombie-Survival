"""Stage the functional layout; never write an open Unity scene."""
from pathlib import Path
import json, sys
R=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
from hub8_layout import augment
W=R/'work/KursHub/audit8'
path=W/'authoring/Assets/Environment/KursHub/KursHubManifest.json'
manifest=json.loads(path.read_text(encoding='utf8'))
result=augment(manifest)
assert not result['conservativeEnvelopeConflicts'], result['conservativeEnvelopeConflicts']
assert all(z['inspectionPoints'] for z in result['zones'])
path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
(W/'functional_layout.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print('AUDIT8_LAYOUT_STAGED',len(result['zones']),'zones',len(result['routes']),'routes',len(result['cameras']),'views',result['addedPlacementCount'],'new placements; actual collider check pending')
