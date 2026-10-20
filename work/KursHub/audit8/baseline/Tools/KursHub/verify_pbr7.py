"""Read back Audit7 bitmap pixels; no Blender/Unity import or source mutation.

This validates representation, channel packing, physical height/normal agreement,
and the declared nominal UV period. It is not an artistic approval or a GPU test.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

from audit7_surfaces import FAMILIES, normal_map


def pixels(folder, name, suffix):
    with Image.open(folder / f'{name}_{suffix}.png') as image:
        array = np.array(image)
        mode = image.mode
    return array, mode


def run(root, output):
    folder = root / 'Assets/Environment/KursHub/Textures'
    manifest = json.loads((folder/'Audit7PBRManifest.json').read_text(encoding='utf8'))
    records = {record['file']: record for record in manifest['maps']}
    specs = {spec['name']: spec for spec in manifest['families']}
    expected_families = dict(FAMILIES, LeafGreen=(1024, .13))
    assert set(expected_families) <= set(specs), 'Missing established texture family'
    assert set(specs)-set(expected_families) <= {'FirNeedles'}, 'Unknown unaudited texture family'
    if 'FirNeedles' in specs:
        fir = specs['FirNeedles']
        assert fir['resolution'] == 1024 and fir['periodMetres'] > 0 and fir['periodYMetres'] > 0
        assert fir['alpha'], 'Frond atlas must declare its alpha coverage'
        expected_families['FirNeedles'] = (1024, fir['periodMetres'])
    suffixes = ('BaseColor', 'Normal', 'Roughness', 'Metallic', 'Occlusion', 'Height', 'MetallicSmoothness')
    expected = {f'{name}_{suffix}.png' for name in expected_families for suffix in suffixes}
    assert set(records) == expected, 'Missing map record'
    total_bytes = 0
    for filename, record in records.items():
        path = folder / filename
        assert path.is_file(), str(path)
        assert hashlib.sha256(path.read_bytes()).hexdigest() == record['sha256'], filename+' changed after generation'
        total_bytes += path.stat().st_size
    ramp = np.repeat(np.arange(64, dtype=np.float32)[:, None], 64, axis=1) * .0001
    assert normal_map(ramp, 1, 1)[1:-1, :, 1].mean() > .5
    assert normal_map(ramp.T, 1, 1)[:, 1:-1, 0].mean() < .5
    results = []
    for name, (n, period) in expected_families.items():
        spec = specs[name]
        assert spec['resolution'] == n and spec['periodMetres'] == period
        expected_tiling = 1 if name.startswith('Rug') or name in ('LeafGreen', 'FirNeedles') else 2 / period
        assert abs(spec['tiling']-expected_tiling) < 1e-9
        row = {'name': name, 'resolution': n, 'uvRepeatMetres': [period, spec['periodYMetres']],
               'tiling': spec['tiling'], 'texelMillimetres': [period*1000/n, spec['periodYMetres']*1000/n], 'channels': {}}
        for suffix in suffixes:
            array, mode = pixels(folder, name, suffix)
            assert array.shape[:2] == (n, n), name+' '+suffix
            channels = 1 if array.ndim == 2 else array.shape[2]
            expected_channels = 3 if suffix == 'Normal' else (4 if suffix == 'MetallicSmoothness' or (suffix == 'BaseColor' and spec.get('alpha', False)) else 3 if suffix == 'BaseColor' else 1)
            assert channels == expected_channels, (name, suffix, array.shape)
            if suffix == 'Height':
                assert array.dtype == np.uint16, (name, mode, array.dtype)
            else:
                assert array.dtype == np.uint8
            row['channels'][suffix] = {'mode': mode, 'minimum': int(array.min()), 'maximum': int(array.max()), 'mean': float(array.mean())}
            if suffix == 'BaseColor':
                if channels == 4:
                    row['alphaCoverage'] = float((array[..., 3] >= 128).mean())
                    if name == 'FirNeedles':
                        assert array[..., 3].min() == 0 and array[..., 3].max() == 255
                        assert .01 < row['alphaCoverage'] < .95, 'Frond alpha does not describe a useful cutout'
                color = array[..., :3].astype(np.float32)/255
                seam_x = float(np.abs(color[:, 0]-color[:, -1]).mean())
                seam_y = float(np.abs(color[0]-color[-1]).mean())
                step_x = float(np.abs(np.diff(color, axis=1)).mean())
                step_y = float(np.abs(np.diff(color, axis=0)).mean())
                row['edgeDifference'] = {'wrapX': seam_x, 'wrapY': seam_y, 'interiorX': step_x, 'interiorY': step_y}
                if name == 'CeramicTile':
                    centres = color[128::256, 128::256]
                    row['tilePattern'] = {'tileSizeMetres': .5, 'tilesPerRepeat': [16, 16], 'redTileCount': int((centres[..., 0] > centres[..., 1]*1.5).sum()), 'totalTiles': int(centres.shape[0]*centres.shape[1])}
                del color
            if suffix == 'Occlusion' and name == 'LeafGreen':
                assert array.min() == array.max() == 255, 'Raised leaf vein crests must not get fake dark cavity AO'
        rough, _ = pixels(folder, name, 'Roughness')
        metallic, _ = pixels(folder, name, 'Metallic')
        packed, _ = pixels(folder, name, 'MetallicSmoothness')
        assert np.array_equal(packed[..., 0], metallic), name+' metallic packing'
        assert int(np.abs(packed[..., 3].astype(np.int16) - (255 - rough.astype(np.int16))).max()) <= 1, name+' smoothness packing'
        assert not np.any(packed[..., 1:3]), name+' unexpected packed channels'
        if name not in ('RustedSteel', 'Copper'):
            assert not np.any(metallic), name+' nonmetal marked metallic'
        row['packedChannelsVerified'] = True
        del rough, metallic, packed
        height_pixels, _ = pixels(folder, name, 'Height')
        hmin, hmax = spec['heightMinMetres'], spec['heightMaxMetres']
        height = height_pixels.astype(np.float32)/65535*(hmax-hmin)+hmin
        del height_pixels
        normal_pixels, _ = pixels(folder, name, 'Normal')
        normal = normal_pixels.astype(np.float32)/255
        predicted = normal_map(height, spec['periodYMetres'], period)
        max_error = float(np.abs(normal-predicted).max())
        assert max_error < .0021, (name, 'height-normal disagreement', max_error)
        length_error = float(np.abs(np.linalg.norm(normal*2-1, axis=-1)-1).max())
        assert length_error < .008, (name, 'invalid normal length', length_error)
        row['heightMetres'] = [hmin, hmax]
        row['heightQuantizationMetres'] = (hmax-hmin)/65535
        row['normalMaximumPixelError'] = max_error
        row['normalMaximumUnitLengthError'] = length_error
        results.append(row)
        del height, normal, normal_pixels, predicted
        print('PBR_READBACK_OK', name, flush=True)
    report = {'status': 'PASS', 'families': len(results), 'maps': len(records), 'pngBytes': total_bytes,
              'analyticRampGreenPositive': True, 'analyticRampRedNegative': True,
              'source': str(folder.resolve()), 'results': results,
              'limits': ['No Unity material/import/GPU rendering claim.',
                         'Nominal metres-based UV period verified from generator contract; per-instance scale can change density.',
                         'Cavity AO is procedural micro-relief, not a geometry bake.',
                         'Height is a real 16-bit exchange field, not active runtime displacement.',
                         'Contact-sheet inspection does not establish photographic reference equivalence.']}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf8')
    print('PBR_READBACK_PASS', len(results), len(records), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    arguments = parser.parse_args()
    run(arguments.root.resolve(), arguments.output.resolve())
