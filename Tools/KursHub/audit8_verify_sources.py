"""Read-only cold verification of both revision-8 editable Blender sources.

Includes unlinked prop-library collections and the deliberately hidden original
lettering archive.  Visibility is changed only in the validation process so
modifiers on hidden parts can be evaluated.  Neither .blend is ever saved.
Source and manifest SHA-256 values are guarded before and after the whole run.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import sys

import bpy
from mathutils import Vector

PROJECT = Path(__file__).resolve().parents[2]
WORK = PROJECT/"work/KursHub/audit8"
AUTHORING = WORK/"authoring"
HUB = Path("Assets/Environment/KursHub")
sys.path.insert(0, str(Path(__file__).parent))
from audit7_verify_props import geometry
from audit8_integrate import _native_name
from audit8_utilities import transform


def _sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _json(path):
    return json.loads(Path(path).read_text(encoding="utf8"))


def _baseline(root, ids):
    result = {}
    for filename in ("KursHubProps.blend", "KursHub.blend"):
        path = root/"Sources/KursHub"/filename
        bpy.ops.wm.open_mainfile(filepath=str(path))
        libraries = {bpy.data.collections.get(identifier) for identifier in ids}
        libraries.discard(None)
        library_objects = {obj for collection in libraries for obj in collection.all_objects}
        result[filename] = dict(
            sha256=_sha(path),
            propObjects=sorted(obj.name for obj in library_objects),
            unrelatedMeshNames=sorted(obj.name for obj in bpy.data.objects
                                      if obj.type == 'MESH' and obj not in library_objects),
            allObjectNames=sorted(obj.name for obj in bpy.data.objects))
    return result


def _archive_records(errors):
    archive = bpy.data.collections.get("Revision7 lettering archive")
    if archive is None:
        errors.append("Missing explicit original-lettering archive")
        return [], set()
    if not archive.hide_render or not archive.hide_viewport:
        errors.append("Original lettering archive is not hidden in both viewport and renders")
    archive_objects = set(archive.all_objects)
    if not archive_objects:
        errors.append("Original lettering archive is empty")
    records = []
    for collection in (archive, *archive.children_recursive):
        records.append(dict(name=collection.name, objects=len(collection.all_objects),
                           hideRender=collection.hide_render, hideViewport=collection.hide_viewport,
                           hiddenByParent=collection != archive))
    return records, archive_objects


def _images(root, errors):
    # Earlier source stages intentionally retain their original image nodes.
    # A node's user count alone does not prove that it feeds the active shader.
    # Compare current rendering inputs against authored pixels, and report
    # disconnected packed legacy images separately rather than deleting them.
    materials = {slot.material for obj in bpy.data.objects if obj.type == 'MESH'
                 for slot in obj.material_slots if slot.material and slot.material.use_nodes}
    connected = {}
    for material in materials:
        outputs = [node for node in material.node_tree.nodes if node.type == 'OUTPUT_MATERIAL']
        active = [node for node in outputs if node.is_active_output] or outputs
        todo, seen = list(active), set()
        while todo:
            node = todo.pop()
            if node in seen:
                continue
            seen.add(node)
            if node.type == 'TEX_IMAGE' and node.image:
                connected.setdefault(node.image, set()).add(material.name)
            if node.type == 'GROUP' and node.node_tree:
                # No authored Kurs material currently needs nested groups;
                # conservatively include their image nodes if one is added.
                for child in node.node_tree.nodes:
                    if child.type == 'TEX_IMAGE' and child.image:
                        connected.setdefault(child.image, set()).add(material.name)
            for socket in node.inputs:
                todo.extend(link.from_node for link in socket.links)
    records = []
    for image in bpy.data.images:
        if not image.users or image.source != 'FILE':
            continue
        packed = bool(image.packed_file) or bool(len(image.packed_files))
        record = dict(name=image.name, filename=Path(bpy.path.abspath(image.filepath)).name,
                      packed=packed, size=list(image.size), users=image.users,
                      activeShaderImage=image in connected,
                      activeMaterials=sorted(connected.get(image, set())))
        if not packed:
            errors.append("Referenced image is not packed: "+image.name)
        if image.packed_file:
            record['packedSha256'] = hashlib.sha256(image.packed_file.data).hexdigest()
            authoritative = root/HUB/"Textures"/record['filename']
            if authoritative.is_file():
                record['authoritativeSha256'] = _sha(authoritative)
                record['matchesAuthoringFile'] = record['packedSha256'] == record['authoritativeSha256']
                record['legacyRetainedPackedImage'] = not record['matchesAuthoringFile'] and image not in connected
                if not record['matchesAuthoringFile'] and image in connected:
                    errors.append("Active packed texture differs from authored texture: "+image.name)
        records.append(record)
    return records


def _enable_readonly_evaluation():
    # Some library collections have fake users and are not linked to the scene.
    # Bring every mesh into this transient scene, then unhide the graph.  No
    # source file is written, and its on-disk digest is checked after evaluation.
    temporary = bpy.data.collections.new("__Audit8_readonly_geometry_evaluation")
    bpy.context.scene.collection.children.link(temporary)
    for collection in bpy.data.collections:
        collection.hide_viewport = False
        collection.hide_render = False
    for obj in list(bpy.data.objects):
        if obj.type != 'MESH':
            continue
        if obj.name not in bpy.context.view_layer.objects:
            temporary.objects.link(obj)
        obj.hide_viewport = False
        obj.hide_render = False
    def show(layer):
        layer.exclude = False
        layer.hide_viewport = False
        for child in layer.children:
            show(child)
    show(bpy.context.view_layer.layer_collection)
    bpy.context.view_layer.update()
    for obj in bpy.context.view_layer.objects:
        if obj.type == 'MESH':
            obj.hide_set(False)
    bpy.context.view_layer.update()
    return bpy.context.evaluated_depsgraph_get()


def _native_instances(manifest, props, old_ids, errors):
    identifiers, maximum_error, counts = {}, 0., {}
    for key in ("propInstances", "interiorInstances", "settlementInstances"):
        counts[key] = len(manifest[key])
        for placement in manifest[key]:
            identifier = placement['id']
            native_name = _native_name(identifier)
            obj = bpy.data.objects.get(native_name)
            if obj is None:
                errors.append("Native placement missing: "+identifier)
                continue
            identifiers[identifier] = native_name
            if obj.get('KursUnityInstanceId') != identifier:
                errors.append("Full Unity ID property missing or altered: "+identifier)
            if obj.instance_type != 'COLLECTION' or not obj.instance_collection or obj.instance_collection.name != placement['prop']:
                errors.append("Native placement uses wrong prop collection: "+identifier)
            if obj.parent:
                errors.append("Native collection placement has unexpected parent: "+identifier)
            for point in ((0,0,0), (1,0,0), (0,1,0), (0,0,1)):
                actual = obj.matrix_basis @ Vector((point[0], point[2], point[1]))
                expected = transform(point, placement)
                delta = max(abs(actual[i]-expected[j]) for i, j in ((0,0), (1,2), (2,1)))
                maximum_error = max(maximum_error, delta)
                if delta > .000025:
                    errors.append(dict(id=identifier, issue='Serialized matrix_basis pose mismatch', errorMetres=delta))
                    break
    missing_old = sorted(old_ids-set(identifiers))
    if missing_old:
        errors.append(dict(issue='Baseline Unity instance IDs missing', ids=missing_old))
    if len(set(identifiers.values())) != len(identifiers):
        errors.append("Native ID hashing caused a duplicate source object name")
    return dict(total=len(identifiers), **counts, retainedBaselineInstanceIds=len(old_ids)-len(missing_old),
                maximumBasisErrorMetres=maximum_error, basisSamplesPerInstance=4,
                idContract='KursUnityInstanceId plus deterministic UTF-8-safe hashed names when needed')


def run(root=AUTHORING, work=WORK):
    root, work = Path(root).resolve(), Path(work).resolve()
    if root == PROJECT or not root.is_relative_to(WORK.resolve()) or not work.is_relative_to(WORK.resolve()):
        raise ValueError("Read-only source verifier accepts only audit8 staging paths")
    manifest_path = root/HUB/"KursHubManifest.json"
    props_path = root/HUB/"Props/KursHubPropsManifest.json"
    manifest, props = _json(manifest_path), _json(props_path)
    ids = {prop['id'] for prop in props['props']}
    baseline_root = WORK/"baseline"
    baseline_manifest = _json(baseline_root/HUB/"KursHubManifest.json")
    baseline_props = _json(baseline_root/HUB/"Props/KursHubPropsManifest.json")
    baseline_ids = {prop['id'] for prop in baseline_props['props']}
    old_instances = {p['id'] for key in ('propInstances','interiorInstances','settlementInstances') for p in baseline_manifest[key]}
    files = [root/"Sources/KursHub"/filename for filename in ("KursHubProps.blend", "KursHub.blend")]
    guard_paths = files+[manifest_path, props_path, root/HUB/"Textures/Audit7PBRManifest.json"]
    before_hashes = {str(path.relative_to(root)): _sha(path) for path in guard_paths}
    baseline = _baseline(baseline_root, baseline_ids)
    results, all_errors = [], []
    for path in files:
        bpy.ops.wm.open_mainfile(filepath=str(path))
        errors, part_records = [], []
        archives, archived_objects = _archive_records(errors)
        images = _images(root, errors)
        names = set(bpy.data.objects.keys())
        original = baseline[path.name]
        if path.name == 'KursHubProps.blend':
            required_names = set(original['propObjects'])
        else:
            required_names = set(original['unrelatedMeshNames'])
        missing_names = sorted(required_names-names)
        if missing_names:
            errors.append(dict(issue='Original native source object names missing', names=missing_names))
        collections = []
        for prop in props['props']:
            collection = bpy.data.collections.get(prop['id'])
            if collection is None:
                errors.append('Native prop collection missing: '+prop['id'])
                continue
            expected = prop['sourceOffset']
            offset_error = max(abs(collection.instance_offset[i]-expected[j]) for i,j in ((0,0),(1,2),(2,1)))
            if offset_error > .000025:
                errors.append(dict(id=prop['id'], issue='Library collection instance_offset mismatch', errorMetres=offset_error))
            if not collection.all_objects:
                errors.append('Native prop collection is empty: '+prop['id'])
            collections.append(dict(id=prop['id'], objects=len(collection.all_objects),
                                    fakeUser=collection.use_fake_user, offsetErrorMetres=offset_error))
        instances = _native_instances(manifest, props, old_instances, errors) if path.name == 'KursHub.blend' else None
        dependencies = _enable_readonly_evaluation()
        meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
        for index, obj in enumerate(meshes, 1):
            try:
                evaluated = obj.evaluated_get(dependencies)
                mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=dependencies)
                metrics = geometry(mesh)
                finite_vertices = all(math.isfinite(c) for vertex in mesh.vertices for c in vertex.co)
                evaluated.to_mesh_clear()
                issues = [key for key in ('nonManifoldEdges', 'degenerateFaces', 'invalidFaceNormals', 'nonPositiveSolids') if metrics[key]]
                if not metrics['uvLayers'] or not metrics['finiteUV']:
                    issues.append('missingOrInvalidUV')
                if not finite_vertices:
                    issues.append('nonFiniteVertices')
                if not obj.material_slots or any(slot.material is None for slot in obj.material_slots):
                    issues.append('missingMaterialSlot')
                if obj.matrix_basis.determinant() <= 0:
                    issues.append('nonPositiveTransformDeterminant')
                if any(scale <= 0 for scale in obj.scale):
                    issues.append('nonPositiveScale')
                record = dict(name=obj.name, archived=obj in archived_objects,
                              triangles=metrics['triangles'], connectedSolids=metrics['connectedSolids'],
                              vertices=metrics['vertices'], polygons=metrics['polygons'],
                              nonManifoldEdges=metrics['nonManifoldEdges'], degenerateFaces=metrics['degenerateFaces'],
                              invalidFaceNormals=metrics['invalidFaceNormals'], nonPositiveSolids=metrics['nonPositiveSolids'],
                              smallestSolidVolume=metrics['smallestSolidVolume'], finiteUV=metrics['finiteUV'],
                              finiteVertices=finite_vertices, uvLayers=metrics['uvLayers'], issues=issues)
                if issues:
                    errors.append(dict(object=obj.name, issues=issues, metrics=metrics))
                part_records.append(record)
            except Exception as error:
                errors.append(dict(object=obj.name, exception=repr(error)))
            if index % 500 == 0:
                print('AUDIT8_NATIVE_PARTS', path.name, index, '/', len(meshes), 'issues', len(errors), flush=True)
        result = dict(file=path.name, sha256=_sha(path), meshes=len(meshes),
                      triangles=sum(p['triangles'] for p in part_records),
                      connectedSolids=sum(p['connectedSolids'] for p in part_records),
                      retainedOriginalSourceNames=len(required_names)-len(missing_names),
                      requiredOriginalSourceNames=len(required_names), propCollections=collections,
                      archives=archives, archivedMeshCount=sum(p['archived'] for p in part_records),
                      packedImageCount=len(images), images=images, instances=instances,
                      activeShaderImageCount=sum(image['activeShaderImage'] for image in images),
                      retainedLegacyPackedImageCount=sum(image.get('legacyRetainedPackedImage', False) for image in images),
                      parts=part_records, errors=errors)
        results.append(result)
        all_errors.extend(dict(file=path.name, detail=error) for error in errors)
        print('AUDIT8_NATIVE_FILE', path.name, 'meshes', len(meshes), 'issues', len(errors), flush=True)
    after_hashes = {str(path.relative_to(root)): _sha(path) for path in guard_paths}
    changed = [path for path in before_hashes if before_hashes[path] != after_hashes[path]]
    if changed:
        all_errors.append(dict(issue='Source or manifest changed during read-only verification', paths=changed))
    report = dict(status='AUDIT8_NATIVE_SOURCES_OK' if not all_errors else 'AUDIT8_NATIVE_SOURCES_FAILED',
                  blenderVersion=bpy.app.version_string, readonly=True,
                  guardedBefore=before_hashes, guardedAfter=after_hashes, guardedFilesUnchanged=not changed,
                  files=results, errors=all_errors,
                  limits=['Native geometry, UV, packed dependencies, IDs and serialized poses only',
                          'No visual reference-accuracy, illumination, NPC performance or AAA rating is inferred'])
    destination = work/'source_validation8.json'
    destination.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print(report['status'], 'files', len(results), 'errors', len(all_errors), flush=True)
    if all_errors:
        raise RuntimeError('Native source verification failed; see '+str(destination))
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=AUTHORING)
    parser.add_argument('--work', type=Path, default=WORK)
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    run(args.root, args.work)
