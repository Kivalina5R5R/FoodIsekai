"""Original intro and Ready-phase cues for FoodIsekaiZ; no external samples.

Run from the project root. Only the Ready*, GuideBubble and Curtain* wav files are written; existing GUIDs are preserved.
These cues use the same tavern instruments as GenerateTavernSounds.py (wooden taps, metallic bells, plucked
lute strings, cloth rustle and the water bubble) and the same light two-tap room reflection, so they sit with
the original gameplay set instead of the anime perk palette.
Everything here plays over IntroMusic, which is in B minor, so pitched notes come only from B minor / D major.
GuideBubble also plays over the game BGM (E minor), so it stays an unpitched bubble.
Levels are matched by the short-term loudness used for the perk cues.
"""
import json
import math
from pathlib import Path
import random
import re
import struct
import sys
import uuid
import wave

sys.path.insert(0, str(Path(__file__).parent))
from GeneratePerkSounds import RATE, FOLDER, loudness, soft_limit  # noqa: E402

RNG = random.Random(61027)

# B minor / D major, the key of IntroMusic.
B2, D3, FS3, B3 = 123.47, 146.83, 185.0, 246.94
D4, E4, FS4, A4, B4 = 293.66, 329.63, 369.99, 440.0, 493.88
D5, FS5, A5, B5 = 587.33, 739.99, 880.0, 987.77
D6, FS6, A6, B6 = 1174.66, 1479.98, 1760.0, 1975.53


# The three instruments below follow GenerateTavernSounds.py so the timbres match the original cues.
def tone(out, start, freq, length, gain=.25, kind='bell'):
    first = int(start * RATE)
    n = min(int(length * RATE), len(out) - first)
    if kind == 'pluck':
        delay = max(3, int(RATE / freq))
        ring = [RNG.uniform(-1, 1) for _ in range(delay)]
        for i in range(n):
            pos = i % delay
            v = ring[pos]
            ring[pos] = (v + ring[(pos + 1) % delay]) * .497
            attack = min(1, i / (RATE * .005))
            tail = min(1, (n - i) / (RATE * .035))
            out[first + i] += v * gain * attack * tail
        return
    partials = [(1, 1, 1), (2.756, .32, .55), (5.404, .1, .25)] if kind == 'bell' else [(1, 1, 1), (2, .22, .55), (3, .09, .3)]
    for i in range(n):
        t = i / RATE
        attack = min(1, t / .004)
        tail = min(1, (n - i) / (RATE * .045))
        v = sum(a * math.sin(2 * math.pi * freq * ratio * t) * math.exp(-t / (length * .27 * decay)) for ratio, a, decay in partials)
        out[first + i] += v * gain * attack * tail


def rustle(out, start, length, gain, swell=None):
    # Low-passed noise; the original paper flick, here also the curtain cloth when it follows a swell curve.
    low = 0
    first = int(start * RATE)
    for i in range(min(int(length * RATE), len(out) - first)):
        t = i / (length * RATE)
        low = .85 * low + .15 * RNG.uniform(-1, 1)
        shape = swell(t) if swell else math.sin(math.pi * t) ** 2
        out[first + i] += low * gain * shape


def puff(out, start, gain):
    # Two heavy cloth panels meeting: a short, muffled breath of air with no pitch.
    first = int(start * RATE)
    low = lower = 0.
    for i in range(min(int(.18 * RATE), len(out) - first)):
        t = i / RATE
        low += .06 * (RNG.uniform(-1, 1) - low)
        lower += .06 * (low - lower)
        envelope = min(1, t / .006) * math.exp(-t / .045)
        out[first + i] += gain * envelope * lower * 6


def melody(out, notes, step=.095, gain=.23, kind='bell', length=.5, start=0):
    for i, f in enumerate(notes):
        tone(out, start + i * step, f, length, gain, kind)


def bubble(out, start, frequency, sweep, length, gain):
    first = int(start * RATE)
    n = min(int(length * RATE), len(out) - first)
    tau = .022
    for i in range(n):
        sec = i / RATE
        phase = 2 * math.pi * (frequency * sec + sweep * (sec - tau * (1 - math.exp(-sec / tau))))
        envelope = (1 - math.exp(-sec / .002)) * math.exp(-sec / (length * .19)) * min(1, (n - i) / (RATE * .025))
        out[first + i] += gain * (math.sin(phase) + .13 * math.sin(phase * 2.07)) * envelope


def build(name, a):
    if name == 'ReadyCardAppear':
        # A card laid on the table: paper flick and a light wooden tick. Pitched D-E-F#-G by PlayerReadySelection.
        rustle(a, 0, .07, .3)
        tone(a, 0, D5, .12, .3, 'wood')
    elif name == 'ReadyStepOn':
        # Two rising bells, in the style of the coin and service cues.
        melody(a, [D6, A6], .06, .2, 'bell', .32)
        tone(a, 0, D4, .1, .1, 'wood')
    elif name == 'ReadyStepOff':
        melody(a, [A5, FS5], .07, .14, 'wood', .18)
    elif name == 'ReadyHoldTick':
        # A small wooden tick; PlayerReadySelection steps it F#, A, B toward the confirmation.
        tone(a, 0, FS5, .12, .3, 'wood')
        tone(a, 0, FS5 * 2, .08, .06, 'bell')
    elif name == 'ReadyContested':
        # The same low wooden double tap as WrongFood, moved into B minor.
        melody(a, [D4, B3], .1, .32, 'wood', .25)
    elif name == 'ReadyConfirm':
        # Built like FoodServed, the game's own success sound: a wooden tap as the card squeezes, a bright
        # rising bell chime, then a small high sparkle as the Ready badge pops in at about 0.42 seconds.
        tone(a, 0, D4, .14, .16, 'wood')
        melody(a, [D5, FS5, A5], .07, .24, 'bell', .55, start=.02)
        melody(a, [D6, A6], .05, .15, 'bell', .45, start=.42)
    elif name == 'GuideBubble':
        # The speech bubble pops like the emoji bubbles, a touch higher so it reads as Lunar's.
        bubble(a, 0, 330, 220, .11, .34)
    elif name == 'CurtainClose':
        # Cloth rustle that swells with the closing panels, then a soft muffled puff and a short, quiet wooden
        # tap as they meet. Nothing pitched rings on, so the meeting reads as fabric, not a chord.
        rustle(a, 0, .68, .55, lambda t: (.25 + .75 * t) * math.sin(math.pi * min(1, t * 1.05)) ** .5)
        puff(a, .63, .45)
        tone(a, .64, D3, .07, .12, 'wood')
    elif name == 'CurtainOpen':
        # Cloth rustle that eases away as the panels open, over a rising lute and a small bell at the reveal.
        rustle(a, 0, .95, .45, lambda t: math.sin(math.pi * t) ** 1.5)
        melody(a, [D4, FS4, A4, D5], .08, .22, 'pluck', .7, start=.15)
        tone(a, .5, A6, .45, .1, 'bell')


# (seconds, short-term loudness target in dB). Player feedback sits 1-2 dB above the Small Perk ticks so it reads
# over the intro music; the four card ticks, step off and Lunar's bubble stay softer because they land with other sounds.
CUES = dict(ReadyCardAppear=(.3, -22), ReadyStepOn=(.45, -18), ReadyStepOff=(.35, -22), ReadyHoldTick=(.25, -20),
            ReadyContested=(.45, -18), ReadyConfirm=(1.0, -16), GuideBubble=(.24, -23),
            CurtainClose=(1.0, -20), CurtainOpen=(1.25, -21))


def create(name):
    duration, level = CUES[name]
    a = [0.0] * int(duration * RATE)
    build(name, a)
    # The same subtle room reflection as the original tavern cues.
    dry = a[:]
    for delay, amount in ((.037, .12), (.071, .07)):
        offset = int(delay * RATE)
        for i in range(offset, len(a)):
            a[i] += dry[i - offset] * amount
    scale = 10 ** ((level - loudness(a)) / 20)
    a = [value * scale * min(1, i / 128, (len(a) - 1 - i) / 256) for i, value in enumerate(a)]
    return [soft_limit(value) for value in a]


def main():
    template = (FOLDER / 'SuccessPop.wav.meta').read_text()
    report = {}
    for name in CUES:
        data = create(name)
        output = FOLDER / (name + '.wav')
        with wave.open(str(output), 'wb') as stream:
            stream.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
            stream.writeframes(b''.join(struct.pack('<h', round(value * 32767)) for value in data))
        meta = Path(str(output) + '.meta')
        guid = re.search(r'guid: (\w+)', meta.read_text())[1] if meta.exists() else uuid.uuid4().hex
        meta.write_text(re.sub(r'guid: \w+', 'guid: ' + guid, template))
        peak = max(abs(value) for value in data)
        assert 0 < peak <= .89 + 1e-6 and data[0] == 0 and data[-1] == 0
        report[name] = dict(seconds=round(len(data) / RATE, 3), peak=round(peak, 4),
                            loudness=round(loudness(data), 1), guid=guid)
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
