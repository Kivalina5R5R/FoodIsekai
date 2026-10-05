"""Original footstep cues for Lunar's wall walk in FoodIsekaiZ; no external samples.

Run from the project root. Only LunarFootstep*.wav assets are written; existing GUIDs are preserved.
Each step is a small, light boot on old tavern floorboards. A short noise burst excites a bank of damped
board resonances (150-1700 Hz, decays of 6-45 ms), so the step reads as hollow wood instead of a tuned tone;
a faint low boom comes from the floor beneath, a tiny heel click adds the contact, and a quieter toe tap
follows. Four variants with different board tunings alternate so consecutive steps never repeat exactly.
"""
import math
from pathlib import Path
import random
import struct
import uuid
import wave

RATE = 48000
TAU = 2 * math.pi
FOLDER = Path('Assets/Audio/SFX')
DURATION = .2
PEAK = .5

META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 48000
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  ambisonic: 0
  3D: 0
  loadInBackground: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def excitation(count, rng, attack_ms, decay_ms, gain):
    # A soft-edged noise burst: the boot sole striking the board.
    burst = []
    for i in range(count):
        t = i / RATE * 1000
        envelope = min(1, t / attack_ms) * math.exp(-t / decay_ms)
        burst.append(gain * envelope * rng.uniform(-1, 1))
    return burst


def resonate(source, frequency, decay):
    # Two-pole resonator: rings at the board mode and dies away with the given time constant.
    radius = math.exp(-1 / (decay * RATE))
    a1 = 2 * radius * math.cos(TAU * frequency / RATE)
    a2 = -radius * radius
    gain = (1 - radius * radius) * .5
    y1 = y2 = 0.
    out = []
    for x in source:
        y = gain * x + a1 * y1 + a2 * y2
        out.append(y)
        y2, y1 = y1, y
    return out


def impact(buffer, start, rng, tuning, strength):
    offset = int(start * RATE)
    count = len(buffer) - offset
    hit = excitation(count, rng, .4, 2.5, strength)
    # Board modes: lower modes ring longest and carry the hollow "tok" of wood.
    modes = [(150, .045, 1.0), (240, .035, .9), (370, .028, .85), (520, .022, .7),
             (740, .016, .55), (1050, .011, .4), (1450, .008, .28), (1700, .006, .2)]
    for frequency, decay, weight in modes:
        ring = resonate(hit, frequency * tuning * rng.uniform(.96, 1.04), decay * rng.uniform(.85, 1.15))
        for i, value in enumerate(ring):
            buffer[offset + i] += weight * value * 9
    # A faint boom from the joists under the boards.
    for i in range(min(int(.06 * RATE), count)):
        t = i / RATE
        buffer[offset + i] += strength * .12 * math.exp(-t / .018) * min(1, t / .002) * math.sin(TAU * 85 * tuning * t)
    # A tiny heel click: band-limited high noise for a few milliseconds.
    previous = 0.
    for i in range(min(int(.004 * RATE), count)):
        noise = rng.uniform(-1, 1)
        buffer[offset + i] += strength * .18 * (noise - previous) * (1 - i / (.004 * RATE))
        previous = noise


def footstep(seed, tuning):
    rng = random.Random(seed)
    buffer = [0.0] * int(DURATION * RATE)
    impact(buffer, 0, rng, tuning, 1.0)
    # The toe rolls down shortly after the heel, softer and a touch brighter.
    impact(buffer, rng.uniform(.038, .05), rng, tuning * 1.08, .45)
    peak = max(abs(value) for value in buffer)
    fade = int(.02 * RATE)
    for i in range(fade):
        buffer[-1 - i] *= i / fade
    return [value / peak * PEAK for value in buffer]


def write(name, samples):
    path = FOLDER / f'{name}.wav'
    with wave.open(str(path), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, value)) * 32767)) for value in samples))
    meta = path.with_suffix('.wav.meta')
    if not meta.exists():
        meta.write_text(META.format(guid=uuid.uuid4().hex), encoding='utf8', newline='\n')
    print(path)


if __name__ == '__main__':
    write('LunarFootstepLeft', footstep(4101, 1.0))
    write('LunarFootstepRight', footstep(4102, 1.06))
    write('LunarFootstepLeft2', footstep(4103, .96))
    write('LunarFootstepRight2', footstep(4104, 1.03))
