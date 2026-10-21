"""Fail-closed publication of revision 8. No command runs Unity or saves a scene.

Commands are explicit: freeze-guards, verify, organize, deploy. The first command
anchors already captured main snapshots once; it never refreshes them. Verify is
read-only. Organize builds a new FINAL_ASSETS/Revision8 directory. Deploy changes
only the Kurs asset library and its two editable Blender sources, with verified
backups and the root prefab written last. Interrupted deployments are recorded;
they are not automatically rolled back over another developer's changes.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import tarfile
import uuid

R = Path(__file__).resolve().parents[2]
W = R / "work/KursHub/audit8"
T = R / "work/KursHub/UnityProject"
A = W / "authoring"
B = W / "baseline"
D = Path("Assets/Environment/KursHub")
S = Path("Sources/KursHub")
SC = Path("Assets/CharacterTesting/CharacterTest.unity")
FINAL = R / "FINAL_ASSETS/Revision8"
PACKAGE = W / "delivery/KursHub_Final.unitypackage"
ANCHOR = W / "publication_guards8.json"
GUARDS = ("main_before8.json", "main_scene_snapshot8.json", "player_source_sync8.json")
ROOT_PREFAB = (D / "KursHub.prefab").as_posix()
ROOT_GUID = "ff5cdff5a433ef342ad7bf24aeafd8b8"
SOURCE_NAMES = ("KursHub.blend", "KursHubProps.blend")
PBR_PATH = D / "Textures/Audit7PBRManifest.json"
LAYOUT = W / "functional_layout.json"


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def read(path):
    return Path(path).read_text(encoding="utf-8-sig")


def js(path):
    return json.loads(read(path))


def dump(path, value, exclusive=False):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x" if exclusive else "w", encoding="utf8", newline="\n") as stream:
        json.dump(value, stream, indent=2, ensure_ascii=False)
        stream.write("\n")


def relpath(value):
    value = str(value).replace("\\", "/")
    path = Path(value)
    require(not path.is_absolute() and ".." not in path.parts and ":" not in value,
            "Unsafe repository-relative path: " + value)
    return path


def inventory(root, relative):
    return {p.relative_to(root).as_posix(): sha(p)
            for p in sorted((root / relative).rglob("*"), key=lambda p: p.as_posix()) if p.is_file()}


def authored_main():
    return inventory(R, D) | inventory(R, S)


def scene_snapshot():
    record = js(W / "main_scene_snapshot8.json")
    if "path" in record:
        require(relpath(record["path"]) == SC, "Snapshot targets a different gameplay scene")
        expected = record["sha256"]
    else:
        require(set(record) == {SC.as_posix()}, "Ambiguous gameplay scene snapshot")
        expected = record[SC.as_posix()]
    require(re.fullmatch(r"[0-9a-f]{64}", expected), "Invalid scene snapshot SHA")
    return expected


def main_guard(expected=None):
    expected = js(W / "main_before8.json") if expected is None else expected
    require(authored_main() == expected,
            "Main Kurs file set/content changed since immutable snapshot; preserve the new work and re-audit")
    require(sha(R / SC) == scene_snapshot(), "Concurrent CharacterTest edit; do not restore/copy an old scene")


def player_guard():
    proof = js(W / "player_source_sync8.json")
    require(proof.get("status") == "CURRENT_PLAYER_TEST_INPUTS_STAGED" and proof.get("mainFilesUnchanged") is True,
            "Current player staging proof is missing or failed")
    files = proof["files"]
    expected = {
        "Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab",
        "Assets/Scripts/Player/FirstPersonController.cs",
        "Assets/InputSystem_Actions.inputactions",
    }
    require(set(files) == expected | {p + ".meta" for p in expected}, "Unexpected player input file set")
    for name, digest in files.items():
        path = relpath(name)
        require(sha(R / path) == digest == sha(T / path), "Player changed since tested staging: " + name)
    return files


def freeze_guards():
    require(not ANCHOR.exists(), "Publication guards already exist and will not be refreshed")
    main_guard()
    player_guard()
    record = {"status": "IMMUTABLE_PUBLICATION_GUARDS8", "createdUtc": utc(),
              "files": {name: sha(W / name) for name in GUARDS},
              "sceneSha256": scene_snapshot(),
              "contract": "Do not replace this anchor or its inputs to bypass a concurrent edit."}
    dump(ANCHOR, record, exclusive=True)
    print("AUDIT8_GUARDS_FROZEN")


def check_anchor():
    record = js(ANCHOR)
    require(record.get("status") == "IMMUTABLE_PUBLICATION_GUARDS8", "Missing immutable publication anchor")
    require(record["files"] == {name: sha(W / name) for name in GUARDS}, "Immutable publication guard was changed")
    require(record["sceneSha256"] == scene_snapshot(), "Scene guard changed")


def ids(path):
    return set(re.findall(r"^--- !u!\d+ &(-?\d+)", read(path), re.M))


def guid(path):
    match = re.search(r"^guid: ([a-f0-9]{32})\s*$", read(path), re.M)
    require(match is not None, "Invalid Unity meta GUID: " + str(path))
    return match[1]


def library_hash():
    # Exact StringComparer.Ordinal contract used by the native player/capture probes.
    excluded = {"Models/KursHubShell.fbx", "KursHub.prefab", "KursHubManifest.json"}
    paths = sorted((p for p in (T / D).rglob("*") if p.is_file() and p.suffix != ".meta"
                    and p.relative_to(T / D).as_posix() not in excluded),
                   key=lambda p: p.relative_to(T).as_posix())
    return hashlib.sha256("\n".join(p.relative_to(T).as_posix() + ":" + sha(p) for p in paths).encode()).hexdigest()


def text_fields(path):
    result = {}
    for line in read(path).splitlines():
        if "=" in line:
            key, value = line.split("=", 1)
            require(key not in result, "Repeated native proof key: " + key)
            result[key] = value
    return result


def clean(proof, keys=("errors", "problems", "engineErrors")):
    for key in keys:
        require(not proof.get(key), "Nonempty verification failures: " + key)


def model_geometry():
    props = js(T / D / "Props/KursHubPropsManifest.json")
    require(len(props["props"]) >= 68, "Revision 8 props are incomplete")
    expected = {relpath(lod["model"]).as_posix() for prop in props["props"] for lod in prop.get("lods", [])}
    require(len(expected) >= 200, "Missing prop LOD manifest entries")
    actual = {p.relative_to(T).as_posix() for p in (T / D / "Props/Models").rglob("*.fbx")}
    require(actual == expected, "Prop FBX file set differs from the final manifest")
    proof = js(W / "release_geometry/fbx_roundtrip.json")
    require(proof["status"] in ("AUDIT7_PROP_ROUNDTRIP_OK", "AUDIT8_PROP_ROUNDTRIP_OK"), "Prop roundtrip failed")
    clean(proof)
    require({relpath(row["path"]).as_posix() for row in proof["results"]} == expected, "Stale prop roundtrip set")
    for row in proof["results"]:
        path = relpath(row["path"])
        require(not row.get("issues"), "Prop roundtrip issues: " + str(path))
        require(sha(A / path) == row["sha256"] == sha(T / path), "Stale prop FBX proof: " + str(path))
    shell = js(W / "interior_lining_shared/interior_lining_proof.json")
    require(shell["status"] == "HUB8_INTERIOR_LINING_SOURCE_FBX_OK", "Shell proof failed")
    require({row["level"] for row in shell["models"]} == {0, 1, 2}, "Three shell LODs were not checked")
    actual_shells = {p.relative_to(T).as_posix() for p in (T / D / "Models").rglob("*.fbx")}
    require(actual_shells == {relpath(row["model"]).as_posix() for row in shell["models"]}, "Unexpected shell FBX set")
    for row in shell["models"]:
        path = relpath(row["model"])
        require(sha(A / path) == row["sha256"] == sha(T / path), "Stale shell proof: " + str(path))
        for key in ("allModelAndGeometryUIDsPreserved", "allConnectionsMaterialsTexturesAndTransformsIdentical",
                    "allOriginalMeshArrayPrefixesIdentical", "unchangedGlobalSettings"):
            require(row.get(key) is True, "Shell identity contract failed: " + key)
        require(row.get("collisionGeometryPayloadsIdentical", 0) > 0, "Missing unchanged shell collision proof")
        require(row.get("geometryMetrics"), "Missing real shell reimport metrics")
        for metric in row["geometryMetrics"]:
            require(all(metric.get(key) == 0 for key in ("nonManifoldEdges", "degenerateFaces", "invalidFaceNormals", "nonPositiveSolids"))
                    and metric.get("finiteUV") is True and metric.get("uvLayers") and metric.get("signedVolume", 0) > 0,
                    "Invalid shell body: " + metric.get("name", "?"))
    for name in expected | actual_shells:
        with (T / name).open("rb") as stream:
            require(stream.read(23) == b"Kaydara FBX Binary  \x00\x1a\x00", "Not a real binary FBX: " + name)
    return len(props["props"]), len(expected), len(actual_shells)


def source_and_pbr():
    proof = js(W / "source_validation8.json")
    require(proof["status"] == "AUDIT8_NATIVE_SOURCES_OK" and proof.get("guardedFilesUnchanged") is True, "Source reopen failed")
    clean(proof)
    before, after = proof["guardedBefore"], proof["guardedAfter"]
    require(before == after and len(before) >= 5, "Source verification changed its inputs")
    for name, digest in before.items():
        require(sha(A / relpath(name)) == digest, "Stale native source proof: " + name)
    source_files = {row["file"]: row["sha256"] for row in proof["files"]}
    require(set(source_files) == set(SOURCE_NAMES), "Both editable sources must reopen")
    for name, digest in source_files.items():
        require(sha(A / S / name) == digest, "Stale editable source: " + name)
    assembly = js(W / "source_assembly8.json")
    require(assembly["status"] == "AUDIT8_SOURCE_ASSEMBLY_OK", "Source assembly failed")
    for key, path in (("propsManifest", D / "Props/KursHubPropsManifest.json"),
                      ("layoutManifest", D / "KursHubManifest.json"), ("pbrManifest", PBR_PATH)):
        require(assembly["inputs"][key] == sha(A / path) == sha(T / path), "Stale source assembly input: " + key)
    require(assembly["sourceFiles"]["props"] == source_files["KursHubProps.blend"]
            and assembly["sourceFiles"]["building"] == source_files["KursHub.blend"], "Source assembly predates final source changes")
    manifest = js(T / PBR_PATH)
    require(len(manifest["families"]) >= 38 and len(manifest["maps"]) >= len(manifest["families"]) * 7, "Incomplete revision 8 PBR families")
    for row in manifest["maps"]:
        path = PBR_PATH.parent / relpath(row["file"])
        require(sha(A / path) == row["sha256"] == sha(T / path), "Stale/missing PBR map: " + row["file"])
    pbr = js(W / "reports/PBR_Readback8.json")
    require(pbr["status"] == "PBR_READBACK8_PASS" and pbr.get("pbrManifestSha256") == sha(T / PBR_PATH), "PBR readback is not bound to final maps")
    clean(pbr)
    require(pbr["families"] == len(manifest["families"]) and len(pbr["maps"]) == len(manifest["maps"]), "Stale PBR readback counts")
    require(pbr["maps"] == manifest["maps"], "PBR readback map records differ from final manifest")
    require({row["name"] for row in pbr["familyResults"]} == {row["name"] for row in manifest["families"]}, "PBR family readback set mismatch")
    require(pbr.get("packedChannelsVerified") is True and all(row.get("packedChannelsVerified") is True for row in pbr["familyResults"]), "PBR packing was not verified")
    return len(manifest["families"]), assembly


def package_check(prefabs):
    proof = js(W / "package_import_validation8.json")
    require(proof["status"] == "PASS" and proof["packageSha256"] == sha(PACKAGE), "Package clean-import proof is stale")
    require(proof["prefabCount"] == prefabs and proof.get("independentCollisionChecks") == prefabs, "Package prefab/collision validation incomplete")
    clean(proof)
    # Inspect without extraction. Unity archives use GUID/pathname, asset, asset.meta.
    entries = {}
    with tarfile.open(PACKAGE, "r:gz") as archive:
        members = {item.name: item for item in archive.getmembers() if item.isfile()}
        for member in archive.getmembers():
            if not member.isfile() or not member.name.endswith("/pathname"):
                continue
            folder = member.name.rsplit("/", 1)[0]
            pathname = archive.extractfile(member).read().decode("utf-8-sig").rstrip("\r\n\x00")
            path = relpath(pathname).as_posix()
            require(path not in entries, "Duplicate package path: " + path)
            pair = {}
            for kind in ("asset", "asset.meta"):
                key = folder + "/" + kind
                if key in members:
                    pair[kind] = hashlib.sha256(archive.extractfile(members[key]).read()).hexdigest()
            entries[path] = pair
    required = inventory(T, D)
    for name, digest in required.items():
        path = name[:-5] if name.endswith(".meta") else name
        kind = "asset.meta" if name.endswith(".meta") else "asset"
        require(entries.get(path, {}).get(kind) == digest, "Package differs from final tested file/meta: " + name)
    return sha(PACKAGE), len(entries)


def capture_check(lib, dependency):
    report = js(W / "after/capture_report.json")
    require(report["status"] == "PASS_NATIVE_CAPTURE" and report["phase"] == "after", "Final native captures missing")
    clean(report)
    require(report["prefabSha256"] == sha(T / ROOT_PREFAB) and report["layoutSha256"] == sha(LAYOUT), "Capture predates final layout/prefab")
    require(report.get("librarySha256") == lib and report.get("prefabDependencyHash128") == dependency,
            "Capture predates final dependency library")
    require((report["width"], report["height"]) == (1920, 1080), "Final capture resolution mismatch")
    views = report["views"]
    require(len(views) == report["dayViews"] + report["nightViews"] and report["dayViews"] >= 73 and report["nightViews"] >= 5,
            "Final camera matrix incomplete")
    image_hashes = {}
    for view in views:
        path = W / "after" / relpath(view["file"])
        with path.open("rb") as stream:
            header = stream.read(24)
        require(header[:8] == b"\x89PNG\r\n\x1a\n" and struct.unpack(">II", header[16:24]) == (1920, 1080), "Bad/missing final image")
        image_hashes[path.relative_to(W).as_posix()] = sha(path)
    require(len(image_hashes) == len(views), "Duplicate final capture files")
    return image_hashes


def verify():
    check_anchor()
    main_guard()
    player_inputs = player_guard()
    tested = js(W / "engine_input8.json")["inputFiles"]
    editors = {p.relative_to(R).as_posix(): sha(p) for p in (R / "Assets/Editor").glob("KursHub*.cs")}
    t_editors = {path: sha(T / path) for path in editors}
    require(editors and editors == t_editors, "Main Kurs editor code differs from tested T")
    require(all(tested.get(path) == digest for path, digest in editors.items()), "Kurs editor code changed after engine preparation")
    owned_helpers = {p.relative_to(T).as_posix(): sha(p) for p in (T / "Assets/Editor").glob("KursHub*.cs")
                     if p.relative_to(T).as_posix() not in editors}
    prop_count, prop_fbx, shell_fbx = model_geometry()
    family_count, assembly = source_and_pbr()
    prefabs = list((T / D).rglob("*.prefab"))
    require(len(prefabs) == prop_count + 1, "Root and nested prefab count mismatch")
    require(assembly["propCollections"] == prop_count, "Stale assembled source library count")
    final_layout = js(T / D / "KursHubManifest.json")
    require(final_layout.get("stage") == 8 and final_layout.get("hub8FloorWear", {}).get("placements"), "Incomplete final revision 8 layout")
    for key in ("propInstances", "interiorInstances", "settlementInstances"):
        require(assembly[key] == len(final_layout[key]), "Source placement count differs from final layout: " + key)
    source_instances = sum(len(final_layout[key]) for key in ("propInstances", "interiorInstances", "settlementInstances"))
    require(assembly["nativeReopenedPoseValidation"]["instances"] == source_instances,
            "Final populated source was not reopened/pose-checked")
    package_sha, package_entries = package_check(len(prefabs))
    native_text = read(W / "native_build8.txt")
    native = text_fields(W / "native_build8.txt")
    require(native_text.startswith("KURS8_NATIVE_BUILD_PASS\n"), "Native build failed")
    require(native["Unity"] == "6000.6.3f1" and native["MainSceneSaved"] == "False", "Native build contract failed")
    if native.get("BuildEvidenceMode") == "CompletedCoreWithFreshReadonlyFinalizer":
        require(native["TwoBuildIdentitiesStable"] == "HistoricalCoreControlFlow", "Historical core must be labelled explicitly")
        core = js(W / "native_core_receipt8.json")
        final = js(W / "native_finalize8.json")
        require(core["status"] == "CORE_BUILD_COMPLETED_FINAL_SCENE_GUARD_FAILED"
                and core.get("twoBuildIdentityGuardPassedByControlFlow") is True, "Invalid completed-core evidence")
        require(final["status"] == "PASS_READONLY_NATIVE_FINALIZER8" and final.get("twoBuildsRerun") is False
                and final.get("freshSetupAndQualityChecksPassed") is True and final.get("assetsChanged") is False
                and final.get("mainSceneSaved") is False and final.get("packageReExported") is False, "Read-only native finalizer failed")
        require(native["CORE_RECEIPT_SHA256"] == final["coreReceiptSha256"] == sha(W / "native_core_receipt8.json"), "Core receipt binding stale")
        require(native["CORE_BUILD_LOG_SHA256"] == final["coreBuildLogSha256"] == core["buildLogSha256"]
                == sha(W / relpath(core["buildLogPath"])), "Original failed scene-guard log changed")
        require(native["FINALIZER_SHA256"] == sha(W / "native_finalize8.json"), "Finalizer receipt changed")
        require(core["assets"] == inventory(T, D), "T assets changed after completed core build")
        require(core["packageSha256"] == final["packageSha256"] == package_sha, "Completed core package changed")
        require(core["mainEditors"] == editors, "Core editor code differs from current main")
        require(sha(R / relpath(core["sourceBuildPath"])) == core["sourceBuildSha256"]
                and sha(T / relpath(core["compiledBuildSourcePath"])) == core["compiledBuildSourceSha256"], "Completed core source changed")
        require(sha(W / relpath(core["auditPath"])) == core["auditSha256"], "Archived original native audit changed")
        require(final["finalizerSourceSha256"] == sha(R / "Tools/KursHub/audit8_finalize_native.cs.txt")
                == sha(T / "Assets/Editor/KursHubFinalizeNative8.cs"), "Read-only finalizer source changed")
        require(final["mainSceneSha256"] == scene_snapshot() and final["effectivePrefabIdsSha256"]
                == sha(W / "effective_prefab_ids8.txt"), "Fresh current-scene target proof stale")
    else:
        require(native["TwoBuildIdentitiesStable"] == "True", "Native two-build identity check missing")
    require(native["MainSceneSHA256"] == scene_snapshot() and native["PACKAGE_SHA256"] == package_sha, "Native build scene/package binding stale")
    for key, path in (("FBX", D / "Models/KursHubShell.fbx"), ("PREFAB", D / "KursHub.prefab"), ("MANIFEST", D / "KursHubManifest.json")):
        require(native[key + "SHA256"] == sha(T / path), "Native build hash stale: " + key)
    require(guid(T / D / "KursHub.prefab.meta") == ROOT_GUID, "Root GUID changed")
    old_ids = ids(B / D / "KursHub.prefab")
    require(old_ids <= ids(T / ROOT_PREFAB) and int(native["OldRootIdsPreserved"]) == len(old_ids), "Baseline 7 authored fileID lost")
    refs = set(re.findall(r"\{fileID: (-?\d+), guid: " + ROOT_GUID + r", type: 3\}", read(R / SC))) - {"100100000"}
    effective = set(read(W / "effective_prefab_ids8.txt").splitlines())
    require(refs and refs <= effective and int(native["MainSceneNativeTargets"]) == len(refs), "Current scene native prefab targets are not all preserved")
    nav = js(W / "navmesh_validation.json")
    require(nav["status"] == "PASS" and nav["unityVersion"] == "6000.6.3f1", "Final native NavMesh validation failed")
    clean(nav)
    require(nav["prefabSHA256"] == sha(T / ROOT_PREFAB) and nav["inputSHA256"] == sha(LAYOUT), "Stale NavMesh proof")
    require(len(nav["routes"]) == 51 and len(nav["zones"]) == 32 and all(row["status"] == "PASS" for row in nav["routes"] + nav["zones"]), "Final 51-route/32-zone validation incomplete")
    lib = library_hash()
    if native.get("BuildEvidenceMode") == "CompletedCoreWithFreshReadonlyFinalizer":
        require(native["LIBRARY_SHA256"] == core["librarySha256"] == final["librarySha256"] == lib,
                "Historical core/fresh finalizer library binding stale")
        require(final["assetInventorySha256"] == core["assetInventorySha256"], "Finalizer inventory differs from completed core")
    player = js(W / "player_validation.json")
    require(player["status"] == "PASS" and player.get("hashGatePassed") is True and player.get("finalLibrarySHA256") == lib, "Final actual-player proof failed/stale")
    clean(player)
    plan = player["plan"]
    for key, expected in (("prefabSHA256", sha(T / ROOT_PREFAB)), ("prefabDependencyHash128", nav["prefabDependencyHash128"]),
                          ("layoutSHA256", sha(LAYOUT)), ("navmeshReportSHA256", sha(W / "navmesh_validation.json")), ("librarySHA256", lib),
                          ("playerPrefabSHA256", player_inputs["Assets/CharacterTesting/Prefabs/CharacterTestPlayer.prefab"]),
                          ("motorImplementationSHA256", player_inputs["Assets/Scripts/Player/FirstPersonController.cs"])):
        require(plan[key] == expected, "Stale player proof: " + key)
    require(player.get("warpsInsideWalkSegments") == 0 and player.get("queuedInputSystemWFrames", 0) > 0 and player.get("actualMoveActionSamples", 0) > 0,
            "Player proof did not use continuous real input/motor movement")
    require(not player.get("fatal") and player.get("sceneSaved") is False, "Player validation changed a scene or failed")
    require(len(player["routes"]) == 51 and len(player["zones"]) == 32
            and all(row["status"] == "PASS" for row in player["routes"] + player["zones"] + player["cases"]), "Player route/inspection cases incomplete")
    images = capture_check(lib, nav["prefabDependencyHash128"])
    candidate = inventory(T, D) | {(S / name).as_posix(): sha(A / S / name) for name in SOURCE_NAMES}
    old = js(W / "main_before8.json")
    require({p for p in old if p.startswith(D.as_posix() + "/")} <= set(candidate), "Deleting old library assets is not authorized")
    for name, digest in candidate.items():
        if name.endswith(".meta") and (R / name).exists():
            require(guid(T / name) == guid(R / name), "Existing Unity GUID would change: " + name)
    # Recheck mutable inputs after all expensive hashing/archive reads.
    main_guard()
    player_guard()
    require(candidate == inventory(T, D) | {(S / name).as_posix(): sha(A / S / name) for name in SOURCE_NAMES}, "Final candidate changed during verification")
    return {"status": "AUDIT8_PUBLICATION_GATES_PASS", "generatedUtc": utc(), "sceneSha256": scene_snapshot(),
            "sceneNativeTargets": len(refs), "baselineRootIds": len(old_ids), "librarySha256": lib,
            "prefabSha256": candidate[ROOT_PREFAB], "packageSha256": package_sha, "packageEntries": package_entries,
            "propCount": prop_count, "prefabCount": len(prefabs), "propFBXCount": prop_fbx, "shellFBXCount": shell_fbx,
            "pbrFamilyCount": family_count, "sourceInstances": source_instances,
            "candidateFiles": candidate, "mainEditors": editors, "ownedTestHelpers": owned_helpers, "captureFiles": images,
            "nativeBuildEvidenceMode": native.get("BuildEvidenceMode", "FullNativeBuild"),
            "limitations": ["Bare current FPC/capsule and environment routes only; no UAL/avatar/pistol/whole-player acceptance.",
                            "No NPC-load/FPS/world-save/registry test or automatic visual AAA acceptance.",
                            "CharacterTest and other developer scenes/gameplay assets are never published."]}


def utc():
    return datetime.now(timezone.utc).isoformat()


def source_path(name):
    return A / name if name.startswith(S.as_posix() + "/") else T / name


def order(name):
    if name == ROOT_PREFAB:
        return 3, name
    if name.endswith(".prefab"):
        return 2, name
    # Meshes, materials, textures, manifests, metas and editable sources precede prefabs.
    return 1, name


def atomic_copy(src, dst, digest):
    dst.parent.mkdir(parents=True, exist_ok=True)
    temporary = dst.with_name(dst.name + ".kurs8-" + uuid.uuid4().hex + ".tmp")
    try:
        with Path(src).open("rb") as source, temporary.open("xb") as target:
            shutil.copyfileobj(source, target, 1024 * 1024)
            target.flush()
            os.fsync(target.fileno())
        require(sha(temporary) == digest, "Staging copy hash mismatch")
        os.replace(temporary, dst)
    finally:
        if temporary.exists():
            temporary.unlink()


def deploy():
    proof = verify()
    before = js(W / "main_before8.json")
    pending = [name for name, digest in proof["candidateFiles"].items() if before.get(name) != digest]
    require(pending, "No changes to deploy")
    backup = W / "publication_backups" / (datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S") + "-" + uuid.uuid4().hex[:8])
    backup.mkdir(parents=True, exist_ok=False)
    journal = {"status": "BACKING_UP", "createdUtc": utc(), "publication": proof,
               "before": {name: before.get(name) for name in pending}, "written": [], "sceneRewritten": False,
               "recovery": "Restore a backup only if current main hash still equals the journal's written candidate hash; never overwrite concurrent edits."}
    dump(backup / "transaction.json", journal)
    for name in pending:
        if name in before:
            target = backup / "before" / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(R / name, target)
            require(sha(target) == before[name], "Backup readback mismatch: " + name)
    main_guard()
    journal["status"] = "PUBLISHING"
    dump(backup / "transaction.json", journal)
    expected = dict(before)
    try:
        for name in sorted(pending, key=order):
            # Full set/hash guard before root publication; per-file guards on other writes.
            if name == ROOT_PREFAB:
                main_guard(expected)
                player_guard()
            require((sha(R / name) if (R / name).exists() else None) == expected.get(name), "Concurrent target edit: " + name)
            digest = proof["candidateFiles"][name]
            require(sha(source_path(name)) == digest, "Tested publication source changed: " + name)
            require(sha(R / SC) == proof["sceneSha256"], "Concurrent gameplay scene edit during publication")
            atomic_copy(source_path(name), R / name, digest)
            expected[name] = digest
            journal["written"].append(name)
            dump(backup / "transaction.json", journal)
        main_guard(expected)
        require(all(sha(R / name) == digest for name, digest in proof["candidateFiles"].items()), "Published readback mismatch")
        journal["status"] = "AUDIT8_DEPLOYED"
        journal["completedUtc"] = utc()
        dump(backup / "transaction.json", journal)
        dump(W / "deployment8.json", {**proof, "status": journal["status"], "backup": backup.relative_to(R).as_posix(),
                                      "copiedFiles": len(pending), "sceneRewritten": False})
    except Exception as error:
        journal["status"] = "INTERRUPTED_REVIEW_BACKUP_BEFORE_RECOVERY"
        journal["error"] = str(error)
        dump(backup / "transaction.json", journal)
        raise
    print("AUDIT8_DEPLOYED", len(pending), "files; scene unchanged; backup", backup.relative_to(R).as_posix())


def organize():
    proof = verify()
    require(not FINAL.exists(), "FINAL_ASSETS/Revision8 already exists; no previous delivery will be overwritten")
    stage = FINAL.with_name("Revision8.pending-" + uuid.uuid4().hex)
    stage.mkdir(parents=True, exist_ok=False)
    def cp(src, relative):
        target = stage / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, target)
        require(sha(src) == sha(target), "Delivery copy mismatch")
    for name in proof["candidateFiles"]:
        cp(source_path(name), Path("BLENDER") / Path(name).name if name.startswith(S.as_posix() + "/") else Path("UNITY") / name)
    # Keep importable Unity paths/metas intact; the package is the canonical one-step import.
    folder_meta = T / (D.as_posix() + ".meta")
    if folder_meta.exists():
        cp(folder_meta, Path("UNITY") / (D.as_posix() + ".meta"))
    cp(PACKAGE, Path("KursHub_Final.unitypackage"))
    for name in proof["captureFiles"]:
        cp(W / name, Path("PREVIEWS/Final_Renders") / Path(name).name)
    reports = ["publication_guards8.json", *GUARDS, "native_build8.txt", "navmesh_validation.json", "player_validation.json",
               "effective_prefab_ids8.txt", "package_import_validation8.json", "source_validation8.json", "source_assembly8.json",
               "release_geometry/fbx_roundtrip.json", "interior_lining_shared/interior_lining_proof.json",
               "reports/PBR_Readback8.json", "after/capture_report.json"]
    for name in reports:
        cp(W / name, Path("REPORTS") / name)
    if proof["nativeBuildEvidenceMode"] == "CompletedCoreWithFreshReadonlyFinalizer":
        for name in ("native_core_receipt8.json", "native_finalize8.json", "native_build_final8.log", "scene_reconciliation8.json"):
            cp(W / name, Path("REPORTS") / name)
        for source in (W / "native_core_evidence8").rglob("*"):
            if source.is_file():
                cp(source, Path("REPORTS/native_core_evidence8") / source.relative_to(W / "native_core_evidence8"))
    dump(stage / "REPORTS/publication_gates8.json", proof)
    (stage / "README.txt").write_text(
        "KursHub revision 8\n\nImport KursHub_Final.unitypackage into Unity 6000.6.3f1 / URP. "
        "The UNITY folder preserves original Assets paths and .meta GUIDs; do not flatten or rename these folders.\n"
        "BLENDER contains both editable native sources. The importable UNITY tree includes all real building/prop FBX, "
        "textures, materials and reusable nested prefabs. Previous FINAL_ASSETS revision 7 files remain untouched.\n\n"
        + "\n".join(proof["limitations"]) + "\n", encoding="utf8")
    integrity = {p.relative_to(stage).as_posix(): sha(p) for p in stage.rglob("*") if p.is_file()}
    dump(stage / "INTEGRITY.json", {"status": "REVISION8_DELIVERY_COMPLETE", "files": integrity})
    main_guard()
    player_guard()
    require(proof["candidateFiles"] == inventory(T, D) | {(S / name).as_posix(): sha(A / S / name) for name in SOURCE_NAMES}, "Candidate changed during delivery assembly")
    require(all(sha(stage / name) == digest for name, digest in integrity.items()), "Delivery integrity failed")
    os.replace(stage, FINAL)
    print("AUDIT8_DELIVERY_ORGANIZED", FINAL)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("freeze-guards", "verify", "organize", "deploy"))
    args = parser.parse_args()
    if args.command == "freeze-guards":
        freeze_guards()
    elif args.command == "verify":
        result = verify()
        print(json.dumps({key: value for key, value in result.items() if key not in ("candidateFiles", "captureFiles", "mainEditors")}, indent=2))
    elif args.command == "organize":
        organize()
    else:
        deploy()


if __name__ == "__main__":
    main()
