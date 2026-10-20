"""Reproducible, project-owned Kurs PBR surfaces. Run with the runtime Python.

Uses only NumPy/Pillow, no downloaded imagery. Existing complete PNG sets are
validated and retained by default; --force regenerates the set from fixed seeds.
The building UVs repeat every two metres; Unity's 0.5 tiling gives four metres.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


FAMILIES = {
    "BluePaint": 1024,
    "CreamConcrete": 1024,
    "RustedSteel": 1024,
    "RoofMembrane": 1024,
    "FloorConcrete": 1024,
    "TarpGreen": 512,
    "SandCanvas": 512,
    "WornWood": 512,
}


def noise(size, grid, rng):
    """Periodic cubic-interpolated value noise, including across tile edges."""
    gy, gx = (grid, grid) if isinstance(grid, int) else grid
    values = rng.random((gy, gx), dtype=np.float32)
    tx = np.arange(size, dtype=np.float32) * gx / size
    ty = np.arange(size, dtype=np.float32) * gy / size
    ix, iy = np.floor(tx).astype(int), np.floor(ty).astype(int)
    fx, fy = tx - ix, ty - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    low = values[iy[:, None] % gy, ix[None, :] % gx] * (1 - fx)[None, :]
    low += values[iy[:, None] % gy, (ix[None, :] + 1) % gx] * fx[None, :]
    high = values[(iy[:, None] + 1) % gy, ix[None, :] % gx] * (1 - fx)[None, :]
    high += values[(iy[:, None] + 1) % gy, (ix[None, :] + 1) % gx] * fx[None, :]
    return low * (1 - fy)[:, None] + high * fy[:, None]


def fields(size, seed):
    rng = np.random.default_rng(seed)
    broad = noise(size, 5, rng)
    medium = noise(size, 23, rng)
    fine = noise(size, 131, rng)
    grain = rng.random((size, size), dtype=np.float32)
    streaks = noise(size, (3, 95), rng)
    return rng, broad, medium, fine, grain, streaks


def rgb(base, variation):
    return np.clip(np.asarray(base)[None, None, :] + variation[..., None], 0, 1)


def normal(height, strength):
    # PNG rows run downwards, opposite tangent +V. Hence +dh/drow in green.
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * strength
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * strength
    result = np.stack((-dx, dy, np.ones_like(height)), axis=-1)
    result /= np.linalg.norm(result, axis=-1, keepdims=True)
    return result * .5 + .5


def mask(metallic, smoothness, size):
    result = np.zeros((size, size, 4), dtype=np.float32)
    result[..., 0] = metallic
    result[..., 3] = smoothness
    return result


def surface(name, size):
    seed = int.from_bytes(hashlib.sha256(name.encode("ascii")).digest()[:4], "big")
    rng, broad, mid, fine, grain, streak = fields(size, seed)
    variation = .045 * (broad - .5) + .025 * (mid - .5) + .022 * (fine - .5)
    if name == "BluePaint":
        chips = np.clip((noise(size, 149, rng) - .84) * 7, 0, 1)
        dirt = np.clip((streak - .45) * .15, 0, .08)
        color = rgb((.055, .26, .46), variation - dirt)
        color = color * (1 - chips[..., None]) + np.array((.24, .15, .09)) * chips[..., None]
        height = fine * .14 + grain * .025 - chips * .12
        metallic, smoothness = .05 + chips * .48, .36 - dirt * 2 - chips * .22
    elif name == "CreamConcrete":
        dirt = np.clip((streak - .42) * .18, 0, .13)
        pits = np.clip((grain - .97) * 10, 0, .3)
        color = rgb((.70, .675, .60), variation - dirt - pits * .16)
        height = fine * .22 + grain * .08 - pits * .13
        metallic, smoothness = 0, .11 + mid * .055
    elif name == "RustedSteel":
        rust = np.clip((broad * .5 + mid * .4 + fine * .1 - .34) * 2.7, 0, 1)
        dark = rgb((.17, .185, .18), variation)
        oxide = rgb((.40, .235, .13), variation * 1.3)
        color = dark * (1 - rust[..., None]) + oxide * rust[..., None]
        height = fine * .18 + grain * .06 + rust * .12
        metallic, smoothness = .78 - rust * .69, .39 - rust * .28
    elif name == "RoofMembrane":
        wet = np.clip((broad - .58) * 1.8, 0, .55)
        color = rgb((.28, .285, .29), variation - wet * .085)
        height = fine * .15 + grain * .06
        metallic, smoothness = 0, .12 + mid * .05
    elif name == "FloorConcrete":
        color = rgb((.42, .43, .42), variation * .3 + (grain - .5) * .012)
        height = fine * .05 + grain * .025
        metallic, smoothness = 0, .14 + mid * .03
    elif name in ("TarpGreen", "SandCanvas"):
        rows, cols = np.mgrid[:size, :size]
        weave = (np.sin(cols * np.pi / 2) + np.sin(rows * np.pi / 2)) * .003
        base = (.20, .265, .185) if name == "TarpGreen" else (.53, .48, .35)
        color = rgb(base, variation + weave)
        height = broad * .035 + weave * 3
        metallic, smoothness = 0, .09
    elif name == "WornWood":
        rows, cols = np.mgrid[:size, :size]
        phase = cols * (np.pi * 2 * 43 / size) + np.sin(rows * np.pi * 2 / size) * 1.2
        fibres = (np.sin(phase) * .5 + .5) ** 10
        grooves = noise(size, (3, 190), rng)
        color = rgb((.37, .30, .21), variation - fibres * .10 - grooves * .055)
        height = grooves * .12 - fibres * .12
        metallic, smoothness = 0, .14
    else:
        raise ValueError(name)
    return {"BaseColor": color, "Normal": normal(height, 1.2),
            "MetallicSmoothness": mask(metallic, smoothness, size)}


def overlays(size=1024):
    rng, broad, mid, fine, grain, streak = fields(size, 734167)
    glass = np.empty((size, size, 4), dtype=np.float32)
    dirty = np.clip((streak - .46) * .8 + (mid - .5) * .05, 0, .4)
    glass[..., :3] = rgb((.56, .65, .66), .035 * (broad - .5) - dirty * .23)
    glass[..., 3] = .25 + dirty
    # Lower wall dirt: image top is clean, bottom is dirty. Horizontal wrapping
    # is periodic; vertical wrapping must be clamped by the Unity importer.
    bottom = np.linspace(0, 1, size, dtype=np.float32)[:, None]
    narrow = np.clip((streak - .58) * 3, 0, 1)
    length = noise(size, (1, 83), rng) * .62 + .18
    trail = np.clip((bottom - length) * 3, 0, 1) * narrow
    dirt = np.empty((size, size, 4), dtype=np.float32)
    dirt[..., :3] = rgb((.135, .125, .103), .025 * (fine - .5))
    dirt[..., 3] = trail * .76 + np.clip((bottom - .95) * 4, 0, .14)
    return {"GlassGrime_BaseColor.png": glass, "DirtStreaks_BaseColor.png": dirt}


def save(array, path):
    pixels = np.rint(np.clip(array, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(pixels).save(path, optimize=True)


def verify_and_report(folder):
    expected = {f"{name}_{kind}.png": (size, 4 if kind == "MetallicSmoothness" else 3)
                for name, size in FAMILIES.items() for kind in ("BaseColor", "Normal", "MetallicSmoothness")}
    expected.update({"GlassGrime_BaseColor.png": (1024, 4), "DirtStreaks_BaseColor.png": (1024, 4)})
    records = []
    for filename, (size, channels) in expected.items():
        path = folder / filename
        with Image.open(path) as source:
            data = np.asarray(source)
        if data.shape != (size, size, channels) or data.dtype != np.uint8:
            raise ValueError(f"Unexpected texture format: {path}: {data.shape}, {data.dtype}")
        if "MetallicSmoothness" in filename and np.any(data[..., 1:3]):
            raise ValueError(f"Metallic/smoothness mask must have G=B=0: {path}")
        if "_Normal" in filename and (np.min(data[..., 2]) < 128 or abs(data[..., :2].mean() - 127.5) > 3):
            raise ValueError(f"Expected tangent-space +Y normals: {path}")
        records.append({"file": filename, "size": [size, size], "channels": channels,
                        "channel_min": data.min(axis=(0, 1)).tolist(), "channel_max": data.max(axis=(0, 1)).tolist(),
                        "channel_mean": np.round(data.mean(axis=(0, 1)), 3).tolist(),
                        "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    report = {"version": 1, "generator": "Tools/KursHub/build_surface_textures.py",
              "normal_convention": "tangent +Y", "uv_period_metres": 4,
              "metallic_smoothness_channels": {"R": "metallic", "A": "smoothness", "G": 0, "B": 0},
              "wrap": {"default": "repeat XY", "DirtStreaks_BaseColor.png": "repeat U, clamp V; PNG top clean, bottom dirty"},
              "surfaces": records}
    (folder / "SurfaceTextures.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    names = ["BluePaint", "CreamConcrete", "RustedSteel", "RoofMembrane", "GlassGrime",
             "TarpGreen", "SandCanvas", "WornWood", "DirtStreaks"]
    sheet = Image.new("RGB", (1168, 966), (31, 34, 35))
    draw = ImageDraw.Draw(sheet)
    for index, name in enumerate(names):
        x, y = index % 4 * 292 + 6, index // 4 * 322 + 6
        with Image.open(folder / f"{name}_BaseColor.png") as source:
            preview = source.convert("RGBA").resize((280, 280), Image.Resampling.LANCZOS)
        background = Image.new("RGBA", preview.size, (75, 91, 98, 255))
        sheet.paste(Image.alpha_composite(background, preview).convert("RGB"), (x, y))
        draw.text((x, y + 289), name, fill=(210, 214, 215))
    sheet.save(folder / "SurfaceContactSheet.png", optimize=True)
    return records


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--force", action="store_true", help="Replace generated PNGs using fixed seeds")
    args = parser.parse_args()
    folder = args.root.resolve() / "Assets/Environment/KursHub/Textures"
    folder.mkdir(parents=True, exist_ok=True)
    for name, size in FAMILIES.items():
        paths = [folder / f"{name}_{kind}.png" for kind in ("BaseColor", "Normal", "MetallicSmoothness")]
        if args.force or not all(path.exists() for path in paths):
            for kind, array in surface(name, size).items():
                save(array, folder / f"{name}_{kind}.png")
    overlay_names = ("GlassGrime_BaseColor.png", "DirtStreaks_BaseColor.png")
    if args.force or not all((folder / name).exists() for name in overlay_names):
        for filename, array in overlays().items():
            save(array, folder / filename)
    records = verify_and_report(folder)
    print(f"KURS_SURFACES_OK: {len(records)} validated maps in {folder}")


if __name__ == "__main__":
    main()
