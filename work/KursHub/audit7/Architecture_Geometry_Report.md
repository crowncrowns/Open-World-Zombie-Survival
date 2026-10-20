# Kurs audit 7: architectural geometry verification

**Final geometry check passed**, after repairing the text meshes. This is the geometry result; Unity import, rendered appearance and player traversal are checked separately.

The three final staged Shell FBX files were imported back into Blender 4.5.14. LOD0 contains 389 chunks / 350,372 triangles, LOD1 contains 389 chunks / 198,244 triangles, and LOD2 contains 299 chunks / 108,188 triangles. All 299 previous chunk names are retained at every level. Added detail chunks disappear at LOD2; the complete original shell, floors and portals remain.

Every imported mesh has assigned material, finite UVs, unit positive transform, closed manifold topology, valid faces/normals and positive volume for each connected solid. There are no font or other topology exceptions in the final check. The original baseline was also imported to distinguish previous defects from added work.

The native source was independently reopened and all **4,646 evaluated meshes** checked, including the editable bevel modifiers: 3,480 original objects retained plus 1,166 additions. Every source mesh passes the same geometry/UV/material/transform checks. The source evaluates to 350,372 triangles, matching the real LOD0 export. The combined GroundBody/GroundRoundBay footprint remains exactly 93 × 130 m; the full FBX bounds also include the approach and protruding architectural details.

The new architecture includes physical edge radii on 1,211 original meshes, complete recess/reveal/sill/cap details on 94 window bays, folded roof flashing, drainage and entrance hardware. These counts come from the author's architecture report and are corroborated by the actual reopened source/export inventory.

## Text defect fixed

The baseline contained 41 text meshes with disconnected cap and side vertices; these produced defects in 11 merged FBX chunks. Merely preserving this previous defect was insufficient. A 0.01 mm merge threshold closes the coincident seams despite float precision at the building's large coordinates. An additional 37 local cap retriangulations remove zero-area triangles without moving the letter outline or filling its holes. All text objects and FBX names are preserved.

`Tools/KursHub/audit7_text_geometry.py` contains the repair integrated by the main architectural pass. The isolated probe first proved the method on every affected source object; the final source and all three actual exports then passed strict checks without a legacy exception.

## Evidence

- `architecture_roundtrip.json`: current final hashes, all imported chunks, all 4,646 evaluated source meshes, dimensions and results.
- `architecture_roundtrip.log`: actual Blender imports and final success marker.
- `architecture_text_fixes.json`: applied text repairs from the architecture exporter.
- `text_repair_probe.json`: independent repair experiment on the original text meshes.
- `geometry/verify_architecture.py`: reproducible read-only verification; final assets have no topology exception.

The earlier `architecture_roundtrip_before_text_repair.json` is explicitly a before-repair diagnostic and is not evidence of final acceptance.
