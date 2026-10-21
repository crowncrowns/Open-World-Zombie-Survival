"""Pack revision-8 PBR sources and controlled reusable collection instances.

Blender only.  Intended to run after the separate sign, soft-goods, roof,
surface, layout and utility passes have finished in audit8/authoring.  Existing
shell objects, archive objects and native instance objects remain present.
Only controlled prop-library collections are replaced from KursHubProps.blend.
No FBX export, Unity asset, main project source, or Unity scene is written here.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import os
import re
import sys
import uuid

import bpy
from mathutils import Quaternion, Vector

PROJECT = Path(__file__).resolve().parents[2]
WORK = PROJECT / "work/KursHub/audit8"
AUTHORING = WORK / "authoring"
HUB = Path("Assets/Environment/KursHub")
sys.path.insert(0, str(Path(__file__).parent))
from audit7_materials import apply_materials, MATERIALS


def _sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _native_name(identifier):
    # Blender names are limited to 63 bytes. The full stable Unity ID lives in
    # a custom property; a deterministic suffix prevents silent truncation.
    if len(identifier.encode('utf8')) <= 63:
        return identifier
    prefix = identifier[:48]
    while len(prefix.encode('utf8')) > 48:
        prefix = prefix[:-1]
    return prefix + '_' + hashlib.sha256(identifier.encode('utf8')).hexdigest()[:10]


def _write(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2)+"\n", encoding="utf8")


def _save_atomic(path):
    """Write a separate complete native file, then replace only that file.

    This avoids Blender's Windows rename-to-existing-.blend1 path.  Prior
    revision7 sources remain in baseline; no existing .blend1 is overwritten.
    """
    path = Path(path)
    temporary = path.with_name(path.stem+".audit8-"+uuid.uuid4().hex+".tmp.blend")
    versions = bpy.context.preferences.filepaths.save_version
    try:
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(temporary), check_existing=False, relative_remap=False)
        if not temporary.is_file() or temporary.stat().st_size < 1024:
            raise AssertionError("Blender did not write a complete temporary native source")
        os.replace(temporary, path)
    finally:
        bpy.context.preferences.filepaths.save_version = versions


def _verify_native_poses(manifest, props):
    from audit8_utilities import transform
    bpy.context.view_layer.update()
    count, maximum_error = 0, 0.
    for key in ("propInstances", "interiorInstances", "settlementInstances"):
        for placement in manifest[key]:
            obj = bpy.data.objects.get(_native_name(placement["id"]))
            if obj is None or obj.instance_type != 'COLLECTION' or obj.instance_collection.name != placement["prop"]:
                raise AssertionError("Native instance/collection mismatch: "+placement["id"])
            if obj.parent is not None:
                raise AssertionError("Unexpected object parent on source placement: "+placement["id"])
            for point in ((0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1)):
                # Hidden collections do not enter Blender's dependency graph;
                # their cached matrix_world can stay stale after a file load.
                # Unparented placements are authored by their serialized basis.
                actual = obj.matrix_basis @ Vector((point[0], point[2], point[1]))
                expected = transform(point, placement)
                error = max(abs(actual[i]-expected[j]) for i, j in ((0, 0), (1, 2), (2, 1)))
                maximum_error = max(maximum_error, error)
                if error > .000025:
                    raise AssertionError((placement["id"], "Native Euler/scale mismatch", error))
            count += 1
    for prop in props["props"]:
        collection = bpy.data.collections.get(prop["id"])
        expected = prop["sourceOffset"]
        if collection is None or max(abs(collection.instance_offset[i]-expected[j]) for i, j in ((0,0),(1,2),(2,1))) > .000025:
            raise AssertionError("Native library instance offset mismatch: "+prop["id"])
    return dict(instances=count, propCollections=len(props["props"]),
                maximumTransformErrorMetres=maximum_error, basisSamplesPerInstance=4,
                verification="Serialized unparented matrix_basis, including hidden source collections")


def _guard(root, work):
    root, work = Path(root).resolve(), Path(work).resolve()
    staging = WORK.resolve()
    if root == PROJECT or not root.is_relative_to(staging) or not work.is_relative_to(staging):
        raise ValueError("Revision-8 integration accepts only its audit8 staging paths")
    for relative in (HUB/"KursHubManifest.json", HUB/"Props/KursHubPropsManifest.json",
                     HUB/"Textures/Audit7PBRManifest.json", Path("Sources/KursHub/KursHubProps.blend"),
                     Path("Sources/KursHub/KursHub.blend")):
        if not (root/relative).is_file():
            raise FileNotFoundError(root/relative)
    return root, work


def _base(name):
    return re.sub(r"\.\d{3}$", "", name)


def _rotation(rotation):
    """Unity Euler Z-X-Y converted through the X,Z,Y coordinate reflection."""
    x, y, z = (math.radians(float(v)) for v in rotation)
    qx = Quaternion((1, 0, 0), x)
    qy = Quaternion((0, 1, 0), y)
    qz = Quaternion((0, 0, 1), z)
    q = qy @ qx @ qz
    return Quaternion((q.w, -q.x, -q.z, -q.y))


def _pose(obj, placement):
    x, y, z = placement["position"]
    obj.location = (x, z, y)
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = _rotation(placement["rotation"])
    x, y, z = placement["scale"]
    if min(x, y, z) <= 0:
        raise ValueError("Non-positive authoring instance scale: "+placement["id"])
    obj.scale = (x, z, y)


def _pack_materials(root, output):
    """Use the established PBR hookup and add the new clear-glass Alpha link."""
    report = apply_materials(root)
    report["revision8Integration"] = True
    report["clearGlassAlpha"] = []
    for material in bpy.data.materials:
        if _base(material.name) not in ("Hub8SkylightGlass", "Hub8FloorWear"):
            continue
        if not material.use_nodes:
            raise AssertionError("Hub8 clear glass lost its material nodes")
        nodes, links = material.node_tree.nodes, material.node_tree.links
        bsdf = next((node for node in nodes if node.type == 'BSDF_PRINCIPLED'), None)
        family = _base(material.name)
        albedo = next((link.from_node for link in bsdf.inputs['Base Color'].links
                       if link.from_node.type == 'TEX_IMAGE'), None) if bsdf else None
        if bsdf is None or albedo is None:
            raise AssertionError("Hub8 clear glass needs its registered BaseColor RGBA map")
        links.new(albedo.outputs['Alpha'], bsdf.inputs['Alpha'])
        material.surface_render_method = 'DITHERED'
        material["KursHub8AlphaSource"] = family+"_BaseColor.png Alpha; no extra opacity multiplier"
        report["clearGlassAlpha"].append(material.name)
    for skipped in report["skipped"]:
        if _base(skipped["name"]).startswith("Hub8"):
            raise AssertionError("New owned material was not packed: "+str(skipped))
    bpy.ops.file.pack_all()
    files = [image for image in bpy.data.images if image.users and image.source == 'FILE']
    unpacked = [image.name for image in files if not image.packed_file and not len(image.packed_files)]
    if unpacked:
        raise AssertionError("Source still depends on unpacked image files: "+", ".join(unpacked))
    report["packedReferencedImages"] = len(files)
    _write(output, report)
    return report


def _archives():
    result = []
    for collection in bpy.data.collections:
        if not collection.name.startswith("Revision7 lettering "):
            continue
        result.append(dict(name=collection.name, objects=len(collection.all_objects),
                           hideRender=collection.hide_render, hideViewport=collection.hide_viewport))
    archive = bpy.data.collections.get("Revision7 lettering archive")
    if archive and (not archive.hide_render or not archive.hide_viewport):
        raise AssertionError("Original glyph archive must remain explicitly hidden")
    return result


def integrate(root=AUTHORING, work=WORK, building_only=False):
    root, work = _guard(root, work)
    manifest_path = root/HUB/"KursHubManifest.json"
    props_path = root/HUB/"Props/KursHubPropsManifest.json"
    pbr_path = root/HUB/"Textures/Audit7PBRManifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf8"))
    props = json.loads(props_path.read_text(encoding="utf8"))
    pbr = json.loads(pbr_path.read_text(encoding="utf8"))
    ids = {p["id"] for p in props["props"]}
    if len(ids) != len(props["props"]):
        raise AssertionError("Duplicate prop-library identifiers")
    families = {record["name"] for record in pbr["families"]}
    required_owned = {chunk["material"] for prop in props["props"] for chunk in prop["chunks"]
                      if chunk["material"].startswith("Hub8")}
    required_owned.update(chunk["material"] for chunk in manifest["chunks"]
                          if chunk["material"].startswith("Hub8"))
    if not required_owned <= families:
        raise AssertionError("New PBR families missing from registered manifest: "+str(required_owned-families))
    for family in required_owned:
        for suffix in ("BaseColor", "Normal", "Roughness", "Metallic", "Occlusion", "Height", "MetallicSmoothness"):
            if not (root/HUB/"Textures"/(family+"_"+suffix+".png")).is_file():
                raise FileNotFoundError(family+"_"+suffix)
    propsfile = root/"Sources/KursHub/KursHubProps.blend"
    buildingfile = root/"Sources/KursHub/KursHub.blend"
    inputs = dict(propsManifest=_sha(props_path), layoutManifest=_sha(manifest_path), pbrManifest=_sha(pbr_path))
    props_report = None
    if not building_only:
        bpy.ops.wm.open_mainfile(filepath=str(propsfile))
        available = {collection.name for collection in bpy.data.collections}
        if not ids <= available:
            raise AssertionError("Native prop collections missing: "+str(ids-available))
        props_archives = _archives()
        props_report = _pack_materials(root, work/"props_material_hookup8.json")
        bpy.context.scene["KursHubQualityRevision"] = "8: manufactured services, printed signs and labels, repaired soft goods; reusable authored library"
        _save_atomic(propsfile)
    else:
        props_archives = []

    bpy.ops.wm.open_mainfile(filepath=str(buildingfile))
    old_collections = {identifier: bpy.data.collections.get(identifier) for identifier in ids}
    controlled = {collection for collection in old_collections.values() if collection}
    controlled_objects = {obj for collection in controlled for obj in collection.all_objects}
    unrelated = {obj.name: obj for obj in bpy.data.objects if obj not in controlled_objects}
    unrelated_collection_names = {collection.name for collection in bpy.data.collections if collection not in controlled}
    saved = {obj.name: (obj, obj.instance_collection.name) for obj in bpy.data.objects
             if obj.instance_collection and obj.instance_collection.name in ids}
    for obj, _identifier in saved.values():
        obj.instance_collection = None
    shared = []
    for identifier, collection in old_collections.items():
        if collection is None:
            continue
        if collection.children:
            raise AssertionError("Controlled library collection unexpectedly contains child collections: "+identifier)
        if any(obj.instance_collection == collection for obj in bpy.data.objects):
            raise AssertionError("Controlled collection is still referenced: "+identifier)
        for obj in list(collection.objects):
            external = [c for c in obj.users_collection if c not in controlled]
            if external:
                collection.objects.unlink(obj)
                shared.append(dict(object=obj.name, retainedIn=[c.name for c in external]))
            else:
                bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(collection)
    with bpy.data.libraries.load(str(propsfile), link=False) as (available, data):
        if not ids <= set(available.collections):
            raise AssertionError("Updated props source is incomplete")
        data.collections = sorted(ids)
    library = {collection.name: collection for collection in data.collections if collection}
    if set(library) != ids:
        raise AssertionError("Controlled imported collection IDs changed")
    known_materials = {entry["name"] for entry in props.get("palette", [])}
    known_materials.update(entry["name"] for entry in manifest.get("palette", []))
    known_materials.update(families)
    known_materials.update(MATERIALS)
    unified = []
    for collection in library.values():
        # Keep reusable types even when no instance currently uses that type.
        # Appended unlinked collections otherwise disappear on save/reopen.
        collection.use_fake_user = True
        for obj in collection.all_objects:
            for slot in obj.material_slots:
                if not slot.material:
                    continue
                name, base = slot.material.name, _base(slot.material.name)
                canonical = bpy.data.materials.get(base)
                if base in known_materials and canonical and canonical != slot.material:
                    slot.material = canonical
                    unified.append(dict(object=obj.name, previous=name, material=base))
    for name, (original, identifier) in saved.items():
        if bpy.data.objects.get(name) != original:
            raise AssertionError("Existing native instance object was replaced: "+name)
        original.instance_collection = library[identifier]

    counts = {}
    placement_by_id = {}
    for key, group_name in (("propInstances", "Reusable exterior props"),
                            ("interiorInstances", "Reusable interior architecture"),
                            ("settlementInstances", "Reusable interior settlement")):
        group = bpy.data.collections.get(group_name)
        if group is None:
            raise AssertionError("Missing established placement collection: "+group_name)
        placements = manifest[key]
        expected = {placement["id"] for placement in placements}
        if len(expected) != len(placements):
            raise AssertionError("Duplicate manifest instance IDs in "+key)
        previous = {obj.get("KursUnityInstanceId", obj.name) for obj in group.objects}
        if not previous <= expected:
            raise AssertionError("Author instance outside manifest preserved without overwriting: "+str(previous-expected))
        for placement in placements:
            if placement["id"] in placement_by_id:
                raise AssertionError("Instance ID reused across groups: "+placement["id"])
            if placement["prop"] not in library:
                raise AssertionError("Unknown placement prop: "+placement["prop"])
            obj = bpy.data.objects.get(_native_name(placement["id"]))
            if obj is None:
                obj = bpy.data.objects.new(_native_name(placement["id"]), None)
                group.objects.link(obj)
            elif obj not in group.objects.values():
                raise AssertionError("Instance name collides with an unrelated authored object: "+placement["id"])
            obj.instance_type = 'COLLECTION'
            obj.instance_collection = library[placement["prop"]]
            _pose(obj, placement)
            obj["UnityPrefab"] = "Assets/Environment/KursHub/Props/Prefabs/"+placement["prop"]+".prefab"
            obj["KursUnityInstanceId"] = placement["id"]
            placement_by_id[placement["id"]] = (obj, placement)
        counts[key] = len(placements)

    lights_group = bpy.data.collections.get("Settlement practical lights")
    if lights_group is None:
        raise AssertionError("Established native practical-light group missing")
    light_ids = set()
    for placement in manifest["settlementLights"]:
        identifier = placement["id"]
        if identifier in light_ids:
            raise AssertionError("Duplicate practical-light identifier: "+identifier)
        light_ids.add(identifier)
        obj = bpy.data.objects.get(_native_name(identifier))
        if obj is None:
            light = bpy.data.lights.new(identifier, 'POINT')
            obj = bpy.data.objects.new(_native_name(identifier), light)
            lights_group.objects.link(obj)
        elif obj.type != 'LIGHT' or obj.data.type != 'POINT' or obj not in lights_group.objects.values():
            raise AssertionError("Authored object collides with manifest Point light: "+identifier)
        x, y, z = placement["position"]
        obj.location = (x, z, y)
        obj.data.color = placement["color"]
        # Keep the established source preview conversion; this is not a claim
        # of photometric equality between Blender watts and URP light units.
        obj.data.energy = placement["intensity"]*4
        obj.data.cutoff_distance = placement["range"]
        obj.data.use_custom_distance = True

    # Source-only mirrors of the actual native floodlight Spot components.
    # Their FBX exports remain mesh-only; Unity receives no duplicate lights.
    source_spots = bpy.data.collections.get("Revision8 native floodlight preview")
    if source_spots is None:
        source_spots = bpy.data.collections.new("Revision8 native floodlight preview")
        bpy.context.scene.collection.children.link(source_spots)
    spot_count = 0
    for identifier, (instance, placement) in placement_by_id.items():
        if placement["prop"] != "KH_Floodlight":
            continue
        name = "Hub8 source Spot "+identifier
        obj = bpy.data.objects.get(name)
        if obj is None:
            light = bpy.data.lights.new(name, 'SPOT')
            obj = bpy.data.objects.new(name, light)
            source_spots.objects.link(obj)
        elif obj.type != 'LIGHT' or obj.data.type != 'SPOT':
            raise AssertionError("Source-only Spot name collision: "+name)
        obj.parent = instance
        obj.location = (0, -.18, 0)
        obj.rotation_mode = 'QUATERNION'
        obj.rotation_quaternion = _rotation((35, 180, 0))
        # Blender lights look along local -Z; Unity Spot lights look along +Z.
        obj.rotation_quaternion @= Quaternion((1, 0, 0), math.pi/2)
        obj.data.color = (1, .73, .43)
        obj.data.energy = float(placement.get("lightIntensity", 0) or 3) * 4
        obj.data.spot_size = math.radians(100)
        obj.data.spot_blend = .35
        obj.data.cutoff_distance = float(placement.get("lightRange", 0) or 14)
        obj.data.use_custom_distance = True
        obj["KursHub8PreviewOnly"] = "Mirror of native KH_Floodlight Work light; not an additional Unity light"
        spot_count += 1

    for name, original in unrelated.items():
        if bpy.data.objects.get(name) != original:
            raise AssertionError("An unrelated shell/archive/source object disappeared: "+name)
    if not unrelated_collection_names <= set(bpy.data.collections.keys()):
        raise AssertionError("An unrelated source collection disappeared")
    building_archives = _archives()
    building_report = _pack_materials(root, work/"building_material_hookup8.json")
    bpy.context.scene["KursHubQualityRevision"] = "8: occupied functional districts, printed signs, manufactured props, clear raised skylight and fixture-authored working light; no NPC simulation"
    bpy.context.scene["KursHubSourceLightingNote"] = "Practical preview conversion only. Unity light components and target-engine renders remain authority."
    baseline = json.loads((WORK/"baseline"/HUB/"KursHubManifest.json").read_text(encoding="utf8"))
    old_ids = {p["id"] for key in ("propInstances", "interiorInstances", "settlementInstances") for p in baseline[key]}
    if not old_ids <= set(saved):
        raise AssertionError("A baseline native instance was not preserved: "+str(old_ids-set(saved)))
    native_poses = _verify_native_poses(manifest, props)
    _save_atomic(buildingfile)
    # Reopen the actual final path, proving the packed native file and stored
    # RNA transforms survive a cold file read after the atomic replacement.
    bpy.ops.wm.open_mainfile(filepath=str(buildingfile))
    reopened_poses = _verify_native_poses(manifest, props)
    for identifier in old_ids:
        if bpy.data.objects.get(identifier) is None:
            raise AssertionError("Baseline instance missing after native reopen: "+identifier)
    report = dict(status="AUDIT8_SOURCE_ASSEMBLY_OK", inputs=inputs,
                  sourceFiles={"props":_sha(propsfile), "building":_sha(buildingfile)},
                  retainedInstanceObjects=len(saved), retainedUnrelatedObjects=len(unrelated),
                  retainedUnrelatedCollections=len(unrelated_collection_names), sharedLibraryObjectsRetained=shared,
                  propCollections=len(library), **counts, manifestPointLights=len(light_ids),
                  sourceOnlyNativeSpotPreviews=spot_count, unifiedMaterialSlots=len(unified),
                  retainedBaselineInstanceIds=len(old_ids), nativePoseValidation=native_poses,
                  nativeReopenedPoseValidation=reopened_poses,
                  propsGlyphArchives=props_archives, buildingGlyphArchives=building_archives,
                  ownedPBRFamilies=sorted(required_owned),
                  propsMaterialReport=props_report, buildingMaterialReport=building_report,
                  limits=["No shell or prop geometry changed by source integration",
                          "No collision policy, floor, roof walking plane or Unity scene changed",
                          "Source lights are previews; actual Unity rendered illumination/FPS require target-engine checks"])
    _write(work/"source_assembly8.json", report)
    print(report["status"], "collections", len(library), "instances", sum(counts.values()), flush=True)
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=AUTHORING)
    parser.add_argument("--work", type=Path, default=WORK)
    parser.add_argument("--building-only", action="store_true")
    arguments = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    integrate(arguments.root, arguments.work, arguments.building_only)
