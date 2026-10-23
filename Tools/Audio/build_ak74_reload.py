"""Собрать перезарядку АК-74 из существующего Free Weapon Sound Effects.

Без синтеза: исходные записи остаются неизменными. Время события перезарядки
принадлежит PlayerWeapons, этот файл отвечает только за звуковое представление.
"""

from array import array
from pathlib import Path
import sys
import wave


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Props/FreeWeaponSFX_v1.1/AssaultRifle/Reload"
OUTPUT = ROOT / "Assets/Audio/Generated/AK74/AK74_Reload.wav"
RATE = 44100
CHANNELS = 2
DURATION = 2.2
PHASES = (
    (0.0, "sfx_wpn_ar_reload_01_magOut.wav"),
    (0.6, "sfx_wpn_ar_reload_02_magDraw.wav"),
    (1.2, "sfx_wpn_ar_reload_03_magIn.wav"),
    # У charge есть второй сильный щелчок на 0,35 с: начало 1,73 сохраняет
    # оба щелчка до окончания backend-таймера 2,2 с, без изменения высоты тона.
    (1.73, "sfx_wpn_ar_reload_04_charge.wav"),
)


def build():
    frames = round(RATE * DURATION)
    mix = [0.0] * (frames * CHANNELS)
    for start_seconds, name in PHASES:
        with wave.open(str(SOURCE / name), "rb") as source:
            assert source.getparams()[:3] == (CHANNELS, 2, RATE), name
            samples = array("h", source.readframes(source.getnframes()))
        if sys.byteorder != "little":
            samples.byteswap()
        start = round(RATE * start_seconds)
        count = min(len(samples) // CHANNELS, frames - start)
        for frame in range(count):
            # Короткие линейные края не создают щелчок при обрезке хвоста.
            gain = min(1.0, frame / (RATE * 0.002), (count - 1 - frame) / (RATE * 0.020))
            for channel in range(CHANNELS):
                mix[(start + frame) * CHANNELS + channel] += samples[frame * CHANNELS + channel] / 32768 * gain
    peak = max(abs(value) for value in mix)
    gain = 0.65 / peak
    output = array("h", (round(max(-1, min(1, value * gain)) * 32767) for value in mix))
    if sys.byteorder != "little":
        output.byteswap()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUTPUT), "wb") as target:
        target.setparams((CHANNELS, 2, RATE, frames, "NONE", "not compressed"))
        target.writeframes(output.tobytes())
    print(f"{OUTPUT.relative_to(ROOT).as_posix()}: {DURATION:.2f}s stereo {RATE}Hz PCM16; peak0.65")


if __name__ == "__main__":
    build()
