"""Attach preserved material/collision chunk identity to the pre-bevel OBB proofs."""
import json,pathlib,collections
OUT=pathlib.Path(__file__).resolve().parent
path=OUT/'colliders.json'
data=json.loads(path.read_text(encoding='utf8'))
before=json.loads((OUT/'props_before.json').read_text(encoding='utf8'))
records={r['id']:r for r in data['props']}
total=0
for prop in before['props']:
    record=records[prop['id']]
    boxes={b['source']:b for b in record['boxes']}
    remaining=set(record['remainingMeshSources'])
    expected={o['name'] for o in prop['objects'] if o['collider']}
    assert set(boxes)|remaining==expected,(prop['id'],'Proof sources do not cover collider source objects')
    groups=collections.defaultdict(lambda:dict(boxes=[],remainingMeshSources=[]))
    for obj in prop['objects']:
        if not obj['collider']:continue
        material=obj['materialSlots'][0]
        candidates=[c for c in prop['chunks'] if c['collider'] and c['material']==material]
        assert len(candidates)==1,(prop['id'],material,candidates)
        name=candidates[0]['name']
        if obj['name'] in boxes:groups[name]['boxes'].append(boxes[obj['name']])
        else:groups[name]['remainingMeshSources'].append(obj['name'])
    record['chunks']=[dict(name=name,allBoxes=not g['remainingMeshSources'],remainingSolids=len(g['remainingMeshSources']),**g) for name,g in sorted(groups.items())]
    total+=len(record['chunks'])
    assert {r['name'] for r in record['chunks']}=={c['name'] for c in prop['chunks'] if c['collider']}
assert len(data['props'])==54 and all('chunks' in p for p in data['props'])
data['schema']='Explicit pre-bevel OBB proof per preserved collider FBX chunk; allBoxes true only when every original closed solid in that chunk is a proven rectangular cuboid.'
path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8')
print('COLLIDER_CHUNK_MAPPING_OK',len(data['props']),'props',total,'old colliding chunks')
