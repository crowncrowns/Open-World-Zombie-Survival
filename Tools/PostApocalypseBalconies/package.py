#!/usr/bin/env python3
"""Package the verified balcony kit and write an integrity manifest."""

from __future__ import annotations

import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "outputs" / "PostApocalypticBalconies_Unity.zip"
VERIFY = ROOT / "Verification" / "PostApocalypseBalconies"
MANIFEST = VERIFY / "package_manifest.json"
INTEGRITY = VERIFY / "package_integrity.json"
INCLUDED = [
    ROOT / "Assets" / "Environment" / "PostApocalypse" / "Balconies",
    ROOT / "Assets" / "Environment" / "PostApocalypse" / "Balconies.meta",
    ROOT / "Docs" / "PostApocalypticBalconies.md",
    ROOT / "Tools" / "PostApocalypseBalconies",
    VERIFY,
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def included_files() -> list[Path]:
    files: set[Path] = set()
    for item in INCLUDED:
        if not item.exists():
            raise FileNotFoundError(item)
        if item.is_file():
            files.add(item)
            continue
        for path in item.rglob("*"):
            if not path.is_file() or "__pycache__" in path.parts or path.suffix == ".pyc":
                continue
            if path in (MANIFEST, INTEGRITY):
                continue
            files.add(path)
    return sorted(files, key=lambda p: p.relative_to(ROOT).as_posix().lower())


def main() -> None:
    if OUTPUT.exists() and "--force" not in sys.argv[1:]:
        raise FileExistsError(f"Refusing to overwrite existing archive: {OUTPUT}")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    VERIFY.mkdir(parents=True, exist_ok=True)

    files = included_files()
    entries = [
        {
            "path": path.relative_to(ROOT).as_posix(),
            "size": path.stat().st_size,
            "sha256": sha256(path),
        }
        for path in files
    ]
    manifest = {
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "archive": OUTPUT.relative_to(ROOT).as_posix(),
        "fileCount": len(entries),
        "payloadBytes": sum(entry["size"] for entry in entries),
        "entries": entries,
    }
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    with ZipFile(OUTPUT, "w", compression=ZIP_DEFLATED, compresslevel=6) as archive:
        for path in files:
            archive.write(path, path.relative_to(ROOT).as_posix())
        archive.write(MANIFEST, MANIFEST.relative_to(ROOT).as_posix())

    with ZipFile(OUTPUT, "r") as archive:
        bad_member = archive.testzip()
        if bad_member is not None:
            OUTPUT.unlink(missing_ok=True)
            raise IOError(f"ZIP CRC check failed for {bad_member}")
        archive_count = len(archive.namelist())

    integrity = {
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "archive": OUTPUT.relative_to(ROOT).as_posix(),
        "archiveBytes": OUTPUT.stat().st_size,
        "archiveSha256": sha256(OUTPUT),
        "manifestSha256": sha256(MANIFEST),
        "payloadFileCount": len(entries),
        "archiveEntryCount": archive_count,
        "payloadBytes": manifest["payloadBytes"],
        "zipCrcCheck": "PASS",
    }
    INTEGRITY.write_text(json.dumps(integrity, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(integrity, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
