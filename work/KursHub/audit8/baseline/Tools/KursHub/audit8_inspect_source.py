"""Read-only roof/space audit of the actual source, not the old generator."""
from pathlib import Path
import bpy,json
R=Path(__file__).resolve().parents[2];W=R/'work/KursHub/audit8'
bpy.ops.wm.open_mainfile(filepath=str(W/'baseline/Sources/KursHub/KursHub.blend'))
rows=[]
for o in bpy.data.objects:
 if o.type!='MESH' or not any(s in o.name.lower() for s in ('sky','lantern','roof','truss','glass')):continue
 points=[o.matrix_world@v.co for v in o.data.vertices]
 rows.append(dict(name=o.name,collections=[c.name for c in o.users_collection],materials=[m.name for m in o.data.materials],vertices=len(o.data.vertices),boundsMin=[min(v[i] for v in points) for i in (0,2,1)],boundsMax=[max(v[i] for v in points) for i in (0,2,1)]))
(W/'source_roof_audit.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8');print('AUDIT8_SOURCE_READ',len(rows))
