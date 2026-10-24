"""Generate tileable 2K PBR surface maps for the balcony kit.

The maps are procedural authoring textures, not photographs or high-to-low bakes.
Packed mask layout: R metallic, G ambient occlusion, B height, A smoothness.
"""
from pathlib import Path
import sys
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'work/PostApocalypseBalconies/UnityProject/Assets/Environment/PostApocalypse/Balconies/Textures'
SIZE = 2048
SEED = 2401024


def periodic_noise(size, cells, rng):
    small = rng.random((cells, cells), dtype=np.float32)
    image = Image.fromarray(np.uint8(np.clip(small * 255, 0, 255)), 'L')
    large = image.resize((size, size), Image.Resampling.BICUBIC)
    return np.asarray(large, dtype=np.float32) / 255.0


def save_rgb(path, array):
    Image.fromarray(np.uint8(np.clip(array * 255.0 + 0.5, 0, 255)), 'RGB').save(path, optimize=True)


def save_rgba(path, array):
    Image.fromarray(np.uint8(np.clip(array * 255.0 + 0.5, 0, 255)), 'RGBA').save(path, optimize=True)


def make_surface(name, color, metalness, smoothness, detail='stone'):
    rng = np.random.default_rng(SEED + sum(ord(c) for c in name))
    n1 = periodic_noise(SIZE, 32, rng)
    n2 = periodic_noise(SIZE, 128, rng)
    n3 = periodic_noise(SIZE, 512, rng)
    n4 = rng.random((SIZE, SIZE), dtype=np.float32)
    y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)

    if detail == 'stone':
        # Fine concrete grain with larger mineral discoloration and pitting.
        grain = (n1 - .5) * .19 + (n2 - .5) * .12 + (n3 - .5) * .06 + (n4 - .5) * .035
        streak = (periodic_noise(SIZE, 16, rng) - .5) * (.025 + .035 * (y / SIZE))
        stain = np.clip((n2 * .75 + n3 * .25 - .62) * 2.6, 0, 1)
        rgb = np.stack([color[0] + grain - stain*.035,
                        color[1] + grain - stain*.032,
                        color[2] + grain - stain*.027], axis=-1)
        height = np.clip(.52 + (n2-.5)*.16 + (n3-.5)*.12 + (n4-.5)*.04, .05, .95)
        ao = np.clip(.91 - stain*.25 - np.maximum(0,.48-n3)*.20, .38, 1)
    elif detail == 'paint':
        # Peeling paint edge speckle and exposed rusty chips.
        cloud = n1*.65 + n2*.35
        chips = np.clip((n3 - .75) * 5.8, 0, 1) * np.clip((cloud-.48)*3.5, 0, 1)
        grain = (n2-.5)*.09 + (n3-.5)*.05 + (n4-.5)*.025
        rust = np.array([.28, .105, .047], dtype=np.float32)
        rgb = np.stack([color[0]+grain, color[1]+grain, color[2]+grain], axis=-1)*(1-chips[...,None]) + rust*chips[...,None]
        height = np.clip(.5 + grain*1.8 - chips*.12, .05, .95)
        ao = np.clip(.94 - chips*.28 - np.maximum(0,.5-n3)*.12, .35, 1)
    elif detail == 'metal':
        # Layered corrosion spots with dark pits; no repeated large decal stamps.
        cloud = n1*.55 + n2*.45
        corrosion = np.clip((cloud-.50)*2.9, 0, 1)
        pits = np.clip((n3-.79)*4.8, 0, 1)
        rust = np.stack([.32 + n2*.12, .12 + n3*.045, .045 + n4*.025], axis=-1)
        steel = np.stack([color[0]+(n3-.5)*.09, color[1]+(n3-.5)*.085, color[2]+(n3-.5)*.08], axis=-1)
        rgb = steel*(1-corrosion[...,None]) + rust*corrosion[...,None]
        rgb *= 1-pits[...,None]*.42
        height = np.clip(.52 + (n2-.5)*.1 - corrosion*.10 + pits*.07, .02, .98)
        ao = np.clip(.9 - pits*.42 - corrosion*.18, .25, 1)
    elif detail == 'wood':
        # Directional growth grain and dark moisture in checks.
        grain_axis = periodic_noise(SIZE, 96, rng)
        line = np.sin((x / SIZE * 2*np.pi*27) + (grain_axis-.5)*3.1).astype(np.float32)
        moisture = np.clip((periodic_noise(SIZE, 24, rng)-.65)*2.8, 0, 1)
        grain = (n2-.5)*.075 + (n3-.5)*.035 + line*.035 + (n4-.5)*.02
        rgb = np.stack([color[0]+grain-moisture*.055,
                        color[1]+grain-moisture*.043,
                        color[2]+grain-moisture*.032], axis=-1)
        height = np.clip(.52 + line*.055 + (n3-.5)*.07 - moisture*.09, .03, .97)
        ao = np.clip(.92 - moisture*.32 - np.maximum(0,.45-n3)*.12, .28, 1)
    elif detail == 'glass':
        smudges = np.clip((n1*.45+n2*.55-.65)*2.4, 0, 1)
        streak = np.clip(np.sin(x/SIZE*2*np.pi*53 + (n2-.5)*2)*.5+.5, 0, 1)*.035
        grain = (n3-.5)*.035 + streak
        rgb = np.stack([color[0]+grain-smudges*.018,
                        color[1]+grain-smudges*.016,
                        color[2]+grain-smudges*.012], axis=-1)
        height = np.clip(.52 + (n2-.5)*.035, .25, .75)
        ao = np.clip(.98-smudges*.12, .7, 1)
    elif detail == 'organic':
        blotch = n1*.6+n2*.4
        mold = np.clip((blotch-.55)*2.2,0,1)
        grain = (n2-.5)*.10 + (n3-.5)*.06 + (n4-.5)*.03
        rgb = np.stack([color[0]+grain-mold*.07,
                        color[1]+grain-mold*.045,
                        color[2]+grain-mold*.025], axis=-1)
        height = np.clip(.5 + (n2-.5)*.11 + (n3-.5)*.1, .04, .96)
        ao = np.clip(.9 - mold*.26 - np.maximum(0,.48-n3)*.1, .35, 1)
    else:
        raise ValueError(detail)

    rgb = np.clip(rgb, 0, 1)
    # Tangent-space normal derived from the high-frequency height field.
    gy, gx = np.gradient(height.astype(np.float32))
    nx, ny, nz = -gx*18.0, -gy*18.0, np.ones_like(height)
    norm = np.sqrt(nx*nx + ny*ny + nz*nz)
    normal = np.stack([nx/norm*.5+.5, ny/norm*.5+.5, nz/norm*.5+.5], axis=-1)
    metallic = np.full_like(height, metalness, dtype=np.float32)
    smooth = np.full_like(height, smoothness, dtype=np.float32)
    mask = np.stack([metallic, ao, height, smooth], axis=-1)

    OUT.mkdir(parents=True, exist_ok=True)
    save_rgb(OUT / f'{name}_BaseColor.png', rgb)
    save_rgb(OUT / f'{name}_Normal.png', normal)
    save_rgba(OUT / f'{name}_Mask.png', mask)
    print('TEXTURE_SET', name, SIZE, flush=True)


def main():
    surfaces = [
        ('Concrete', (.43,.42,.39), 0.0, .16, 'stone'),
        ('PaintedMetal', (.36,.33,.28), .42, .24, 'paint'),
        ('CorrodedMetal', (.34,.25,.18), .88, .18, 'metal'),
        ('Timber', (.37,.26,.15), 0.0, .18, 'wood'),
        ('Glazing', (.31,.42,.43), 0.0, .62, 'glass'),
        ('Organic', (.31,.30,.22), 0.0, .12, 'organic'),
    ]
    for args in surfaces:
        make_surface(*args)


if __name__ == '__main__':
    main()
