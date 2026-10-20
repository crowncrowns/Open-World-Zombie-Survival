"""Reference correction: checkpoint opens onto atrium, warm lighting stays legible."""
from pathlib import Path
import json
R=Path(__file__).resolve().parents[2];p=R/'work/KursHub/audit7/authoring/Assets/Environment/KursHub/KursHubManifest.json'
d=json.loads(p.read_text(encoding='utf8'))
assert not d.get('qualityAtmosphere'),'Already applied'
for item in d['settlementInstances']:
    if 'checkpoint' in item['id'].lower():item['position'][2]+=13.5
for item in d['settlementLights']:
    if 'checkpoint' in item['id'].lower():item['position'][2]+=13.5
    item['color']=[1,.72,.46]
    item['intensity']*=.50 if 'Sign light' not in item['id'] else .35
d['qualityAtmosphere']='Checkpoint Z=-34.5 at atrium threshold; warmer neutral lamp color and lower direct intensity to retain material texture'
p.write_text(json.dumps(d,indent=2),encoding='utf8')
print('AUDIT7_ATMOSPHERE_OK: existing checkpoint/light IDs retained; geometry unchanged')
