"""Deterministic, short original service cues; no vendor audio is modified."""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 44100
OUT = Path(__file__).resolve().parents[2] / "Assets/Audio/Generated/Services"


def write(name, duration, notes):
    rng = random.Random(11)
    samples = []
    for i in range(round(duration * RATE)):
        time = i / RATE
        value = 0.0
        for start, frequency, length, strength, metallic in notes:
            t = time - start
            if not 0 <= t < length:
                continue
            envelope = min(t / .004, 1) * math.exp(-t * (20 if metallic else 16))
            envelope *= min((length - t) / .018, 1)
            tone = math.sin(2 * math.pi * frequency * t)
            if metallic:
                tone = .5 * tone + .23 * math.sin(2 * math.pi * frequency * 2.73 * t)
                tone += .07 * rng.uniform(-1, 1) * math.exp(-t * 80)
            value += strength * envelope * tone
        samples.append(round(max(-.85, min(.85, value)) * 32767))
    OUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT / name), "wb") as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes(struct.pack("<" + "h" * len(samples), *samples))


write("TradeAccepted.wav", .27, [(0, 780, .12, .22, False), (.09, 1040, .16, .2, False)])
write("OperationRejected.wav", .22, [(0, 260, .12, .19, False), (.065, 210, .14, .17, False)])
write("RepairCompleted.wav", .43, [(0, 1320, .12, .27, True), (.12, 1690, .12, .23, True), (.25, 890, .17, .21, True)])
