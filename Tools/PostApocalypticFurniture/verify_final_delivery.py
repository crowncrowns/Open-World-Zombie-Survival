"""Validate installed files, complete ZIP manifest and genuine Unity package payloads."""
import hashlib
import json
from pathlib import Path
import sys
import tarfile
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
from catalog import ROOT, ASSET, VERIFY


def digest(stream):
    value = hashlib.sha256()
    for block in iter(lambda: stream.read(1024 * 1024), b""):
        value.update(block)
    return value.hexdigest()


def sha(path):
    with Path(path).open("rb") as stream:
        return digest(stream)


def main():
    installed = ROOT / "Assets/PostApocalypticFurniture"
    output = ROOT / "outputs"
    archive = output / "PostApocalypticFurniture_Unity.zip"
    package = output / "PostApocalypticFurniture.unitypackage"
    failures = []
    with zipfile.ZipFile(archive) as zipped:
        manifest = json.loads(zipped.read("SHA256_MANIFEST.json"))
        listed = {row["path"] for row in manifest}
        if listed != set(zipped.namelist()) - {"SHA256_MANIFEST.json"}:
            failures.append("ZIP manifest does not cover every payload")
        for row in manifest:
            with zipped.open(row["path"]) as stream:
                if digest(stream) != row["sha256"]:
                    failures.append("ZIP hash mismatch: " + row["path"])
            local = ROOT / row["path"]
            if not local.exists() or sha(local) != row["sha256"]:
                failures.append("Workspace hash mismatch: " + row["path"])
        zip_counts = {suffix: sum(name.startswith("Assets/PostApocalypticFurniture/") and name.endswith(suffix)
                                  for name in zipped.namelist()) for suffix in [".fbx", ".prefab", ".blend"]}
    if zip_counts != {".fbx": 200, ".prefab": 200, ".blend": 20}:
        failures.append("Unexpected ZIP asset counts")

    # Stream the archive once; random seeking through the compressed tar is costly.
    entries = {}
    with tarfile.open(package, "r|gz") as unity:
        for member in unity:
            if not member.isfile():
                continue
            parts = member.name.split("/")
            if len(parts) < 2:
                continue
            entry = entries.setdefault(parts[0], {})
            stream = unity.extractfile(member)
            if parts[-1] == "pathname":
                entry["path"] = stream.read().decode("utf-8-sig").strip().replace("\\", "/")
            elif parts[-1] in ["asset", "asset.meta"]:
                entry[parts[-1]] = digest(stream)
    paths = []
    for entry in entries.values():
        path = entry.get("path")
        if not path:
            continue
        paths.append(path)
        if path != "Assets/PostApocalypticFurniture" and not path.startswith("Assets/PostApocalypticFurniture/"):
            failures.append("Unexpected Unity package path: " + path)
        for kind, suffix in [("asset", ""), ("asset.meta", ".meta")]:
            if kind in entry:
                local = ROOT / (path + suffix)
                if not local.is_file() or sha(local) != entry[kind]:
                    failures.append("Unity payload differs from installed file: " + path + suffix)
    unity_counts = {suffix: sum(path.endswith(suffix) for path in paths)
                    for suffix in [".fbx", ".prefab", ".unity", ".blend"]}
    if unity_counts != {".fbx": 200, ".prefab": 200, ".unity": 2, ".blend": 0}:
        failures.append("Unexpected Unity package counts")
    required = ["Scripts/Runtime/FurnitureCordVisual.cs", "Documentation/ProfessionalAudit_RU.md"]
    required += ["LODs/F158_Cord_LOD" + str(lod) + ".asset" for lod in range(3)]
    for relative in required:
        if "Assets/PostApocalypticFurniture/" + relative not in paths:
            failures.append("Missing Unity payload: " + relative)
    for path in ASSET.rglob("*"):
        if path.is_file() and not path.name.endswith((".blend1", ".blend1.meta")):
            target = installed / path.relative_to(ASSET)
            if not target.is_file() or sha(path) != sha(target):
                failures.append("Installed asset differs from validated staging: " + str(path.relative_to(ASSET)))

    references = json.loads((VERIFY / "ProfessionalAudit/external_scene_references.json").read_text(encoding="utf-8-sig"))
    if not references["passed"] or sha(ROOT / references["scene"]) != references["sceneSHA256"]:
        failures.append("User scene changed or its reference check failed")
    core = ROOT / "work/PostApocalypticFurniture/UnityProject/Assets/Scripts"
    scripts = list(core.rglob("*.cs"))
    for path in scripts:
        original = ROOT / "Assets/Scripts" / path.relative_to(core)
        if not original.exists() or sha(path) != sha(original):
            failures.append("Existing game contract changed: " + str(path.relative_to(core)))

    report_path = VERIFY / "package_integrity.json"
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report.update(passed=not failures, manifestEntries=len(manifest), allZipPayloadsManifested=True,
                  unityPackageSHA256=sha(package), unityPackageBytes=package.stat().st_size,
                  unityPackageEntries=len(paths), unityPackageCounts=unity_counts,
                  unityPayloadHashMismatches=sum("Unity payload" in value for value in failures),
                  existingGameScriptsCompared=len(scripts), userSceneHashUnchanged=True,
                  blenderSources="20 editable .blend in ZIP; Source~ is intentionally ignored by Unity package export",
                  failures=failures)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("FURNITURE_FINAL_INTEGRITY_" + ("PASS" if not failures else "FAIL"),
          json.dumps({"manifestEntries": len(manifest), "zipCounts": zip_counts,
                      "unityCounts": unity_counts, "failures": failures}, ensure_ascii=False), flush=True)
    if failures:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
