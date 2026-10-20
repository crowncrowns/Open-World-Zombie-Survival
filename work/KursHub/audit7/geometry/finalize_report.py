import pathlib,json,hashlib
OUT=pathlib.Path(__file__).resolve().parent
PACKAGE=OUT/'output'
load=lambda path:json.loads(path.read_text(encoding='utf8'))
manifest=load(PACKAGE/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json')
before=load(OUT/'props_before.json');after=load(OUT/'props_after.json')
roundtrip=load(OUT/'fbx_roundtrip.json');source=load(OUT/'source_roundtrip.json')
repairs=load(OUT/'lod_export_repairs.json')
assert roundtrip['status']=='AUDIT7_PROP_ROUNDTRIP_OK' and not roundtrip['errors']
assert source['status']=='AUDIT7_PROPS_SOURCE_REOPEN_OK'
summary=load(OUT/'geometry_summary.json')
summary.update(status='AUDIT7_PROPS_AND_ALL_FBX_VERIFIED',roundtripFBX=roundtrip['exports'],
    fbxChunksPerLOD0=sum(len(p['chunks']) for p in manifest['props']),
    lodTriangles={str(i):sum(l['triangles'] for p in manifest['props'] for l in p['lods'] if l['level']==i) for i in range(3)},
    repairedLODExports=len(repairs['fixes']),repairedChunks=sum(len(r['restoredChunks']) for r in repairs['fixes']),
    sourceReopened=True,all54LOD0PreservedDuringLODRepair=True,
    mainAssetsModified=False,unit='metres',uvLayers=['UVMap'],guardedRegenerationVerified=True,
    limitation='Geometry/source/FBX validation only. Unity import, UV2, colliders, rendered material quality and game Play acceptance require separate target-engine checks.')
(OUT/'geometry_summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
colliders=load(OUT/'colliders.json')
if not any(p['id']=='KH_WeaponsDisplay' for p in colliders['props']):
    colliders['props'].append(dict(id='KH_WeaponsDisplay',boxes=[],remainingMeshSources=[],chunks=[],policy='Static countertop decoration; rack is supported by colliding counter, detailed guns carry no gameplay collider.'))
(OUT/'colliders.json').write_text(json.dumps(colliders,ensure_ascii=False,indent=2),encoding='utf8')
hashes={str(p.relative_to(PACKAGE)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in PACKAGE.rglob('*') if p.is_file()}
(OUT/'output_hashes.json').write_text(json.dumps(dict(status='AUDIT7_GEOMETRY_OUTPUT_HASHES',files=hashes),ensure_ascii=False,indent=2),encoding='utf8')
old={p['id']:p for p in before['props']};new={p['id']:p for p in after['props']}
rows=[]
for prop in manifest['props']:
    levels={l['level']:l['triangles'] for l in prop['lods']}
    records=new[prop['id']]['objects']
    bevels=sum(any(m['type']=='BEVEL' for m in o['modifiers']) for o in records)
    added=sum(o['added'] for o in records)
    fixed=[str(r['level']) for r in repairs['fixes'] if r['id']==prop['id']]
    rows.append('| '+ ' | '.join((prop['id'],str(old.get(prop['id'],{}).get('triangles',0)),str(levels[0]),str(levels.get(1,'—')),str(levels.get(2,'—')),str(bevels),str(added),'FBX OK'+('; restored small chunks in LOD '+','.join(fixed) if fixed else ''))) +' |')
text='''# Kurs audit 7: props geometry handoff

Status: **all staged source and 160 FBX files reopened and verified**. This report covers the geometry subtask, not the complete artistic/Unity acceptance audit.

## Deliverable locations

- `output/Sources/KursHub/KursHubProps.blend`: editable 54 native collections; physical bevel and weighted-normal modifiers retained.
- `output/Assets/Environment/KursHub/Props/Models`: 54 real binary FBX LOD0 files.
- `output/Assets/Environment/KursHub/Props/Models/LOD`: 106 real binary LOD1/LOD2 files.
- `output/Assets/Environment/KursHub/Props/KursHubPropsManifest.json`: current names, materials, origins, bounds, exact final triangle counts and LOD file paths.
- `colliders.json`: analytical cuboid proofs from the original 53 prototypes plus an explicit decorative-only policy for the new weapons display. This is collider authoring data; Unity collider implementation is outside this subtask.

## Preserved and improved

All 1,535 original source objects and all previous material/collision FBX chunk names are preserved. There are 469 added physical fittings/rack/weapon objects, totaling 2,004 solids across 54 props. The existing 53 identifiers are retained; `KH_WeaponsDisplay` is the only new identifier. 639 objects have real rounded edges, 416 wood parts have grain-oriented UVs. Flat privacy-screen/drape repair cloth now follows the real fabric surface. Added wood joints, nails, bolts, stool rungs and detector vents remain editable objects.

LOD totals are **219,190 / 131,480 / 69,482** triangles. Each emitted LOD decreases triangles relative to the preceding level. `KH_GrimePatch` contains only 12 triangles and intentionally has one level. LOD0 geometry was never substituted during repair.

## Real export defect found and fixed

The preceding exporter finished writing all models but its reverse import failed. Aggressive reduction of tiny disconnected solids could pass the export-side aggregate volume test yet become open or inverted after actual FBX conversion. A full collecting audit found 24 affected files and 38 chunks. Only failed chunks were restored from the preceding safe LOD; other reductions and every chunk identifier were retained. Original rejected files are kept under `rejected_fbx`, and `lod_export_repairs.json` records each replacement and before/after hash. Do not ship rejected files.

After repair, all 160 FBX files were imported again using Blender 4.5.14. The audit checks the explicit empty root, chunk names/parenting, unit positive transforms, UV presence and finite values, material slot, triangle counts, LOD0 bounds, manifold edges, degenerate faces, face normals, aggregate volume and each connected solid's volume. All passed. Component volumes use double precision relative to a local reference, avoiding cancellation on tiny fasteners.

`source_roundtrip.json` records an independent reopen of the saved `.blend`, including evaluated modifiers, closed positive solids, original object retention and native collection placement origins. `output_hashes.json` binds the final staged assets.

## Scope limits and integration

No main-project Assets, prefabs, scenes or metadata were changed by this subtask. These checks do not prove Unity compatibility, collider passage, UV2 quality, LOD transitions, frame rate, material realism or reference matching. The source currently has UVMap only; Unity lightmap UV generation must be checked in the integration stage. PBR maps are being integrated separately.

`Tools/KursHub/audit7_geometry.py` now invokes reusable `audit7_verify_props.py` after export, applies `audit7_repair_lods.py` only if reduced levels fail, and verifies every FBX again. Failed LOD0 or still-invalid output remains fatal. Verify-only never repairs asset files. The guarded read-only path passed all 160 exports, and a separate real three-FBX fixture reproduced one failed LOD2, repaired it, and then passed all three imports (`guarded_workflow_test.json`). Source audit input should be the archived stage-6 baseline, not the already corrected stage-7 source. Tested final outputs were not regenerated while installing the guard.

## Per-prototype geometry counts

| Prop | Before tris | LOD0 | LOD1 | LOD2 | Beveled solids | Added solids | Final state |
|---|---:|---:|---:|---:|---:|---:|---|
'''+ '\n'.join(rows)+'\n'
(OUT/'Geometry_Report.md').write_text(text,encoding='utf8')
print(summary)
