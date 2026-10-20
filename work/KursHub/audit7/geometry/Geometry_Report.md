# Kurs audit 7: props geometry handoff

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
| KH_Sandbag | 640 | 640 | 466 | 336 | 0 | 0 | FBX OK |
| KH_Barricade | 204 | 1260 | 756 | 378 | 11 | 0 | FBX OK |
| KH_WatchPost | 1748 | 5876 | 3522 | 1760 | 43 | 0 | FBX OK |
| KH_TarpWide | 1488 | 1488 | 892 | 446 | 0 | 0 | FBX OK |
| KH_TarpNarrow | 1488 | 1488 | 892 | 446 | 0 | 0 | FBX OK |
| KH_WindowBoards | 168 | 744 | 444 | 250 | 6 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_CorrugatedPatch | 1984 | 1984 | 1188 | 608 | 0 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_CableSag | 676 | 868 | 518 | 258 | 2 | 0 | FBX OK |
| KH_Floodlight | 96 | 1092 | 652 | 334 | 10 | 3 | FBX OK |
| KH_GrimePatch | 12 | 12 | — | — | 0 | 0 | FBX OK |
| KH_Railing | 84 | 756 | 452 | 226 | 7 | 0 | FBX OK |
| KH_Partition4 | 84 | 564 | 334 | 184 | 5 | 0 | FBX OK |
| KH_Shopfront6 | 792 | 1656 | 988 | 508 | 9 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_DoorPartition4 | 108 | 972 | 582 | 290 | 9 | 0 | FBX OK |
| KH_MeshPartition4 | 480 | 1056 | 632 | 316 | 6 | 0 | FBX OK |
| KH_DirectionBoard | 6136 | 6424 | 3850 | 1940 | 3 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_MedicalSign | 796 | 892 | 532 | 286 | 1 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_StallTimber | 5282 | 14130 | 8468 | 4788 | 77 | 76 | FBX OK; restored small chunks in LOD 2 |
| KH_StallPipe | 5394 | 14626 | 8766 | 5004 | 81 | 76 | FBX OK; restored small chunks in LOD 2 |
| KH_StallCounter | 1032 | 2672 | 1600 | 798 | 15 | 10 | FBX OK |
| KH_GoodsFood | 2784 | 5184 | 3102 | 1746 | 20 | 24 | FBX OK; restored small chunks in LOD 2 |
| KH_GoodsTools | 1156 | 1924 | 1144 | 588 | 8 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_GoodsGear | 1448 | 1448 | 978 | 746 | 0 | 0 | FBX OK; restored small chunks in LOD 1,2 |
| KH_PalletCrates | 996 | 9300 | 5574 | 3300 | 69 | 84 | FBX OK; restored small chunks in LOD 2 |
| KH_SupplySackStack | 896 | 896 | 530 | 286 | 0 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_LowDisplay | 1630 | 4030 | 2410 | 1322 | 20 | 24 | FBX OK; restored small chunks in LOD 2 |
| KH_WorkbenchStocked | 1386 | 8026 | 4808 | 2404 | 60 | 44 | FBX OK |
| KH_Stool | 72 | 852 | 510 | 254 | 6 | 9 | FBX OK |
| KH_Bench | 144 | 1496 | 892 | 444 | 12 | 10 | FBX OK |
| KH_BunkMakeshift | 4400 | 8240 | 4934 | 2772 | 40 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_Bedroll | 1928 | 2888 | 1730 | 1002 | 10 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_LivingScreen | 1836 | 2468 | 1474 | 816 | 4 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_RugRed | 2284 | 2284 | 1368 | 684 | 0 | 0 | FBX OK |
| KH_RugBlue | 2284 | 2284 | 1368 | 684 | 0 | 0 | FBX OK |
| KH_RugWoven | 2284 | 2284 | 1368 | 684 | 0 | 0 | FBX OK |
| KH_RailDrape | 2148 | 2272 | 1360 | 740 | 0 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_RailDrapeBlue | 2148 | 2272 | 1360 | 740 | 0 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_RestTable | 1648 | 2800 | 1672 | 940 | 12 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_FireBarrel | 2488 | 2488 | 1488 | 1086 | 0 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_CampChair | 924 | 1980 | 1184 | 590 | 11 | 0 | FBX OK |
| KH_ResidentBundle | 3408 | 3408 | 2040 | 1138 | 0 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_PottedPlant | 886 | 886 | 528 | 264 | 0 | 0 | FBX OK |
| KH_FloorDebris | 852 | 852 | 508 | 264 | 0 | 0 | FBX OK; restored small chunks in LOD 2 |
| KH_KursAtriumBanner | 28112 | 28112 | 16864 | 8428 | 0 | 0 | FBX OK |
| KH_SignWeapons | 4988 | 4988 | 2990 | 1492 | 0 | 0 | FBX OK |
| KH_SignMedicalTrade | 6260 | 6260 | 3752 | 1874 | 0 | 0 | FBX OK |
| KH_SignGear | 8154 | 8154 | 4886 | 2442 | 0 | 0 | FBX OK |
| KH_SignRepair | 9116 | 9116 | 5466 | 2730 | 0 | 0 | FBX OK |
| KH_CheckpointHeader | 8450 | 8450 | 5066 | 2530 | 0 | 0 | FBX OK |
| KH_CheckpointRules | 9776 | 9776 | 5862 | 2928 | 0 | 0 | FBX OK |
| KH_MetalDetector | 1876 | 3412 | 2042 | 1072 | 15 | 8 | FBX OK; restored small chunks in LOD 2 |
| KH_SecurityFence | 540 | 2076 | 1244 | 620 | 16 | 0 | FBX OK |
| KH_StringLights | 1584 | 3312 | 1986 | 992 | 18 | 0 | FBX OK |
| KH_WeaponsDisplay | 0 | 5772 | 3458 | 1724 | 33 | 101 | FBX OK |
