from __future__ import annotations

import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


SIZE = 4096


def noise_field(size: int, seed: int, octaves=(4, 10, 28, 72, 190, 500)) -> np.ndarray:
    rng = np.random.default_rng(seed)
    accum = np.zeros((size, size), dtype=np.float32)
    weight_sum = 0.0
    for i, cells in enumerate(octaves):
        grid_size = max(2, cells)
        small = rng.integers(0, 256, (grid_size, grid_size), dtype=np.uint8)
        image = Image.fromarray(small, mode="L").resize((size, size), Image.Resampling.BICUBIC)
        layer = np.asarray(image, dtype=np.float32) / 255.0
        weight = 0.47 ** i
        accum += (layer - 0.5) * weight
        weight_sum += weight
    accum /= max(weight_sum, 1e-6)
    return np.clip(accum * 2.2, -1.0, 1.0)


def scratch_field(size: int, seed: int, count: int = 1500, horizontal_bias: bool = False) -> np.ndarray:
    rng = np.random.default_rng(seed)
    image = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(image)
    for _ in range(count):
        x = int(rng.integers(0, size))
        y = int(rng.integers(0, size))
        length = int(rng.integers(max(8, size // 180), max(15, size // 18)))
        if horizontal_bias:
            angle = float(rng.normal(0.0, 0.10))
        else:
            angle = float(rng.uniform(-math.pi, math.pi))
        x2 = x + int(math.cos(angle) * length)
        y2 = y + int(math.sin(angle) * length)
        value = int(rng.integers(45, 220))
        draw.line((x, y, x2, y2), fill=value, width=int(rng.choice([1, 1, 1, 2])))
    return np.asarray(image.filter(ImageFilter.GaussianBlur(0.65)), dtype=np.float32) / 255.0


def wood_grain(size: int, seed: int) -> tuple[np.ndarray, np.ndarray]:
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    nx, ny = x / size, y / size
    turbulence = noise_field(size, seed + 44, (5, 15, 52, 210))
    warp = 4.0 * np.sin(nx * 9.0 + np.sin(nx * 21.0) * 0.5) + turbulence * 22.0
    phase = ny * (2.0 * math.pi * 105.0) + warp
    streak_image = Image.fromarray(rng.integers(0,256,(size,48),dtype=np.uint8)).resize((size,size),Image.Resampling.BICUBIC)
    streaks = np.asarray(streak_image,dtype=np.float32)/127.5-1.0
    fine = np.sin(phase) * 0.22 + streaks * 0.60
    broad = noise_field(size, seed + 3, (5, 13, 38, 125, 620)) * 0.58
    knots = np.zeros((size, size), dtype=np.float32)
    for _ in range(9):
        cx, cy = rng.uniform(0.02, 0.98, 2)
        rx, ry = rng.uniform(0.05, 0.14), rng.uniform(0.008, 0.025)
        dx = (nx - cx) / rx
        dy = (ny - cy) / ry
        radius = np.sqrt(dx * dx + dy * dy)
        rings = np.sin(radius * 18.0) * np.exp(-radius * 0.7)
        knots += rings.astype(np.float32) * np.exp(-radius * radius * 0.25).astype(np.float32) * 0.3
    grain = np.clip(fine * 0.33 + broad + knots, -0.85, 0.85)
    return grain.astype(np.float32), np.clip(0.5 + grain * 0.44, 0.0, 1.0).astype(np.float32)


def build_surface(kind: str, seed: int):
    n = SIZE
    rng = np.random.default_rng(seed)
    noise = noise_field(n, seed + 9)
    fine_noise = noise_field(n, seed + 83, (12, 64, 260, 900))
    scratches = scratch_field(n, seed + 17, 2200 if kind.startswith("wood") else 2700,
                              horizontal_bias=kind.startswith("wood"))
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)

    if kind.startswith("wood"):
        grain, grain_mask = wood_grain(n, seed + 31)
        if kind != "wood_raw":
            chips_noise = noise_field(n, seed + 111, (9, 23, 70, 210))
            chip_signal = chips_noise + fine_noise * 0.15 + scratches * 0.10
            chip = np.clip((chip_signal - 0.14) * 38.0, 0.0, 1.0)
            palette = {"wood_green": (0.25,0.32,0.17), "wood_blue": (0.22,0.34,0.43), "wood_brown": (0.34,0.22,0.12)}
            paint = np.array(palette[kind], dtype=np.float32)[None,None,:] * (1.0 + grain[...,None]*0.15 + noise[...,None]*0.12)
            raw = np.stack((0.43 + grain * 0.12, 0.34 + grain * 0.10, 0.23 + grain * 0.08), axis=2)
            color = paint * (1.0-chip[...,None]) + raw * chip[...,None]
            height = grain * (0.025 + chip*0.07) + fine_noise * 0.012 - scratches * 0.04 - chip * 0.06
            roughness = np.clip(0.72 + grain * 0.12 + noise * 0.10 - scratches * 0.10, 0.42, 0.96)
            metallic = np.zeros_like(noise)
        else:
            color = np.stack((0.45 + grain * 0.25, 0.37 + grain * 0.22, 0.28 + grain * 0.18), axis=2)
            color *= (0.88 + noise[..., None] * 0.12)
            color *= 1.0-scratches[...,None]*0.30
            height = grain * 0.12 + fine_noise * 0.035 - scratches * 0.05
            roughness = np.clip(0.78 + grain * 0.12 + noise * 0.09, 0.50, 0.98)
            metallic = np.zeros_like(noise)
    elif kind == "steel_painted":
        chips_noise = noise_field(n, seed + 211, (8, 19, 61, 200))
        chips = np.clip((chips_noise + 0.34) * 2.7, 0.0, 1.0)
        base = 0.52 + noise * 0.08 + fine_noise * 0.025
        rust = np.stack((0.39 + noise * 0.12, 0.18 + noise * 0.08, 0.09 + noise * 0.04), axis=2)
        paint = np.stack((base * 0.92, base * 0.98, base * 0.83), axis=2)
        color = paint * (1.0 - chips[..., None] * 0.58) + rust * (chips[..., None] * 0.58)
        height = fine_noise * 0.10 + scratches * 0.11 + chips * 0.15 + noise * 0.06
        roughness = np.clip(0.48 + chips * 0.27 + scratches * 0.12 + noise * 0.10, 0.26, 0.95)
        metallic = np.clip(scratches * 0.85, 0.0, 0.85)
    elif kind == "bare_steel":
        base = 0.54 + noise * 0.045 + fine_noise * 0.014
        color = np.stack((base * 0.96, base * 0.99, base), axis=2)
        height = fine_noise * 0.025 + scratches * 0.02
        roughness = np.clip(0.42 + noise * 0.12 + scratches * 0.1,0.28,0.7)
        metallic = np.ones_like(noise) * 0.92
    elif kind == "rusted_steel":
        oxide = noise_field(n, seed + 301, (8, 24, 83, 310))
        color = np.stack((0.40 + noise * 0.13 + oxide * 0.08,
                          0.19 + noise * 0.09 + oxide * 0.05,
                          0.09 + noise * 0.05 + oxide * 0.03), axis=2)
        color += scratches[..., None] * np.array([0.10, 0.095, 0.08], dtype=np.float32)
        height = oxide * 0.24 + fine_noise * 0.11 + scratches * 0.14
        roughness = np.clip(0.82 + noise * 0.10 - scratches * 0.15, 0.45, 1.0)
        metallic = np.clip(scratches * 0.75, 0.0, 0.75)
    elif kind == "cast_iron":
        oxide = noise_field(n, seed + 401, (6, 21, 66, 250))
        pits = scratch_field(n, seed + 499, 4200)
        base = 0.27 + noise * 0.08 + fine_noise * 0.025
        color = np.stack((base * 0.82 + np.maximum(oxide, 0) * 0.14,
                          base * 0.88 + np.maximum(oxide, 0) * 0.065,
                          base * 0.74 + np.maximum(oxide, 0) * 0.035), axis=2)
        height = noise * 0.12 + pits * 0.10 + oxide * 0.08
        roughness = np.clip(0.70 + pits * 0.18 + noise * 0.08, 0.42, 0.98)
        metallic = np.clip(0.60 - np.maximum(oxide, 0) * 0.24, 0.26, 0.72)
    else:  # porous concrete
        pores = noise_field(n, seed + 501, (16, 68, 210, 650, 1500))
        color = np.stack((0.47 + noise * 0.10 + pores * 0.035,
                          0.47 + noise * 0.10 + pores * 0.035,
                          0.44 + noise * 0.09 + pores * 0.030), axis=2)
        pinholes = scratch_field(n, seed + 509, 7000)
        height = pores * 0.20 + pinholes * 0.10 + fine_noise * 0.035
        roughness = np.clip(0.91 + noise * 0.06, 0.68, 1.0)
        metallic = np.zeros_like(noise)

    color = np.clip(color, 0.02, 1.0)
    # Encode layered height variation into a tangent-space normal map.
    gy, gx = np.gradient(height.astype(np.float32))
    nx = -gx * (n * 0.48)
    ny = -gy * (n * 0.48)
    nz = np.ones_like(nx)
    norm = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack((nx / norm * 0.5 + 0.5, -(ny / norm) * 0.5 + 0.5, nz / norm * 0.5 + 0.5), axis=2)
    ao = np.clip(0.88 - np.maximum(-noise, 0) * 0.13 - np.maximum(-height, 0) * 0.20, 0.56, 1.0)
    smoothness = np.clip(1.0 - roughness, 0.03, 0.68)
    metallic_rgba = np.stack((metallic, metallic, metallic, smoothness), axis=2)
    return color, normal, metallic_rgba, ao, roughness


def save_rgb(path: Path, values: np.ndarray, upscale: int = 1, grain_seed: int | None = None):
    rgb = np.clip(values * 255.0, 0, 255).astype(np.uint8)
    image = Image.fromarray(rgb, mode="RGB")
    if upscale != 1:
        image = image.resize((image.width * upscale, image.height * upscale), Image.Resampling.LANCZOS)
        if grain_seed is not None:
            rng = np.random.default_rng(grain_seed)
            noise = rng.normal(0.0, 1.15, (image.height, image.width, 1))
            pixels = np.asarray(image, dtype=np.float32) + noise
            image = Image.fromarray(np.clip(pixels, 0, 255).astype(np.uint8), mode="RGB")
    image.save(path, optimize=True)


def save_rgba(path: Path, values: np.ndarray):
    rgba = np.clip(values * 255.0, 0, 255).astype(np.uint8)
    Image.fromarray(rgba, mode="RGBA").save(path, optimize=True)


def save_gray(path: Path, values: np.ndarray):
    gray = np.clip(values * 255.0, 0, 255).astype(np.uint8)
    Image.fromarray(gray, mode="L").save(path, optimize=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--only", default=None)
    args = parser.parse_args()
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=True)
    surfaces = [
        ("WoodGreen", "wood_green", 1101, 1),
        ("WoodBlue", "wood_blue", 1102, 1),
        ("WoodBrown", "wood_brown", 1103, 1),
        ("WoodRaw", "wood_raw", 2202, 1),
        ("SteelPaint", "steel_painted", 3303, 1),
        ("SteelRust", "rusted_steel", 4404, 1),
        ("CastIron", "cast_iron", 5505, 1),
        ("Concrete", "concrete", 6606, 1),
        ("SteelBare", "bare_steel", 7707, 1),
    ]
    for name, kind, seed, upscale in surfaces:
        if args.only and name not in args.only.split(","): continue
        color, normal, metal, ao, rough = build_surface(kind, seed)
        save_rgb(out / f"Tex_{name}_BaseColor.png", color, upscale, seed)
        save_rgb(out / f"Tex_{name}_Normal.png", normal)
        save_rgba(out / f"Tex_{name}_MetallicSmoothness.png", metal)
        save_gray(out / f"Tex_{name}_AO.png", ao)
        save_gray(out / f"Tex_{name}_Roughness.png", rough)
        del color, normal, metal, ao, rough
        print(f"TEXTURE_SET_OK {name} base={SIZE * upscale} detail={SIZE}", flush=True)


if __name__ == "__main__":
    main()
