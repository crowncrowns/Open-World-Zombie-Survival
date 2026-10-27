import sys,json,hashlib,collections
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from catalog import ROWS,VERIFY
from models_01_10 import build as first
from models_11_20 import build as second
models=[]
for row in ROWS:
    g=(first if row['id']<=100 else second)(row)
    # No ID, names, parts, metadata, materials or colors contribute to this signature.
    geometry=[([tuple(round(v,6) for v in p) for p in m['v']],m['f']) for m in g.meshes]
    fingerprint=hashlib.sha256(repr(geometry).encode()).hexdigest()
    models.append(dict(id=row['id'],category=row['category'],geometrySHA256=fingerprint))
groups=collections.defaultdict(list)
for m in models:groups[m['geometrySHA256']].append(m['id'])
duplicates=[v for v in groups.values() if len(v)>1]
report=dict(passed=len(models)==200 and len(groups)==200,count=len(models),uniqueGeometries=len(groups),duplicates=duplicates,categories=dict(collections.Counter(m['category'] for m in models)),assets=models)
(VERIFY/'catalog_uniqueness.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('CATALOG_200_UNIQUE_GEOMETRY_'+('PASS' if report['passed'] else 'FAIL'),len(groups),flush=True)
