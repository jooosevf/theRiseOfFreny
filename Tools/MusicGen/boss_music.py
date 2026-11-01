# Generates Assets/Audio/Resources/Music/ciclope.ogg, the Ciclope's fight music (José, 2026-11-01:
# its own track while the boss is in the Arena). Same chiptune kit as arena_music_2.py, but heavier
# and faster: E Phrygian at 168 BPM (the F over E and the Bb tritone make it sinister), a square
# bass doubling the triangle an octave up, double kicks, a low drone under the riff and a lead that
# screams in the high part. 32 bars (~46 s) in four parts; loops without a seam (note tails wrap
# to the start, the last bar climbs back to E).
# Needs numpy and ffmpeg.  python3 boss_music.py <out.ogg>
import sys, subprocess, numpy as np

SR = 44100
BPM = 168
STEP = 60 / BPM / 4                 # one 16th
BARS = 32
L = int(round(BARS * 16 * STEP * SR))
out = np.zeros((L, 2))
rng = np.random.default_rng(50)

NOTES = {'C': 0, 'C#': 1, 'D': 2, 'D#': 3, 'E': 4, 'F': 5, 'F#': 6, 'G': 7, 'G#': 8, 'A': 9, 'Bb': 10, 'B': 11}
def freq(name):
    n, o = name[:-1], int(name[-1])
    return 440 * 2 ** ((NOTES[n] + 12 * (o + 1) - 69) / 12)

def octave(name, up):
    return name[:-1] + str(int(name[-1]) + up)

def add(signal, at, pan=0.0, gain=1.0):
    start = int(round(at * SR)) % L
    idx = (np.arange(len(signal)) + start) % L
    out[idx, 0] += signal * gain * (1 - pan) / 2 * 2 ** 0.5
    out[idx, 1] += signal * gain * (1 + pan) / 2 * 2 ** 0.5

def envelope(t, dur, attack, decay):
    env = np.minimum(1, t / attack)
    if decay:
        env *= np.exp(-t / decay)
    rel = min(len(t), int(0.015 * SR))
    env[-rel:] *= np.linspace(1, 0, rel)
    return env

def pulse(f, dur, duty=0.5, attack=0.003, decay=None, vibrato=0.0):
    t = np.arange(int(dur * SR)) / SR
    ph = f * t
    if vibrato:
        ph = ph + vibrato * np.sin(2 * np.pi * 6.5 * t) * np.clip(t * 4 - 0.5, 0, 1) / (2 * np.pi * 6.5) * f / 100
    w = np.where((ph % 1) < duty, 1.0, -1.0)
    return w * envelope(t, dur, attack, decay)

def triangle(f, dur, decay=None):
    t = np.arange(int(dur * SR)) / SR
    ph = (f * t) % 1
    w = np.round((4 * np.abs(ph - 0.5) - 1) * 8) / 8
    return w * envelope(t, dur, 0.002, decay)

def smooth(x, k):
    return np.convolve(x, np.ones(k) / k, mode='same')

def kick():
    t = np.arange(int(0.18 * SR)) / SR
    f = 38 + 150 * np.exp(-t / 0.02)
    return np.tanh(2.2 * np.sin(2 * np.pi * np.cumsum(f) / SR)) * np.exp(-t / 0.07)

def snare():
    t = np.arange(int(0.2 * SR)) / SR
    noise = rng.uniform(-1, 1, len(t))
    return 0.8 * noise * np.exp(-t / 0.07) + 0.5 * np.sin(2 * np.pi * 180 * t) * np.exp(-t / 0.03)

def hat(length=0.025):
    t = np.arange(int(length * SR)) / SR
    noise = rng.uniform(-1, 1, len(t))
    return (noise - smooth(noise, 6)) * np.exp(-t / (length / 3))

def crash():
    t = np.arange(int(1.4 * SR)) / SR
    noise = rng.uniform(-1, 1, len(t))
    return (noise - smooth(noise, 4)) * np.exp(-t / 0.4)

def gong():
    # a low metallic hit for the start of each part: detuned squares, slow decay
    t = np.arange(int(1.6 * SR)) / SR
    w = sum(np.sign(np.sin(2 * np.pi * f * t)) for f in (freq('E2'), freq('E2') * 1.007, freq('Bb2')))
    return smooth(w / 3, 10) * np.exp(-t / 0.5)

# the riff: the bass line in 16ths per bar (None = rest); E pedal with F and Bb stabs
RIFF = [
    ['E2', 'E2', 'E3', 'E2', 'E2', 'F2', 'E2', 'E2', 'E2', 'E2', 'E3', 'E2', 'G2', 'F2', 'E2', 'D2'],
    ['E2', 'E2', 'E3', 'E2', 'E2', 'F2', 'E2', 'E2', 'Bb2', 'Bb2', 'A2', 'A2', 'G2', 'G2', 'F2', 'F2'],
]
RIFF_C = [   # the middle part walks down to C and B
    ['C2', 'C2', 'C3', 'C2', 'C2', 'D2', 'C2', 'C2', 'C2', 'C2', 'C3', 'C2', 'E2', 'D2', 'C2', 'C2'],
    ['B1', 'B1', 'B2', 'B1', 'B1', 'C2', 'B1', 'B1', 'Bb1', 'Bb1', 'Bb2', 'Bb1', 'B1', 'B1', 'D2', 'D#2'],
]
# melodies, (note, 16ths) per bar; 'R' is a rest
MEL_A = [
    [('E5', 6), ('F5', 2), ('E5', 4), ('B4', 4)],
    [('C5', 4), ('B4', 4), ('Bb4', 6), ('R', 2)],
    [('E5', 6), ('F5', 2), ('G5', 4), ('F5', 2), ('E5', 2)],
    [('F5', 4), ('E5', 4), ('D5', 2), ('C5', 2), ('B4', 4)],
]
MEL_B = [
    [('B5', 4), ('Bb5', 4), ('A5', 4), ('G5', 4)],
    [('F5', 6), ('G5', 2), ('F5', 4), ('E5', 4)],
    [('B5', 2), ('C6', 2), ('B5', 2), ('Bb5', 2), ('B5', 8)],
    [('E6', 6), ('D6', 2), ('C6', 4), ('B5', 2), ('Bb5', 2)],
]
MEL_C = [
    [('G5', 8), ('E5', 8)],
    [('F#5', 8), ('D#5', 8)],
    [('G5', 8), ('C6', 8)],
    [('B5', 12), ('R', 4)],
]

# (first bar, bars, riff, melody, style)
PARTS = [
    (0, 4, RIFF, None, 'intro'),
    (4, 8, RIFF, MEL_A, 'stomp'),
    (12, 8, RIFF, MEL_B, 'blast'),
    (20, 4, RIFF_C, MEL_C, 'heavy'),
    (24, 8, RIFF, MEL_B, 'scream'),
]

def bar_time(b): return b * 16 * STEP

def lead(phrase, t0, duty, gain, up=0, pan=0.2):
    step = 0
    for name, length in phrase:
        if name != 'R':
            sig = pulse(freq(octave(name, up)), length * STEP * 0.95, duty=duty, decay=0.9, vibrato=1.6)
            add(sig, t0 + step * STEP, pan=pan, gain=gain)
            add(sig, t0 + (step + 3) * STEP, pan=-pan * 1.5, gain=gain * 0.28)   # echo
        step += length

for first, count, riff, melody, style in PARTS:
    for k in range(count):
        b = first + k
        t0 = bar_time(b)
        line = riff[k % len(riff)]
        last_of_part = k == count - 1

        # bass: the riff on the triangle, doubled by a square an octave up (the heavy part halves it)
        for i, note in enumerate(line):
            if note is None or (style == 'intro' and i % 2):
                continue
            if style == 'heavy' and i % 4:
                continue
            length = STEP * (3.8 if style == 'heavy' else 0.85)
            add(triangle(freq(note), length), t0 + i * STEP, gain=0.6)
            add(smooth(pulse(freq(octave(note, 1)), length, duty=0.25, decay=0.12), 3), t0 + i * STEP, pan=-0.15,
                gain=0.07 if style == 'intro' else 0.1)

        # the drone: a low held E and Bb (the tritone) under the intro and the heavy part
        if style in ('intro', 'heavy'):
            for n in (('E3', 'Bb3') if style == 'intro' else ('C3', 'G3')):
                sig = pulse(freq(n), 16 * STEP, duty=0.5, attack=0.3, decay=3.0)
                add(smooth(sig, 14), t0, gain=0.05)

        # lead
        if melody is not None:
            phrase = melody[k % len(melody)]
            if style == 'scream':
                lead(phrase, t0, 0.25, 0.12, pan=0.25)
                lead(phrase, t0, 0.5, 0.06, up=-1, pan=-0.3)
            elif style == 'heavy':
                lead(phrase, t0, 0.125, 0.14, pan=0.15)
            else:
                lead(phrase, t0, 0.5, 0.11, pan=0.2)

        # drums
        if k == 0:
            add(gong(), t0, gain=0.22)
            if style != 'intro':
                add(crash(), t0, pan=-0.2, gain=0.1)
        if style == 'intro':
            for s in (0, 8):
                add(kick(), t0 + s * STEP, gain=0.8)
            if last_of_part:
                for i in range(8, 16):   # build in
                    add(snare(), t0 + i * STEP, gain=0.06 + 0.03 * (i - 8))
            continue
        if style == 'heavy':
            add(kick(), t0, gain=0.85)
            add(kick(), t0 + 2 * STEP, gain=0.6)
            add(snare(), t0 + 8 * STEP, gain=0.4)
            for i in range(4):
                add(hat(0.06), t0 + i * 4 * STEP, pan=0.2, gain=0.08)
        else:
            # double kick: every 16th in the blast and the scream, a gallop in the stomp
            kicks = range(16) if style in ('blast', 'scream') else [0, 2, 3, 6, 8, 10, 11, 14]
            for s in kicks:
                add(kick(), t0 + s * STEP, gain=0.5 if style in ('blast', 'scream') else 0.7)
            for s in (4, 12):
                add(snare(), t0 + s * STEP, gain=0.38)
            for i in range(8):
                add(hat(), t0 + i * 2 * STEP, pan=0.2, gain=0.09 if i % 2 else 0.06)
        if last_of_part:
            for i in range(12, 16):   # a fill into the next part (into the start when it loops)
                add(snare(), t0 + i * STEP, gain=0.22)

out = np.tanh(out * 1.2)               # a little grit, like an overdriven chip
out /= np.max(np.abs(out)) / 0.38     # denser than the arena tracks: a lower peak gives the same loudness
pcm = (out * 32767).astype('<i2').tobytes()
subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-f', 's16le', '-ar', str(SR), '-ac', '2', '-i', '-',
                '-c:a', 'libvorbis', '-q:a', '5', sys.argv[1]], input=pcm, check=True)
print(f'{sys.argv[1]}: {L / SR:.1f} s')
