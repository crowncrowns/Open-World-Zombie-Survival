<<<<<<< Updated upstream
"""Reproducible, project-owned Kurs PBR surfaces. Run with the runtime Python.

Uses only NumPy/Pillow, no downloaded imagery. Existing complete PNG sets are
validated and retained by default; --force regenerates the set from fixed seeds.
The building UVs repeat every two metres; Unity's 0.5 tiling gives four metres.
=======
"""Deterministic, periodic PBR surfaces for the authored Kurs hub.

Uses only analytic noise and weave/grain patterns, without external images.
The default target is staging; pass --root to choose another project root.
Normal maps are tangent-space +Y. MetallicSmoothness stores metal in R and
smoothness in A for URP/Lit. All dimensions and seeds are stable across runs.
>>>>>>> Stashed changes
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


<<<<<<< Updated upstream
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
=======
REPOSITORY = Path(__file__).resolve().parents[2]


def periodic_noise(rng, size, cells):
    """Smooth wrapped lattice; unequal cell counts make directional streaks."""
    cx, cy = (cells, cells) if isinstance(cells, int) else cells
    lattice = rng.random((cy, cx), dtype=np.float32)
    x = np.arange(size, dtype=np.float32) * cx / size
    y = np.arange(size, dtype=np.float32) * cy / size
    ix, iy = x.astype(int), y.astype(int)
    tx, ty = x - ix, y - iy
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a = lattice[iy[:, None] % cy, ix[None, :] % cx]
    b = lattice[iy[:, None] % cy, (ix[None, :] + 1) % cx]
    c = lattice[(iy[:, None] + 1) % cy, ix[None, :] % cx]
    d = lattice[(iy[:, None] + 1) % cy, (ix[None, :] + 1) % cx]
    return (a * (1 - tx) + b * tx) * (1 - ty[:, None]) + (c * (1 - tx) + d * tx) * ty[:, None]


def fbm(rng, size, frequencies=(3, 8, 22, 64, 180), weights=(.37, .27, .18, .12, .06)):
    return sum(periodic_noise(rng, size, f) * w for f, w in zip(frequencies, weights))


def smoothstep(low, high, value):
    t = np.clip((value - low) / (high - low), 0, 1)
    return t * t * (3 - 2 * t)


def color(base, variation):
    return np.asarray(base, dtype=np.float32)[None, None, :] + variation[..., None]


def blend(a, b, weight):
    return a * (1 - weight[..., None]) + np.asarray(b, dtype=np.float32) * weight[..., None]


def normal_map(height_m, tile_metres):
    # Wrapped central differences share the same physical texel spacing at seams.
    step = tile_metres / height_m.shape[0]
    dx = (np.roll(height_m, -1, axis=1) - np.roll(height_m, 1, axis=1)) / (2 * step)
    dy = (np.roll(height_m, -1, axis=0) - np.roll(height_m, 1, axis=0)) / (2 * step)
    normal = np.stack((-dx, dy, np.ones_like(dx)), axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    return normal * .5 + .5


def save_map(directory, name, pixels):
    pixels = np.rint(np.clip(pixels, 0, 1) * 255).astype(np.uint8)
    path = directory / name
    Image.fromarray(pixels).save(path, optimize=True)
    return {
        "file": name, "size": list(pixels.shape[:2][::-1]),
        "channels": pixels.shape[-1],
        "channel_min": pixels.min(axis=(0, 1)).tolist(),
        "channel_max": pixels.max(axis=(0, 1)).tolist(),
        "channel_mean": pixels.mean(axis=(0, 1)).round(3).tolist(),
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
    }


def save_surface(directory, name, albedo, height, metal, smoothness, tile_metres):
    n = albedo.shape[0]
    packed = np.zeros((n, n, 4), dtype=np.float32)
    packed[..., 0], packed[..., 3] = metal, smoothness
    return [
        save_map(directory, name + "_BaseColor.png", albedo),
        save_map(directory, name + "_Normal.png", normal_map(height, tile_metres)),
        save_map(directory, name + "_MetallicSmoothness.png", packed),
    ]


def blue_paint(directory):
    n, rng = 1024, np.random.default_rng(24001)
    broad, grain = fbm(rng, n), periodic_noise(rng, n, 220)
    streak = periodic_noise(rng, n, (70, 4))
    # Sparse, broken paint loss rather than a repeated round rust-spot pattern.
    damage = .58 * periodic_noise(rng, n, 75) + .42 * periodic_noise(rng, n, 190)
    chipped = smoothstep(.81, .91, damage) * smoothstep(.49, .67, broad)
    worn = smoothstep(.67, .82, damage) * .24
    base = color((.071, .263, .448), (broad - .5) * .115 + (grain - .5) * .018)
    base = blend(base, (.23, .32, .36), worn)
    base *= (1 - smoothstep(.58, .84, streak)[..., None] * .12)
    base = blend(base, (.29, .22, .16), chipped * .80)
    height = (grain - .5) * .00012 - chipped * .0006
    metal = .05 + chipped * .53
    smooth = np.clip(.34 + (grain - .5) * .06 - chipped * .21 - worn * .1, .07, .46)
    return save_surface(directory, "BluePaint", base, height, metal, smooth, 4)


def cream_concrete(directory):
    n, rng = 1024, np.random.default_rng(24002)
    broad, aggregate = fbm(rng, n), periodic_noise(rng, n, 300)
    streak = periodic_noise(rng, n, (44, 3))
    pits = smoothstep(.79, .94, periodic_noise(rng, n, 220))
    dirt = smoothstep(.57, .83, streak) * .35 + smoothstep(.61, .78, broad) * .2
    base = color((.727, .704, .624), (broad - .5) * .14 + (aggregate - .5) * .033)
    base = blend(base, (.41, .40, .35), dirt)
    base = blend(base, (.41, .41, .38), pits * .24)
    height = (aggregate - .5) * .0009 + (broad - .5) * .0013 - pits * .001
    return save_surface(directory, "CreamConcrete", base, height, 0, .11 + aggregate * .06, 4)


def rusted_steel(directory):
    n, rng = 1024, np.random.default_rng(24003)
    broad, fine = fbm(rng, n), periodic_noise(rng, n, 240)
    oxide = smoothstep(.35, .65, broad)
    base = color((.20, .215, .22), (fine - .5) * .065)
    rust = color((.36, .19, .105), (broad - .5) * .26 + (fine - .5) * .04)
    base = blend(base, rust, oxide)
    height = (fine - .5) * .00045 + oxide * .0008
    return save_surface(directory, "RustedSteel", base, height, .76 - oxide * .67, .39 - oxide * .29, 2)


def roof_membrane(directory):
    n, rng = 1024, np.random.default_rng(24004)
    broad, grit = fbm(rng, n), periodic_noise(rng, n, 340)
    base = color((.285, .295, .297), (broad - .5) * .095 + (grit - .5) * .026)
    damp = smoothstep(.61, .77, periodic_noise(rng, n, 6)) * .17
    base *= 1 - damp[..., None]
    height = (grit - .5) * .0012 + (broad - .5) * .001
    return save_surface(directory, "RoofMembrane", base, height, 0, .12 + grit * .05, 4)


def glass_grime(directory):
    n, rng = 1024, np.random.default_rng(24005)
    broad = fbm(rng, n)
    streak = periodic_noise(rng, n, (98, 3))
    x, y = np.meshgrid(np.arange(n) / n, np.arange(n) / n)
    # Two-metre repeat: a sparse periodic edge and organic vertical rain streaks.
    # No dark painted frame: the actual facade mullions remain geometry.
    edgex = np.minimum(x, 1 - x)
    edgey = np.minimum(y, 1 - y)
    edge = np.exp(-edgex * 95) * .42 + np.exp(-edgey * 120) * .18
    dirt = np.clip(edge + smoothstep(.60, .82, streak) * .40 + smoothstep(.64, .82, broad) * .1, 0, 1)
    rgba = np.empty((n, n, 4), dtype=np.float32)
    rgba[..., :3] = blend(color((.57, .67, .68), (broad - .5) * .025), (.34, .33, .28), dirt * .65)
    rgba[..., 3] = .23 + dirt * .62
    return [save_map(directory, "GlassGrime_BaseColor.png", rgba)]


def dirt_streaks(directory):
    n, rng = 1024, np.random.default_rng(24009)
    y = np.arange(n, dtype=np.float32)[:, None] / (n - 1)
    broad = fbm(rng, n)
    channels = periodic_noise(rng, n, (83, 3))
    trail = smoothstep(.60, .87, channels)
    starts = periodic_noise(rng, n, (20, 1)) * .48 + .19
    run_down = smoothstep(starts, np.minimum(starts + .19, .91), y)
    ground = smoothstep(.84, 1, y) * (.07 + broad * .12)
    alpha = np.clip(trail * run_down * .67 + ground, 0, .78)
    rgba = np.empty((n, n, 4), dtype=np.float32)
    rgba[..., :3] = color((.135, .126, .105), (broad - .5) * .045)
    rgba[..., 3] = alpha
    # Repeat U. Clamp V: a clean top and dirty foot deliberately differ.
    return [save_map(directory, "DirtStreaks_BaseColor.png", rgba)]


def fabric(directory, name, base_color, seed):
    n, rng = 512, np.random.default_rng(seed)
    broad = fbm(rng, n, (3, 9, 26, 110), (.43, .29, .19, .09))
    y, x = np.meshgrid(np.arange(n), np.arange(n), indexing="ij")
    # Analytic periodic weave has a tiny amplitude to avoid moire at distance.
    weave = (np.cos(x * np.pi) + np.cos(y * np.pi)) * .5
    wrinkles = periodic_noise(rng, n, (4, 32))
    base = color(base_color, (broad - .5) * .13 + weave * .007 + (wrinkles - .5) * .025)
    height = weave * .00006 + (wrinkles - .5) * .0004
    return save_surface(directory, name, base, height, 0, .09, 2)


def worn_wood(directory):
    n, rng = 512, np.random.default_rng(24008)
    broad = fbm(rng, n)
    grain = periodic_noise(rng, n, (120, 3))
    thin = periodic_noise(rng, n, (240, 5))
    dark = smoothstep(.58, .84, grain) * .10 + smoothstep(.71, .88, thin) * .08
    base = color((.40, .326, .234), (broad - .5) * .115 - dark)
    height = (grain - .5) * .001 + (thin - .5) * .0005
    return save_surface(directory, "WornWood", base, height, 0, .14, 2)


def contact_sheet(directory, records):
    sources = [r for r in records if r["file"].endswith("_BaseColor.png")]
    sheet = Image.new("RGB", (4 * 292, ((len(sources) + 3) // 4) * 322), (31, 33, 35))
    draw = ImageDraw.Draw(sheet)
    for i, rec in enumerate(sources):
        im = Image.open(directory / rec["file"])
        # Alpha composites onto neutral dark for reviewing the in-game glass dirt.
        bg = Image.new("RGBA", im.size, (75, 91, 99, 255))
        bg.alpha_composite(im.convert("RGBA"))
        bg = bg.convert("RGB").resize((280, 280), Image.Resampling.LANCZOS)
        px, py = (i % 4) * 292 + 6, (i // 4) * 322 + 6
        sheet.paste(bg, (px, py))
        draw.text((px, py + 288), rec["file"].replace("_BaseColor.png", ""), fill=(224, 224, 222))
    sheet.save(directory / "SurfaceContactSheet.png", optimize=True)
>>>>>>> Stashed changes


def main():
    parser = argparse.ArgumentParser(description=__doc__)
<<<<<<< Updated upstream
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
=======
    parser.add_argument("--root", type=Path, default=REPOSITORY / "work/KursHub/stage4/authoring")
    args = parser.parse_args()
    directory = args.root.resolve() / "Assets/Environment/KursHub/Textures"
    directory.mkdir(parents=True, exist_ok=True)
    records = []
    for maker in (blue_paint, cream_concrete, rusted_steel, roof_membrane, glass_grime):
        records += maker(directory)
    records += fabric(directory, "TarpGreen", (.205, .265, .183), 24006)
    records += fabric(directory, "SandCanvas", (.533, .483, .353), 24007)
    records += worn_wood(directory)
    records += dirt_streaks(directory)
    report = {"version": 1, "generator": "Tools/KursHub/build_surface_textures.py", "normal_convention": "tangent +Y", "metallic_smoothness_channels": {"R": "metallic", "A": "smoothness", "G": 0, "B": 0}, "wrap": {"default": "repeat XY", "DirtStreaks_BaseColor.png": "repeat U, clamp V; PNG top clean, bottom dirty"}, "surfaces": records}
    (directory / "SurfaceTextures.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    contact_sheet(directory, records)
    print(json.dumps({"output": str(directory), "maps": len(records), "total_bytes": sum((directory / r["file"]).stat().st_size for r in records)}, ensure_ascii=False))
>>>>>>> Stashed changes


if __name__ == "__main__":
    main()
