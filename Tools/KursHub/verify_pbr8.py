"""Read actual Kurs PBR PNGs; optionally refresh only the manifest's map index.

This verifier never regenerates a pixel, exports geometry or launches Unity.
Physical height-to-normal reconstruction follows each generator's own rule;
printed labels and the deliberately flat roof/film normals are not treated as
revision-7 rough surfaces. Results establish exchange-data consistency only.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import struct
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from PIL import Image


PROJECT = Path(__file__).resolve().parents[2]
WORK = PROJECT / "work/KursHub/audit8"
SUFFIXES = ("BaseColor", "Normal", "Roughness", "Metallic", "Occlusion",
            "Height", "MetallicSmoothness")
LEGACY_EXTRA = {"DirtStreaks_BaseColor.png"}
FLAT_NORMAL = {"Hub8LabelsA", "Hub8LabelsB", "Hub8SkylightGlass",
               "Hub8SkylightFrame", "Hub8FloorWear"}
SIGN_SIZE = {
    "Hub8SignKurs": (4.5, 6.), "Hub8SignWeapons": (4., .95),
    "Hub8SignMedical": (4., 1.3), "Hub8SignGear": (4., 1.25),
    "Hub8SignRepair": (3., 1.5), "Hub8SignCheckpoint": (6., 1.4),
    "Hub8SignRules": (2.6, 1.6),
}


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def json_hash(value):
    return hashlib.sha256(json.dumps(value, ensure_ascii=False, sort_keys=True,
                                    separators=(",", ":")).encode("utf8")).hexdigest()


def read_png(path):
    raw = path.read_bytes()
    assert raw[:8] == b"\x89PNG\r\n\x1a\n" and raw[12:16] == b"IHDR", path
    width, height, depth, color, compression, filtering, interlace = struct.unpack(
        ">IIBBBBB", raw[16:29])
    with Image.open(path) as im:
        im.load()
        pixels = np.asarray(im).copy()
        mode = im.mode
    assert pixels.shape[:2] == (height, width), path
    record = dict(file=path.name, resolution=[width, height], mode=mode,
                  min=np.min(pixels, axis=(0, 1)).tolist(),
                  max=np.max(pixels, axis=(0, 1)).tolist(),
                  sha256=hashlib.sha256(raw).hexdigest(), bytes=len(raw),
                  pngBitDepth=depth, pngColorType=color, dtype=str(pixels.dtype))
    return pixels, record


def normals_from_height(height, period_y, period_x):
    n = height.shape[0]
    dx = (np.roll(height, -1, 1)-np.roll(height, 1, 1))*(n/(2*period_x))
    dy = (np.roll(height, -1, 0)-np.roll(height, 1, 0))*(n/(2*period_y))
    normal = np.stack((-dx, dy, np.ones_like(height)), axis=2)
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    return normal


def verify_family(folder, spec):
    name = spec["name"]
    n = spec["resolution"]
    px, py = spec["periodMetres"], spec["periodYMetres"]
    assert isinstance(n, int) and n > 0 and math.isfinite(px) and px > 0
    assert math.isfinite(py) and py > 0
    assert math.isfinite(spec["tiling"]) and spec["tiling"] > 0
    assert math.isfinite(spec["normalStrength"]) and spec["normalStrength"] >= 0
    lo, hi = spec["heightMinMetres"], spec["heightMaxMetres"]
    assert math.isfinite(lo) and math.isfinite(hi) and lo <= hi
    data, records = {}, []
    for suffix in SUFFIXES:
        pixels, record = read_png(folder/f"{name}_{suffix}.png")
        assert record["resolution"] == [n, n], (name, suffix, "resolution")
        if suffix == "Height":
            assert record["pngBitDepth"] == 16 and record["pngColorType"] == 0, (name, "height must be actual 16-bit grayscale PNG")
            assert pixels.dtype == np.uint16 and record["mode"] in ("I;16", "I;16B"), (name, record)
        else:
            assert record["pngBitDepth"] == 8 and pixels.dtype == np.uint8, (name, suffix, "byte channels")
            expected = ("RGB", "RGBA") if suffix == "BaseColor" else (("RGB",) if suffix == "Normal" else (("RGBA",) if suffix == "MetallicSmoothness" else ("L",)))
            assert record["mode"] in expected, (name, suffix, record["mode"], expected)
        data[suffix] = pixels
        records.append(record)
    if spec["alpha"]:
        assert data["BaseColor"].ndim == 3 and data["BaseColor"].shape[2] == 4, (name, "missing actual alpha")
    elif data["BaseColor"].shape[-1] == 4:
        assert np.all(data["BaseColor"][:, :, 3] == 255), (name, "unregistered translucency")

    packed = data["MetallicSmoothness"]
    metallic_error = np.abs(packed[:, :, 0].astype(np.int16)-data["Metallic"].astype(np.int16))
    smoothness_error = np.abs(packed[:, :, 3].astype(np.int16)-(255-data["Roughness"].astype(np.int16)))
    assert metallic_error.max() <= 1 and smoothness_error.max() <= 1, (name, "packed R/A disagree with exchange maps")
    assert not np.any(packed[:, :, 1:3]), (name, "packed G/B must remain zero")
    raw_normal = data["Normal"].astype(np.float32)/127.5-1
    length = np.linalg.norm(raw_normal, axis=2)
    norm_error = np.abs(length-1)
    assert np.isfinite(raw_normal).all() and np.isfinite(length).all()
    # The ceramic generator deliberately converts with uint8 truncation;
    # audit7/sign write_png uses nearest-integer rounding. A truncated channel
    # can differ by <2/255 in [-1,1], a rounded channel by <=1/255. Bound the
    # resulting vector-length error by the Euclidean component-error bound.
    truncated = name == "CeramicTile"
    normal_tolerance = math.sqrt(3)*(2 if truncated else 1)/255+1e-6
    assert norm_error.max() <= normal_tolerance, (name, "normals not unit within generator byte quantization", float(norm_error.max()))
    assert data["Normal"][:, :, 2].min() >= 128, (name, "negative tangent Z")
    height = data["Height"].astype(np.float32)/65535*(hi-lo)+lo
    assert np.isfinite(height).all()
    contract = "height-derived OpenGL +Y normal; positive PNG row derivative in green"
    if name in FLAT_NORMAL:
        assert np.all(data["Normal"] == np.array((128, 128, 255), np.uint8)), (name, "generator declares flat normal")
        error = 0
        contract = "Generator explicitly writes flat (128,128,255); exchange height is not runtime displacement"
        if name == "Hub8SkylightFrame":
            assert hi-lo < .00002 and spec["normalStrength"] == .35
            contract += "; frame stores <=20 micrometres of exchange-only height independently of flat normal"
        else:
            assert lo == hi == 0 and np.ptp(data["Height"]) == 0, (name, "flat-height contract")
    else:
        expected = normals_from_height(height, py, px)
        encoded = np.clip(expected*.5+.5, 0, 1)*255
        expected_pixels = (np.floor(encoded) if truncated else np.rint(encoded)).astype(np.int16)
        delta = np.abs(expected_pixels-data["Normal"].astype(np.int16))
        error = int(delta.max())
        assert error <= 2, (name, "height/normal basis or scale mismatch", error)
    unique = (name.startswith(("Rug", "Hub8Labels", "Hub8Sign"))
              or name in ("LeafGreen", "FirNeedles", "Hub8FloorWear"))
    if unique:
        assert spec["tiling"] == 1, (name, "unique UV must not repeat")
    else:
        assert abs(spec["tiling"]-2/px) < 1e-9, (name, "metres/2 UV physical period")
    if name in SIGN_SIZE:
        assert (px, py) == SIGN_SIZE[name] and spec["normalStrength"] == 1
        assert lo == -.00005 and hi == .00005 and spec["emission"] is False
        assert "Unique" in spec["uv"] and "mirrored" in spec["uv"]
    if name.startswith("Hub8Labels"):
        assert spec["uniqueUV"] and (px, py) == (1, 1) and spec["normalStrength"] == 1
    if name == "Hub8SkylightGlass":
        assert spec["normalStrength"] == .12 and (px, py) == (2, 2)
        alpha = data["BaseColor"][:, :, 3].astype(np.float32)/255
        assert alpha.min() >= .085 and alpha.max() <= .175, (name, "roof pane opacity contract")
    if name == "Hub8FloorWear":
        assert spec["uniqueUV"] and (px, py) == (2, 2) and spec["normalStrength"] == 1
    if name == "CeramicTile":
        assert n == 4096 and (px, py) == (8, 8) and spec["tiling"] == .25
    return records, dict(name=name, resolution=n, normalRule=contract,
                         normalMaximumLengthError=float(norm_error.max()),
                         normalEncoding="uint8 truncation" if truncated else "nearest-integer uint8 rounding / explicit flat normal",
                         allowedNormalLengthQuantizationError=normal_tolerance,
                         heightNormalMaximumByteDifference=error,
                         reconstructedHeightMinMetres=float(height.min()),
                         reconstructedHeightMaxMetres=float(height.max()),
                         metallicPackedMaximumByteDifference=int(metallic_error.max()),
                         smoothnessPackedMaximumByteDifference=int(smoothness_error.max()),
                         packedChannelsVerified=True,
                         uniqueUV=unique, declaredNormalStrength=spec["normalStrength"],
                         physicalPeriodMetres=[px, py], tiling=spec["tiling"])


def run(root, report_path, refresh=False):
    root, report_path = root.resolve(), report_path.resolve()
    assert root != PROJECT, "Use a separate staging root"
    folder = root/"Assets/Environment/KursHub/Textures"
    manifest_path = folder/"Audit7PBRManifest.json"
    original_bytes = manifest_path.read_bytes()
    manifest = json.loads(original_bytes)
    original_other = copy.deepcopy({k: v for k, v in manifest.items() if k != "maps"})
    families = manifest["families"]
    names = [s["name"] for s in families]
    assert len(names) == len(set(names)), "Duplicate material family"
    expected = {f"{name}_{suffix}.png" for name in names for suffix in SUFFIXES}
    actual = {p.name for p in folder.glob("*.png")}
    errors, records, family_proof = [], [], []
    if expected-actual:
        errors.append(dict(issue="Missing registered maps", files=sorted(expected-actual)))
    if actual-expected != LEGACY_EXTRA:
        errors.append(dict(issue="Unexpected/missing retained non-family images", files=sorted(actual-expected)))
    for spec in families:
        try:
            maps, proof = verify_family(folder, spec)
            records.extend(maps)
            family_proof.append(proof)
            print("PBR_READBACK_OK", spec["name"], flush=True)
        except Exception as exc:
            errors.append(dict(family=spec["name"], issue=repr(exc)))
            print("PBR_READBACK_FAILED", spec["name"], repr(exc), flush=True)
    legacy = []
    for name in sorted(actual-expected):
        pixels, record = read_png(folder/name)
        records.append(record)
        legacy.append(dict(file=name, role="Retained legacy alpha dirt image; not falsely registered as a seven-map PBR family", sha256=record["sha256"]))
    records.sort(key=lambda r: r["file"])
    if not errors:
        assert {r["file"] for r in records} == actual and len(records) == len(actual)
        assert all(sha(folder/r["file"]) == r["sha256"] for r in records), "Pixel file changed during readback"
        assert manifest_path.read_bytes() == original_bytes, "Concurrent PBR manifest edit; no write performed"
        if refresh:
            report_path.parent.mkdir(parents=True, exist_ok=True)
            backup = report_path.with_name("PBRManifest8_before_readback.json")
            if not backup.exists():
                backup.write_bytes(original_bytes)
            manifest["maps"] = records
            assert {k: v for k, v in manifest.items() if k != "maps"} == original_other
            temporary = manifest_path.with_name(manifest_path.name+".readback.tmp")
            temporary.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+"\n", encoding="utf8")
            temporary.replace(manifest_path)
            after = json.loads(manifest_path.read_text(encoding="utf8"))
            assert {k: v for k, v in after.items() if k != "maps"} == original_other
    ramp = np.repeat(np.arange(64, dtype=np.float32)[:, None], 64, axis=1)*.00001
    assert float(normals_from_height(ramp, 1, 1)[1:-1, :, 1].mean()) > 0
    digest = hashlib.sha256()
    for record in records:
        digest.update((record["file"]+"\0"+record["sha256"]+"\n").encode("utf8"))
    report = dict(status="PBR_READBACK8_PASS" if not errors else "PBR_READBACK8_FAILED",
                  generatedUtc=datetime.now(timezone.utc).isoformat(),
                  root=str(root), families=len(families), expectedFamilyMaps=len(expected),
                  actualPngFiles=len(actual), mapsRecordsBefore=len(json.loads(original_bytes).get("maps", [])),
                  mapsRecordsAfter=len(manifest.get("maps", [])),
                  manifestBeforeSHA256=hashlib.sha256(original_bytes).hexdigest(),
                  manifestAfterSHA256=sha(manifest_path),
                  pbrManifestSha256=sha(manifest_path),
                  familySpecsSHA256=json_hash(families),
                  pixelLibrarySHA256=digest.hexdigest(),
                  allNonMapManifestFieldsUnchanged=True, pixelFilesModified=False,
                  manifestMapsRefreshed=bool(refresh and not errors),
                  normalConvention="Tangent +Y / OpenGL; positive PNG row height derivative gives positive green; no green flip",
                  analyticRampGreenPositive=True,
                  allowedNormalLengthQuantizationError="Per-family Euclidean bound: sqrt(3)/255 for rounding, 2*sqrt(3)/255 for declared ceramic truncation",
                  allowedHeightNormalByteDifference=2,
                  maps=records, familyReadback=family_proof, familyResults=family_proof,
                  packedChannelsVerified=not errors and len(family_proof)==len(families),
                  retainedLegacyImages=legacy,
                  verifierSha256=sha(Path(__file__)),
                  generatorSourceSha256={name:sha(PROJECT/"Tools/KursHub"/name) for name in
                     ("audit7_surfaces.py", "audit7_leaf_surface.py", "audit7_fir_surface.py",
                      "audit8_ceramic.py", "audit8_label_atlases.py", "audit8_signs.py",
                      "audit8_skylight_surfaces.py", "audit8_floor_wear.py")},
                  errors=errors, provenance="Original procedural textures and original typography; not photographic scans or photogrammetry",
                  limitations=["Numerical consistency does not establish visual realism, AAA quality or reference matching.",
                               "Local procedural AO is not a bake of all scene geometry; no directional-light decomposition was inferred from pixels.",
                               "Height is exchange data only; these checks do not claim Unity displacement/parallax.",
                               "Unity shader hookup, alpha sorting, material/UV appearance, lighting and FPS require independent target-engine checks."])
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2)+"\n", encoding="utf8")
    print(report["status"], len(families), "families", len(actual), "PNGs", len(errors), "errors", flush=True)
    if errors:
        raise RuntimeError("Actual PNG readback failed; see "+str(report_path))
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=WORK/"authoring")
    parser.add_argument("--report", type=Path, default=WORK/"reports/PBR_Readback8.json")
    parser.add_argument("--refresh-maps", action="store_true")
    args = parser.parse_args()
    run(args.root, args.report, args.refresh_maps)
