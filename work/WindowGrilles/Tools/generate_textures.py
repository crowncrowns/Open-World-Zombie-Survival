from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter


SIZE = 4096
OUT = Path(__file__).resolve().parents[1] / "UnityProject" / "Assets" / "Environment" / "Windows" / "MetalGrilles" / "Textures"

PAINTS = {
    "01_Classic": (73, 91, 48),
    "02_Forged": (38, 39, 36),
    "03_Reinforced": (68, 70, 66),
    "04_Survivor": (90, 74, 56),
    "05_Hinged": (57, 64, 52),
}


def noise_field(seed: int, res: int, size: int = SIZE) -> np.ndarray:
    rng = np.random.default_rng(seed)
    src = rng.integers(0, 256, (res, res), dtype=np.uint8)
    im = Image.fromarray(src, "L").resize((size, size), Image.Resampling.BICUBIC)
    return np.asarray(im, dtype=np.float32) / 255.0


def save_rgb(path: Path, arr: np.ndarray) -> None:
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").save(path, optimize=True, compress_level=6)


def save_l(path: Path, arr: np.ndarray) -> None:
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "L").save(path, optimize=True, compress_level=6)


def scratches(seed: int, pale: tuple[int, int, int], dark: tuple[int, int, int], count: int) -> Image.Image:
    im = Image.new("RGB", (SIZE, SIZE), (0, 0, 0))
    draw = ImageDraw.Draw(im)
    rng = np.random.default_rng(seed)
    for _ in range(count):
        x = int(rng.integers(0, SIZE))
        y = int(rng.integers(0, SIZE))
        length = int(rng.integers(8, 240))
        dx = int(rng.integers(-5, 6))
        dy = length if rng.random() < 0.58 else int(rng.integers(-length, length))
        col = pale if rng.random() < 0.52 else dark
        w = int(rng.choice([1, 1, 1, 2, 3]))
        draw.line((x, y, x + dx, y + dy), fill=col, width=w)
    return im


def make_set(name: str, color: tuple[int, int, int], seed: int, family: str) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    n0 = noise_field(seed, 72)
    n1 = noise_field(seed + 11, 320)
    n2 = noise_field(seed + 23, 1024)
    rng = np.random.default_rng(seed + 39)
    cloud = (n0 - 0.5) * 0.34 + (n1 - 0.5) * 0.18 + (n2 - 0.5) * 0.06
    fine = rng.normal(0.0, 1.35, (SIZE, SIZE)).astype(np.float32)
    if family == "rust":
        palette = np.array([126.0, 67.0, 35.0], dtype=np.float32)
        variation = np.stack((cloud * 85, cloud * 58, cloud * 35), axis=-1)
    elif family == "bare":
        palette = np.array([89.0, 92.0, 88.0], dtype=np.float32)
        variation = np.stack((cloud * 48, cloud * 48, cloud * 48), axis=-1)
    else:
        palette = np.array(color, dtype=np.float32)
        variation = np.stack((cloud * 62, cloud * 67, cloud * 54), axis=-1)
    rgb = palette[None, None, :] + variation + fine[..., None]

    # Streaks and tiny paint loss break up the broad color without turning the
    # whole surface into a uniform rust filter.
    if family == "paint":
        streak = scratches(seed + 71, tuple(max(0, c - 26) for c in color), tuple(min(255, c + 23) for c in color), 6200)
        s = np.asarray(streak, dtype=np.float32)
        s = np.where((s.sum(axis=2, keepdims=True) > 0), s, rgb)
        rgb = np.where(s.sum(axis=2, keepdims=True) > 0, s, rgb)
    elif family == "rust":
        streak = scratches(seed + 71, (171, 101, 56), (73, 43, 27), 4200)
        s = np.asarray(streak, dtype=np.float32)
        rgb = np.where(s.sum(axis=2, keepdims=True) > 0, s, rgb)

    base_path = OUT / f"T_{name}_BaseColor.png"
    save_rgb(base_path, rgb)

    # URP Lit reads metallic in R and smoothness in A from its packed mask map.
    # The remaining channels deliberately mirror metallic for easier inspection.
    if family == "rust":
        metal = np.clip(58 + (n1 - 0.5) * 28, 25, 100)
        smooth = np.clip(35 + (n2 - 0.5) * 16, 18, 58)
        ao = np.clip(222 - np.maximum(0, n0 - 0.45) * 47 - np.maximum(0, n1 - 0.55) * 22, 145, 250)
    elif family == "bare":
        metal = np.clip(205 + (n1 - 0.5) * 42, 166, 248)
        smooth = np.clip(112 + (n2 - 0.5) * 44, 78, 158)
        ao = np.clip(235 - np.maximum(0, n0 - 0.48) * 36 - np.maximum(0, n1 - 0.55) * 15, 170, 252)
    else:
        metal = np.clip(12 + (n1 - 0.5) * 8, 3, 26)
        smooth = np.clip(88 + (n2 - 0.5) * 18, 64, 116)
        ao = np.clip(238 - np.maximum(0, n0 - 0.48) * 28 - np.maximum(0, n1 - 0.55) * 14, 185, 253)
    packed = np.stack((metal, metal, metal, smooth), axis=-1).astype(np.uint8)
    Image.fromarray(packed, "RGBA").save(OUT / f"T_{name}_MetallicSmoothness.png", optimize=True, compress_level=6)
    save_l(OUT / f"T_{name}_AO.png", ao)

    # Fine, restrained tangent-space relief: hammered paint and oxide grain.
    height = (n1 * 0.7 + n2 * 0.3).astype(np.float32)
    gy, gx = np.gradient(height)
    nx = np.clip(128.0 - gx * 340.0, 0, 255)
    ny = np.clip(128.0 + gy * 340.0, 0, 255)
    nz = np.sqrt(np.maximum(0, 255.0 * 255.0 - (nx - 128.0) ** 2 - (ny - 128.0) ** 2))
    normal = np.stack((nx, ny, nz), axis=-1).astype(np.uint8)
    save_rgb(OUT / f"T_{name}_Normal.png", normal)


def main() -> None:
    for i, (name, color) in enumerate(PAINTS.items(), 1):
        make_set(name, color, 2400 + i * 113, "paint")
    make_set("Rust_Common", (126, 67, 35), 3511, "rust")
    make_set("BareSteel_Common", (89, 92, 88), 3661, "bare")
    files = sorted(OUT.glob("*.png"))
    print(f"TEXTURES_OK count={len(files)} resolution={SIZE}x{SIZE} bytes={sum(f.stat().st_size for f in files)}")


if __name__ == "__main__":
    main()
