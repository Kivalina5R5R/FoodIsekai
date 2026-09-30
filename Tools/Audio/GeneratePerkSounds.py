"""Original perk cues for FoodIsekaiZ; no external samples.

Run from the project root. Only Perk*.wav and SmallPerk*.wav assets are written.
Existing GUIDs are preserved; the scene wiring is deliberately maintained separately.

The Perk* shop cues (reveal to purchase) are the shared tavern set used by the Big
Perk shop. SmallPerk* cues and the purchase animation cues (flip to release) follow
an anime food-reaction style: harp glissandi, glockenspiel sparkles, a swelling
"aah" choir and orchestral hits that resolve V to I on the final release.
"""
import argparse
import json
import math
from pathlib import Path
import random
import re
import struct
import uuid
import wave

RATE = 48000
TAU = 2 * math.pi
FOLDER = Path('Assets/Audio/SFX')
RNG = random.Random(72819)

# C major, the key shared with the tavern cadences.
C3, D3, E3, F3, G3, A3, B3 = 130.81, 146.83, 164.81, 174.61, 196.0, 220.0, 246.94
C4, D4, E4, F4, G4, A4 = 261.63, 293.66, 329.63, 349.23, 392.0, 440.0
C5, D5, E5, G5, A5 = 523.25, 587.33, 659.25, 783.99, 880.0
C6, D6, E6, G6, A6 = 1046.5, 1174.66, 1318.51, 1567.98, 1760.0
C7, E7, G7 = 2093.0, 2637.02, 3135.96


def span(buffer, start, duration):
    offset = int(start * RATE)
    return offset, max(0, min(int(duration * RATE), len(buffer) - offset))


# Shared tavern instruments used by the Big Perk shop cues.
def tone(buffer, start, frequency, duration, gain, decay=True):
    offset = int(start * RATE)
    for i in range(min(int(duration * RATE), len(buffer) - offset)):
        t = i / RATE
        envelope = min(1, t / .012, (duration - t) / .055)
        envelope *= math.exp(-t * 5 / duration) if decay else math.sin(math.pi * t / duration) ** .7
        partials = math.sin(2 * math.pi * frequency * t)
        partials += .23 * math.sin(2 * math.pi * frequency * 2.003 * t) * math.exp(-t * 7)
        partials += .1 * math.sin(2 * math.pi * frequency * 3.98 * t) * math.exp(-t * 12)
        buffer[offset + i] += gain * envelope * partials


def wind(buffer, duration, gain, pulses=1):
    low = 0
    previous = 0
    for i in range(min(len(buffer), int(duration * RATE))):
        t = i / RATE / duration
        cutoff = .025 + .16 * math.sin(math.pi * t) ** 2
        low += cutoff * (RNG.uniform(-1, 1) - low)
        band = low - previous
        previous += .012 * (low - previous)
        envelope = math.sin(math.pi * t) ** 1.5
        envelope *= .5 + .5 * math.sin(math.pi * t * pulses) ** 2
        buffer[i] += band * gain * envelope


def chord(buffer, frequencies, start=0, duration=.5, gain=.15, step=.07, decay=True):
    for index, frequency in enumerate(frequencies):
        tone(buffer, start + index * step, frequency, duration, gain, decay)


# Anime food-reaction instruments.
def celesta(buffer, start, frequency, duration, gain):
    # A soft mallet strike that settles into an almost pure tone.
    offset, count = span(buffer, start, duration)
    for i in range(count):
        t = i / RATE
        envelope = min(1, t / .0015, (count - i) / (RATE * .02)) * math.exp(-t * 4.2 / duration)
        value = math.sin(TAU * frequency * t)
        value += .35 * math.sin(TAU * frequency * 2 * t) * math.exp(-t * 18)
        value += .12 * math.sin(TAU * frequency * 4.07 * t) * math.exp(-t * 30)
        buffer[offset + i] += gain * envelope * value


def glock(buffer, start, frequency, duration, gain):
    # Glockenspiel bar partials give the "kira" sparkle its metallic edge.
    offset, count = span(buffer, start, duration)
    for i in range(count):
        t = i / RATE
        envelope = min(1, t / .001, (count - i) / (RATE * .02)) * math.exp(-t * 5 / duration)
        value = math.sin(TAU * frequency * t)
        value += .45 * math.sin(TAU * frequency * 2.76 * t) * math.exp(-t * 14)
        value += .2 * math.sin(TAU * frequency * 5.4 * t) * math.exp(-t * 28)
        buffer[offset + i] += gain * envelope * value


def harp(buffer, start, frequency, duration, gain):
    # Karplus-Strong string with a brighter, longer ring than the tavern lute.
    offset, count = span(buffer, start, duration)
    delay = max(3, int(RATE / frequency))
    ring = [RNG.uniform(-1, 1) for _ in range(delay)]
    for i in range(count):
        position = i % delay
        value = ring[position]
        ring[position] = (value + ring[(position + 1) % delay]) * .4985
        envelope = min(1, i / (RATE * .002), (count - i) / (RATE * .04))
        buffer[offset + i] += value * gain * envelope


def gliss(buffer, start, notes, step, duration, gain):
    for index, frequency in enumerate(notes):
        harp(buffer, start + index * step, frequency, duration, gain * (.8 + .2 * index / max(1, len(notes) - 1)))


def phases(frequency, count, vibrato=0., rate=5.2, scoop=0.):
    # Running phase for a note with delayed vibrato and an optional upward scoop.
    values = []
    phase = 0.
    offset = RNG.uniform(0, TAU)
    for i in range(count):
        t = i / RATE
        depth = vibrato * min(1, t / .25)
        f = frequency * (1 + depth * math.sin(TAU * rate * t + offset)) * (1 - scoop * math.exp(-t * 40))
        phase += TAU * f / RATE
        values.append(phase)
    return values


def brass(buffer, start, frequency, duration, gain, attack=.02, release=.12):
    # Additive saw whose brightness blooms on the attack, like an orchestral stab.
    offset, count = span(buffer, start, duration)
    phase = phases(frequency, count, vibrato=.003, scoop=.025)
    envelope = []
    opening = []
    for i in range(count):
        t = i / RATE
        envelope.append(min(1, t / attack, (count - i) / (RATE * release)) * (.55 + .45 * math.exp(-t * 7)))
        opening.append(min(1, t / attack) * (.45 + .55 * math.exp(-t * 5)))
    for harmonic in range(1, max(2, min(18, int(7000 / frequency)))):
        weight = gain / harmonic
        for i in range(count):
            rolloff = math.exp(-(harmonic - 1) * (1.1 - .85 * opening[i]))
            buffer[offset + i] += weight * rolloff * envelope[i] * math.sin(harmonic * phase[i])


def voice_envelope(i, count, attack, release):
    t = i / RATE
    rise = min(1, t / attack)
    fall = min(1, (count - i) / (RATE * release))
    return rise * rise * (3 - 2 * rise) * fall


def choir(buffer, start, frequency, duration, gain, attack=.4, release=.35):
    # Three detuned "aah" voices; harmonics are weighted by the vowel's formants.
    offset, count = span(buffer, start, duration)
    formants = [(730, 1., 130), (1090, .5, 150), (2440, .22, 220)]
    for detune in (-.004, 0., .0045):
        phase = phases(frequency * (1 + detune), count, vibrato=.006)
        for harmonic in range(1, max(2, min(24, int(4200 / frequency)))):
            f = frequency * harmonic
            weight = sum(a * math.exp(-((f - center) / width) ** 2) for center, a, width in formants)
            weight = gain * (weight + .08 / harmonic) / 3
            if weight < gain * .004:
                continue
            for i in range(count):
                buffer[offset + i] += weight * voice_envelope(i, count, attack, release) * math.sin(harmonic * phase[i])


def strings(buffer, start, frequency, duration, gain, attack=.2, release=.3, tremolo=0.):
    # A detuned string section; tremolo bows the same notes in rapid pulses.
    offset, count = span(buffer, start, duration)
    for detune in (-.003, .003):
        phase = phases(frequency * (1 + detune), count, vibrato=.004, rate=5.8)
        for harmonic in range(1, max(2, min(12, int(6000 / frequency)))):
            weight = gain / harmonic * math.exp(-harmonic * .12) / 2
            for i in range(count):
                t = i / RATE
                pulse = 1. if tremolo <= 0 else .35 + .65 * abs(math.sin(math.pi * tremolo * t))
                buffer[offset + i] += weight * pulse * voice_envelope(i, count, attack, release) * math.sin(harmonic * phase[i])


def timpani(buffer, start, frequency, duration, gain):
    # A tuned drum body that settles in pitch, with a felt-mallet thump on top.
    offset, count = span(buffer, start, duration)
    phase = 0.
    low = 0.
    for i in range(count):
        t = i / RATE
        phase += TAU * frequency * (1 + .12 * math.exp(-t * 25)) / RATE
        body = math.sin(phase) + .45 * math.sin(phase * 1.504) * math.exp(-t * 6) + .25 * math.sin(phase * 1.98) * math.exp(-t * 9)
        low += .06 * (RNG.uniform(-1, 1) - low)
        thump = low * math.exp(-t * 45) * 2.5
        envelope = min(1, t / .002, (count - i) / (RATE * .05)) * math.exp(-t * 3.2 / duration)
        buffer[offset + i] += gain * (body * envelope + thump)


def cymbal(buffer, start, duration, gain, swell=0.):
    # High-passed noise with metallic partials; swell reverses it into a rise.
    offset, count = span(buffer, start, duration)
    low = 0.
    metals = [(3120, .06), (4410, .05), (5870, .04), (7330, .03)]
    for i in range(count):
        t = i / RATE
        noise = RNG.uniform(-1, 1)
        low += .35 * (noise - low)
        u = t / duration
        if swell:
            envelope = u ** 2.4 * min(1, (count - i) / (RATE * .01))
        else:
            envelope = min(1, t / .003, (count - i) / (RATE * .06)) * (.35 * math.exp(-t * 14) + .65 * math.exp(-t * 3 / duration))
        ring = sum(a * math.sin(TAU * f * t) for f, a in metals)
        buffer[offset + i] += gain * envelope * (noise - low + ring)


def whoosh(buffer, start, duration, gain, low_hz, high_hz):
    # A band-passed air sweep; the band follows the card's spin.
    offset, count = span(buffer, start, duration)
    band = 0.
    level = 0.
    for i in range(count):
        u = i / max(1, count)
        center = low_hz * (high_hz / low_hz) ** u
        f = 2 * math.sin(math.pi * min(center, RATE / 6) / RATE)
        noise = RNG.uniform(-1, 1)
        level += f * band
        high = noise - level - .55 * band
        band += f * high
        buffer[offset + i] += gain * band * math.sin(math.pi * u) ** 2


def kiin(buffer, start, frequency, duration, gain):
    # The anime "shine" ring: a pure high tone with a shimmering beat.
    offset, count = span(buffer, start, duration)
    for i in range(count):
        t = i / RATE
        u = i / max(1, count)
        envelope = u ** 1.6 * min(1, (count - i) / (RATE * .015))
        value = math.sin(TAU * frequency * t) + .5 * math.sin(TAU * frequency * 1.004 * t)
        value += .2 * math.sin(TAU * frequency * 2 * t)
        buffer[offset + i] += gain * envelope * value


def buzz(buffer, start, frequency, duration, gain):
    # A muffled nasal "bu" made from odd harmonics only.
    offset, count = span(buffer, start, duration)
    for i in range(count):
        t = i / RATE
        envelope = min(1, t / .006, (count - i) / (RATE * .03))
        value = sum(math.sin(TAU * frequency * h * t) / h * math.exp(-h * .25) for h in (1, 3, 5, 7))
        buffer[offset + i] += gain * envelope * value


def riser(buffer, start, duration, gain, from_hz, to_hz):
    # An exponential pitch climb that swells into the next hit.
    offset, count = span(buffer, start, duration)
    phase = 0.
    for i in range(count):
        u = i / max(1, count)
        phase += TAU * from_hz * (to_hz / from_hz) ** (u * u) / RATE
        envelope = u ** 2.2 * min(1, (count - i) / (RATE * .004))
        buffer[offset + i] += gain * envelope * (math.sin(phase) + .3 * math.sin(phase * 2.01))


def crack(buffer, start, gain):
    # A few milliseconds of bright noise and a click: the "pa!" that makes the blast feel sudden.
    offset, count = span(buffer, start, .06)
    low = 0.
    for i in range(count):
        t = i / RATE
        noise = RNG.uniform(-1, 1)
        low += .5 * (noise - low)
        buffer[offset + i] += gain * ((noise - low) * math.exp(-t * 90) + math.sin(TAU * 1800 * t) * math.exp(-t * 220))


def sub_drop(buffer, start, duration, gain, from_hz, to_hz):
    # A sine that falls in pitch under the hit, felt as weight on large speakers.
    offset, count = span(buffer, start, duration)
    phase = 0.
    for i in range(count):
        t = i / RATE
        u = i / max(1, count)
        phase += TAU * (to_hz + (from_hz - to_hz) * math.exp(-t * 9)) / RATE
        envelope = min(1, t / .003, (count - i) / (RATE * .08)) * math.exp(-t * 2.4) * (1 - u) ** .5
        buffer[offset + i] += gain * envelope * math.sin(phase)


def layer(buffer, build, curve):
    # Mixes an instrument group through a gain curve over the cue's length.
    part = [0.] * len(buffer)
    build(part)
    for i, value in enumerate(part):
        buffer[i] += value * curve(i / len(buffer))


def reverb(data, mix):
    # A small Schroeder hall so hits bloom instead of stopping dead.
    wet = [0.] * len(data)
    for delay_ms, feedback in [(29.7, .8), (37.1, .79), (41.1, .78), (43.7, .77)]:
        delay = int(RATE * delay_ms / 1000)
        line = [0.] * delay
        damp = 0.
        for i, value in enumerate(data):
            out = line[i % delay]
            damp += .35 * (out - damp)
            line[i % delay] = value + damp * feedback
            wet[i] += out * .25
    for delay_ms, gain in [(5.0, .7), (1.7, .7)]:
        delay = int(RATE * delay_ms / 1000)
        line = [0.] * delay
        for i, value in enumerate(wet):
            buffered = line[i % delay]
            out = -gain * value + buffered
            line[i % delay] = value + gain * out
            wet[i] = out
    return [dry + mix * w for dry, w in zip(data, wet)]


def rise(power):
    return lambda u: .12 + .88 * u ** power


def anime(name, data):
    # Each cue keeps one idea with at most a few layers so the sequence stays readable.
    if name == 'SmallPerkReveal':
        gliss(data, 0, [C5, E5, G5, A5, C6, E6, G6, C7], .045, .8, .17)
        for note in (C4, E4, G4):
            strings(data, .05, note, 1.1, .03, attack=.3, release=.45)
    elif name == 'SmallPerkFocus':
        glock(data, 0, G6, .35, .24)
        glock(data, .06, C7, .33, .2)
    elif name == 'SmallPerkHoldTick':
        celesta(data, 0, C6, .28, .26)
    elif name == 'SmallPerkCancel':
        celesta(data, 0, E5, .2, .2)
        celesta(data, .08, C5, .24, .16)
    elif name == 'SmallPerkUnavailable':
        buzz(data, 0, 155.56, .13, .3)
        buzz(data, .17, 155.56, .15, .3)
    elif name == 'SmallPerkPurchase':
        # Short on purpose: the card flip starts on the same frame and carries the rest of the story.
        for index, note in enumerate((C6, E6, G6)):
            glock(data, index * .035, note, .35, .15)
        celesta(data, .1, C7, .42, .14)
    elif name == 'PerkFlip':
        whoosh(data, .08, 1.1, .5, 350, 2600)
        for index, note in enumerate((C6, D6, E6, G6, A6, C7)):
            glock(data, .2 + index * .17, note, .32, .06)
    elif name == 'PerkImpact':
        timpani(data, 0, 87.31, .9, .6)
        for note in (F3, A3, C4, F4):
            brass(data, .004, note, .45, .07, attack=.008, release=.2)
        cymbal(data, .002, .8, .14)
    elif name == 'PerkGather':
        # The choir swells on IV, then lifts to V as the gold streams arrive.
        def subdominant(part):
            for note in (F3, A3, C4, F4):
                choir(part, 0, note, 1.55, .07, attack=.7, release=.4)
        def dominant(part):
            for note in (G3, B3, D4, G4):
                choir(part, 1.3, note, 1.3, .08, attack=.35, release=.05)
        layer(data, subdominant, rise(1.2))
        layer(data, dominant, rise(1.2))
        cymbal(data, 1., 1.6, .08, swell=1)
    elif name == 'PerkCharge':
        # Tension climbs to the last sample: the release lands right as the reversed cymbal peaks.
        for note in (G3, B3, D4, G4):
            choir(data, 0, note, .8, .07, attack=.03, release=.004)
        def roll(part):
            hit = 0.
            while hit < .78:
                timpani(part, hit, G3 / 2, .2, .22)
                hit += .05
        layer(data, roll, rise(1.8))
        riser(data, 0, .8, .06, G5, G6 * 2)
        cymbal(data, .15, .65, .16, swell=1)
    elif name == 'PerkRelease':
        crack(data, 0, .5)
        sub_drop(data, 0, 1.1, .55, 110, 38)
        timpani(data, 0, 65.41, 1.5, .55)
        timpani(data, 0, 98.0, .7, .3)
        # Sustained layers enter a few milliseconds late so the transient punches through alone.
        for note in (C3, G3, C4, E4, G4, C5, E5, G5):
            brass(data, .012, note, 1.3, .05, attack=.006, release=.5)
        for note in (C4, E4, G4, C5):
            choir(data, .02, note, 1.6, .06, attack=.04, release=.8)
        cymbal(data, .004, 1.75, .26)
        for index, note in enumerate((C7, G6, E6, C6)):
            glock(data, .18 + index * .08, note, .6, .1)

def loudness(data):
    # Short-term loudness in dB: a 300 ms RMS of a K-weighting approximation
    # (bass roll-off below ~100 Hz, +4 dB presence shelf above ~1.5 kHz).
    weighted = []
    low = 0.
    shelf = 0.
    for value in data:
        low += .013 * (value - low)
        high = value - low
        shelf += .196 * (high - shelf)
        weighted.append(high + .58 * (high - shelf))
    window = min(len(weighted), int(RATE * .3))
    squares = [0.]
    for value in weighted:
        squares.append(squares[-1] + value * value)
    best = max((squares[i + window] - squares[i]) / window
               for i in range(0, len(weighted) - window + 1, int(RATE * .02)))
    return 10 * math.log10(max(best, 1e-12))


def soft_limit(value, knee=.75, ceiling=.89):
    # Gentle saturation above the knee keeps loudness matching from producing hard clipping.
    size = abs(value)
    if size <= knee:
        return value
    return math.copysign(knee + (ceiling - knee) * math.tanh((size - knee) / (ceiling - knee)), value)


# (seconds, short-term loudness target in dB, reverb mix). Feedback ticks sit well below the story hits,
# and the purchase animation climbs from the flip to the impact, gather, charge and release.
ANIME = dict(SmallPerkReveal=(1.2, -19, .2), SmallPerkFocus=(.4, -20, .12),
             SmallPerkHoldTick=(.3, -21, .1), SmallPerkCancel=(.35, -23, .1),
             SmallPerkUnavailable=(.4, -23, .06), SmallPerkPurchase=(.55, -18, .15),
             PerkFlip=(1.5, -19, .12), PerkImpact=(1.0, -14, .25), PerkGather=(2.6, -16, .25),
             PerkCharge=(.8, -16, .12), PerkRelease=(1.8, -11, .28))
TAVERN = dict(PerkReveal=.7, PerkFocus=.28, PerkHoldTick=.16, PerkCancel=.23,
              PerkUnavailable=.28, PerkPurchase=.7)


def tavern(name, data):
    if name == 'PerkReveal':
        wind(data, .42, .8, 3)
        chord(data, [392, 523.25, 659.25], duration=.4, gain=.12)
    elif name == 'PerkFocus':
        tone(data, 0, 784, .23, .21)
        tone(data, .025, 1568, .18, .045)
    elif name == 'PerkHoldTick':
        tone(data, 0, 1046.5, .12, .14)
    elif name == 'PerkCancel':
        chord(data, [659.25, 523.25], duration=.14, gain=.11, step=.05)
    elif name == 'PerkUnavailable':
        chord(data, [220, 196], duration=.15, gain=.18, step=.07)
    elif name == 'PerkPurchase':
        chord(data, [784, 1046.5, 1318.5], duration=.45, gain=.19, step=.045)
        tone(data, 0, 196, .18, .16)
    dry = data[:]
    for delay, gain in [(.041, .11), (.083, .055), (.137, .025)]:
        offset = int(delay * RATE)
        for i in range(offset, len(data)):
            data[i] += dry[i - offset] * gain
    return data, .7


def create(name):
    if name in TAVERN:
        data, target = tavern(name, [0.0] * int(TAVERN[name] * RATE))
        peak = max(abs(value) for value in data)
        scale = min(1.25, target / max(peak, 1e-6))
    else:
        duration, level, mix = ANIME[name]
        data = [0.0] * int(duration * RATE)
        anime(name, data)
        data = reverb(data, mix)
        # Anime cues are matched by perceived loudness, not by peak.
        scale = 10 ** ((level - loudness(data)) / 20)
        target = .89
    # Anime cues keep a 0.5 ms fade-in so transients such as the release crack stay sharp.
    attack = 240 if name in TAVERN else 24
    data = [value * scale * min(1, i / attack, (len(data) - 1 - i) / 720) for i, value in enumerate(data)]
    if name not in TAVERN:
        data = [soft_limit(value) for value in data]
    return data, target


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--only', nargs='*')
    selected = parser.parse_args().only
    template = (FOLDER / 'SuccessPop.wav.meta').read_text()
    report = {}
    montage = []
    for name in list(TAVERN) + list(ANIME):
        if selected and name not in selected:
            continue
        data, target = create(name)
        output = FOLDER / (name + '.wav')
        with wave.open(str(output), 'wb') as stream:
            stream.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
            stream.writeframes(b''.join(struct.pack('<h', round(value * 32767)) for value in data))
        meta = Path(str(output) + '.meta')
        guid = re.search(r'guid: (\w+)', meta.read_text())[1] if meta.exists() else uuid.uuid4().hex
        meta.write_text(re.sub(r'guid: \w+', 'guid: ' + guid, template))
        peak = max(abs(value) for value in data)
        assert 0 < peak <= target + 1e-6 and data[0] == 0 and data[-1] == 0
        report[name] = dict(seconds=round(len(data) / RATE, 3), peak=round(peak, 4),
                            rms=round(math.sqrt(sum(v * v for v in data) / len(data)), 4),
                            loudness=round(loudness(data), 1), guid=guid)
        montage.extend(data + [0.0] * int(RATE * .35))
    Path('Temp').mkdir(exist_ok=True)
    Path('Temp/perk-sfx-validation.json').write_text(json.dumps(report, indent=2))
    with wave.open('Temp/PerkSoundPreview.wav', 'wb') as stream:
        stream.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        stream.writeframes(b''.join(struct.pack('<h', round(value * 32767)) for value in montage))
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
