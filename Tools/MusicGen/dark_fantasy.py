# Dark Fantasy music for The Rise of Freny (José, 2026-11-02: the game's art direction is Dark
# Fantasy, so the music follows it). Everything here is synthesized from scratch, so the tracks
# are ours to ship. Style references only (nothing copied): Dark Souls (choir + orchestra), Diablo's
# Tristram (lonely plucked guitar over a drone), Darkest Dungeon (war drums, low strings),
# Blasphemous (Phrygian modes, bells).
#
#   python3 dark_fantasy.py menu    <out.ogg>   ~87 s, D minor, 66 BPM: guitar, drone, choir, bells
#   python3 dark_fantasy.py arena   <out.ogg>   ~96 s, C minor, 120 BPM: string gallop, war drums, brass, choir
#   python3 dark_fantasy.py ciclope <out.ogg>   ~55 s, E Phrygian, 140 BPM: organ, chanting choir, taiko
#
# Every track loops without a seam: note tails and the reverb wrap around to the start (the
# reverb is a circular convolution over the whole loop). Needs numpy and ffmpeg.
import sys, subprocess, numpy as np

SR = 44100
NOTES = {'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3, 'E': 4, 'F': 5, 'F#': 6, 'Gb': 6,
         'G': 7, 'G#': 8, 'Ab': 8, 'A': 9, 'A#': 10, 'Bb': 10, 'B': 11}


def midi(name):
    return NOTES[name[:-1]] + 12 * (int(name[-1]) + 1)


def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12)


class Song:
    def __init__(self, bpm, bars, seed):
        self.beat = 60.0 / bpm
        self.bar = 4 * self.beat
        self.L = int(round(bars * self.bar * SR))
        self.buses = {}
        self.rng = np.random.default_rng(seed)

    def bus(self, name):
        if name not in self.buses:
            self.buses[name] = np.zeros((self.L, 2))
        return self.buses[name]

    def add(self, name, sig, at, pan=0.0, gain=1.0):
        out = self.bus(name)
        if sig.ndim == 1:
            sig = np.stack([sig * np.sqrt((1 - pan) / 2), sig * np.sqrt((1 + pan) / 2)], axis=1) * np.sqrt(2)
        start = int(round(at * SR)) % self.L
        n = len(sig)
        while n > 0:   # wrap around the loop
            take = min(n, self.L - start)
            out[start:start + take] += sig[len(sig) - n:len(sig) - n + take] * gain
            n -= take
            start = 0

    def t(self, bar, beat=0.0):
        return bar * self.bar + beat * self.beat


# ---------- tools ----------

def env(n, attack, release, decay=None):
    t = np.arange(n) / SR
    e = np.minimum(1.0, t / max(attack, 1e-4))
    if decay:
        e = e * np.exp(-t / decay)
    r = min(n, int(release * SR))
    if r > 0:
        e[-r:] *= np.linspace(1, 0, r) ** 1.5
    return e


def spectral(x, curve):
    # filters a mono or stereo signal in the frequency domain; curve(freqs) -> gain
    X = np.fft.rfft(x, axis=0)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    g = curve(f)
    return np.fft.irfft(X * (g[:, None] if x.ndim == 2 else g), n=len(x), axis=0)


def lowpass(fc, order=2):
    return lambda f: 1 / np.sqrt(1 + (f / fc) ** (2 * order))


def highpass(fc, order=2):
    return lambda f: 1 / np.sqrt(1 + (fc / np.maximum(f, 1e-3)) ** (2 * order))


def formant(peaks, base=0.15):
    def curve(f):
        g = base * lowpass(500)(f)
        for fc, gain, width in peaks:
            g = g + gain * np.exp(-0.5 * ((f - fc) / width) ** 2)
        return g * highpass(80)(f)
    return curve


VOWEL_AH = [(750, 1.0, 110), (1150, 0.55, 130), (2600, 0.22, 220)]
VOWEL_OH = [(450, 1.0, 90), (800, 0.6, 110), (2700, 0.12, 220)]


def saw(freq_inst):
    # band-limited (polyBLEP) saw from an instantaneous frequency array
    dt = freq_inst / SR
    ph = np.cumsum(dt) % 1.0
    y = 2 * ph - 1
    lo = ph < dt
    x = ph[lo] / dt[lo]
    y[lo] -= x + x - x * x - 1
    hi = ph > 1 - dt
    x = (ph[hi] - 1) / dt[hi]
    y[hi] -= x * x + x + x + 1
    return y


def vib(n, f, rate, cents, delay, rng):
    t = np.arange(n) / SR
    depth = np.clip((t - delay) / 0.4, 0, 1) * cents
    wobble = np.sin(2 * np.pi * rate * t + rng.uniform(0, 6.28))
    return f * 2 ** (depth * wobble / 1200)


# ---------- instruments ----------

def strings(song, m, dur, attack=0.08, release=0.25, voices=3, cents=9, vibrato=10):
    n = int((dur + release) * SR)
    y = np.zeros(n)
    for i in range(voices):
        det = (i - (voices - 1) / 2) * cents
        f = vib(n, hz(m) * 2 ** (det / 1200), 5.2 + 0.3 * i, vibrato, 0.25, song.rng)
        y += saw(f)
    return y / voices * env(n, attack, release)


def spiccato(song, m, dur):
    n = int(max(dur, 0.12) * SR)
    y = saw(np.full(n, hz(m))) + saw(np.full(n, hz(m) * 1.004))
    return 0.5 * y * env(n, 0.006, 0.05, decay=0.09)


def brass(song, m, dur, release=0.2):
    n = int((dur + release) * SR)
    f = vib(n, hz(m), 5.0, 8, 0.35, song.rng)
    bright = 0.5 * (saw(f) + saw(f * 1.003))
    dark = spectral(bright, lowpass(hz(m) * 3, 2))
    t = np.arange(n) / SR
    sweep = 0.25 + 0.75 * np.exp(-t / 0.18) * np.minimum(1, t / 0.05)
    return (dark + sweep * (bright - dark)) * env(n, 0.04, release)


def choir(song, m, dur, attack=0.35, release=0.6):
    n = int((dur + release) * SR)
    y = np.zeros(n)
    for i in range(5):
        det = song.rng.uniform(-14, 14)
        f = vib(n, hz(m) * 2 ** (det / 1200), song.rng.uniform(4.6, 5.8), 18, 0.2, song.rng)
        y += saw(f)
    breath = spectral(song.rng.uniform(-1, 1, n), lambda f: np.exp(-0.5 * ((f - 1800) / 900) ** 2))
    return (y / 5 + 0.25 * breath) * env(n, attack, release)


def chant(song, m, dur):
    return choir(song, m, dur, attack=0.03, release=0.12)


def organ(song, m, dur, release=0.3):
    n = int((dur + release) * SR)
    t = np.arange(n) / SR
    f = hz(m)
    y = np.zeros(n)
    for ratio, amp in ((0.5, 0.6), (1, 1.0), (2, 0.7), (3, 0.35), (4, 0.45), (6, 0.2), (8, 0.25)):
        if f * ratio < 9000:
            y += amp * np.sin(2 * np.pi * f * ratio * t + ratio)
    trem = 1 + 0.06 * np.sin(2 * np.pi * 5.8 * t)
    return y / 3.5 * trem * env(n, 0.03, release)


def pluck(song, m, dur, bright=0.5):
    # Karplus-Strong, worked a whole period at a time
    f = hz(m)
    p = int(round(SR / f))
    n = int(dur * SR) + p
    y = np.zeros(n)
    noise = song.rng.uniform(-1, 1, p)
    y[:p] = spectral(noise, lowpass(f * (6 + 20 * bright), 1))
    damp = 0.996 if f < 300 else 0.993
    for s in range(p, n, p):
        prev = y[s - p:s]
        avg = 0.5 * (prev + np.roll(prev, 1))
        take = min(p, n - s)
        y[s:s + take] = (damp * avg)[:take]
    y = y[p:]
    return y * env(len(y), 0.002, 0.08)


def bell(song, m, dur=6.0):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = hz(m)
    y = np.zeros(n)
    for ratio, amp, dec in ((0.5, 0.6, 3.5), (1.0, 1.0, 2.5), (1.19, 0.5, 1.8), (1.5, 0.35, 1.6),
                            (2.0, 0.45, 1.2), (2.51, 0.3, 0.9), (2.66, 0.25, 0.8), (3.01, 0.2, 0.6),
                            (4.1, 0.15, 0.4), (5.4, 0.08, 0.25)):
        y += amp * np.sin(2 * np.pi * f * ratio * t) * np.exp(-t / dec)
    return y / 3 * env(n, 0.001, 0.3)


def taiko(song, pitch=1.0, dur=1.2):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = (44 + 70 * np.exp(-t / 0.04)) * pitch
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / (0.35 / pitch))
    skin = np.sin(2 * np.pi * 118 * pitch * t) * np.exp(-t / 0.12) * 0.35
    hit = spectral(song.rng.uniform(-1, 1, n), lowpass(900 * pitch, 2)) * np.exp(-t / 0.03) * 0.9
    return (body + skin + hit) * env(n, 0.001, 0.1)


def frame_drum(song, dur=0.4):
    n = int(dur * SR)
    t = np.arange(n) / SR
    rattle = spectral(song.rng.uniform(-1, 1, n), lambda f: np.exp(-0.5 * ((f - 2400) / 1500) ** 2))
    body = np.sin(2 * np.pi * (170 + 60 * np.exp(-t / 0.01)) * t) * np.exp(-t / 0.07)
    return (1.6 * rattle * np.exp(-t / 0.08) + 0.6 * body) * env(n, 0.001, 0.05)


def swell(song, dur):
    # a reversed cymbal: noise rising into the next section
    n = int(dur * SR)
    y = spectral(song.rng.uniform(-1, 1, n), highpass(2500, 1))
    return y * np.linspace(0, 1, n) ** 3


def wind(song, gain=1.0):
    n = song.L
    noise = song.rng.uniform(-1, 1, (n, 2))
    y = spectral(noise, lambda f: np.exp(-0.5 * ((f - 500) / 350) ** 2))
    t = np.arange(n) / SR
    k = song.L / SR
    # the swells repeat a whole number of times per loop, so they wrap without a seam
    mod = 0.55 + 0.45 * np.sin(2 * np.pi * 3 * t / k) * np.sin(2 * np.pi * 7 * t / k + 1)
    return y * mod[:, None] * gain


# ---------- writing music ----------

def phrase(text):
    # "D5:3 E5:0.5 R:1 | ..." -> [(beat, beats, midi or None)]
    out, beat = [], 0.0
    for tok in text.replace('|', ' ').split():
        name, beats = tok.split(':')
        beats = float(beats)
        out.append((beat, beats, None if name == 'R' else midi(name)))
        beat += beats
    return out


def play(song, bus, start_bar, text, inst, pan=0.0, gain=1.0, legato=1.0, shift=0):
    for beat, beats, m in phrase(text):
        if m is not None:
            sig = inst(song, m + shift, beats * song.beat * legato)
            song.add(bus, sig, song.t(start_bar, beat), pan, gain)


CHORDS = {
    'Dm': ['D', 'F', 'A'], 'Bb': ['Bb', 'D', 'F'], 'Gm': ['G', 'Bb', 'D'], 'A': ['A', 'C#', 'E'],
    'F': ['F', 'A', 'C'], 'Eb': ['Eb', 'G', 'Bb'], 'Cm': ['C', 'Eb', 'G'], 'Ab': ['Ab', 'C', 'Eb'],
    'G': ['G', 'B', 'D'], 'Fm': ['F', 'Ab', 'C'], 'Db': ['Db', 'F', 'Ab'], 'Em': ['E', 'G', 'B'],
    'E': ['E', 'G#', 'B'], 'C': ['C', 'E', 'G'],
}


def chord(name, octave):
    notes = CHORDS[name]
    root = midi(notes[0] + str(octave))
    out = []
    for n in notes:
        m = midi(n + str(octave))
        out.append(m + 12 if m < root else m)
    return out


def mixdown(song, plan, reverb_seconds, wet, out_path, target_rms):
    L = song.L
    dry = np.zeros((L, 2))
    send = np.zeros((L, 2))
    for name, (gain, curve, rev) in plan.items():
        if name not in song.buses:
            continue
        x = song.buses[name]
        if curve is not None:
            x = spectral(x, curve)
        dry += x * gain
        send += x * gain * rev
    # reverb: decaying stereo noise, darker as it fades, convolved around the loop
    n = int(reverb_seconds * SR)
    t = np.arange(n) / SR
    ir = song.rng.uniform(-1, 1, (n, 2)) * np.exp(-t / (reverb_seconds / 6.5))[:, None]
    ir = spectral(ir, lowpass(4500, 1))
    ir[:int(0.012 * SR)] = 0   # pre-delay
    pad = np.zeros((L, 2))
    pad[:n] = ir
    wet_sig = np.fft.irfft(np.fft.rfft(send, axis=0) * np.fft.rfft(pad, axis=0), n=L, axis=0)
    wet_sig *= np.sqrt(np.mean(send ** 2) / max(np.mean(wet_sig ** 2), 1e-12))
    mix = dry + wet * wet_sig
    mix = spectral(mix, highpass(30, 2))
    mix *= target_rms / np.sqrt(np.mean(mix ** 2))
    mix = np.tanh(mix * 1.1) / 1.1          # gentle limiter
    mix *= min(1.0, 0.89 / np.max(np.abs(mix)))
    pcm = (np.clip(mix, -1, 1) * 32767).astype('<i2').tobytes()
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-f', 's16le', '-ar', str(SR), '-ac', '2',
                    '-i', '-', '-c:a', 'libvorbis', '-q:a', '5', out_path], input=pcm, check=True)


# ---------- the tracks ----------

def menu(out):
    # "O Despertar": a lonely guitar over a drone, then cello, then the choir takes the theme.
    s = Song(66, 24, 11)
    prog = ['Dm', 'Bb', 'Gm', 'A', 'Dm', 'F', 'Eb', 'A']
    theme = ('D5:3 E5:0.5 F5:0.5 | D5:2 C5:1 Bb4:1 | G4:3 A4:0.5 Bb4:0.5 | A4:2 C#5:1 E5:1 | '
             'F5:3 E5:0.5 D5:0.5 | C5:2 A4:1 C5:1 | Bb4:2 G4:1 Eb5:1 | D5:1 C#5:1 A4:2')
    for bar in range(24):
        c = prog[bar % 8]
        r, th, fi = chord(c, 3)
        # guitar: root, fifth, octave, third above, ... in eighths
        pattern = [r - 12, fi - 12, r, th, r + 12, th, fi, r]
        if bar >= 22:
            pattern = pattern[:4] + [None] * 4
        for i, m in enumerate(pattern):
            if m is not None:
                s.add('guitar', pluck(s, m, 2.5, bright=0.35), s.t(bar, i * 0.5) + s.rng.uniform(0, 0.012),
                      pan=-0.25 + 0.5 * (i % 2), gain=0.9 if i else 1.1)
        # drone on the root, a long held low string
        s.add('drone', strings(s, r - 24, s.bar, attack=0.6, release=0.8, vibrato=4), s.t(bar), gain=0.7)
        if bar >= 8 and bar < 22:
            for m in chord(c, 3):
                s.add('pad', choir(s, m, s.bar * 0.98, attack=0.8, release=1.0), s.t(bar),
                      pan=s.rng.uniform(-0.5, 0.5), gain=0.35)
    play(s, 'cello', 8, theme, lambda song, m, d: strings(song, m, d, attack=0.12, release=0.4, voices=2, vibrato=14),
         pan=0.15, shift=-12, legato=0.98)
    play(s, 'choir', 16, theme, lambda song, m, d: choir(song, m, d, attack=0.15, release=0.5), legato=0.97)
    for bar in (0, 4, 8, 12, 16, 18, 20):
        s.add('bells', bell(s, midi('D4')), s.t(bar), pan=0.3, gain=0.5)
    s.add('wind', wind(s), 0)
    mixdown(s, {
        'guitar': (0.9, lowpass(5000), 0.45),
        'drone': (0.35, lowpass(400), 0.5),
        'pad': (0.35, formant(VOWEL_OH), 0.9),
        'cello': (0.55, lowpass(2200), 0.6),
        'choir': (0.8, formant(VOWEL_AH), 0.9),
        'bells': (0.35, lowpass(6000), 1.0),
        'wind': (0.035, None, 0.2),
    }, 4.5, 0.55, out, 0.085)


def arena(out):
    # "Arena de Sangue": galloping low strings, war drums, a brass theme the choir takes over,
    # a Phrygian breakdown and a final climax. 6 parts of 8 bars.
    s = Song(120, 48, 23)
    prog = ['Cm', 'Ab', 'Eb', 'G', 'Cm', 'Ab', 'Fm', 'G']
    dark = ['Cm', 'Db', 'Cm', 'Db', 'Ab', 'Fm', 'G', 'G']
    theme = ('C4:1 Eb4:0.5 G4:0.5 C5:2 | C5:1 Bb4:0.5 Ab4:0.5 Eb4:2 | Bb4:1 G4:0.5 Bb4:0.5 Eb5:2 | '
             'D5:1.5 C5:0.5 B4:2 | C5:1 Eb5:0.5 D5:0.5 C5:1 G4:1 | Ab4:1 C5:1 Eb5:2 | '
             'F5:1 Eb5:0.5 D5:0.5 C5:1 Ab4:1 | G4:1.5 Ab4:0.5 B4:2')
    theme2 = ('G5:2 Eb5:1 C5:1 | Ab5:2 Eb5:1 C5:1 | G5:1 F5:0.5 Eb5:0.5 Bb4:2 | B4:1 D5:1 G5:2 | '
              'C6:2 G5:1 Eb5:1 | Eb5:1 F5:0.5 G5:0.5 Ab5:2 | F5:1 G5:0.5 Ab5:0.5 G5:1 F5:1 | D5:2 B4:2')
    for bar in range(48):
        part = bar // 8
        c = (dark if part == 4 else prog)[bar % 8]
        r = chord(c, 2)[0]
        # string gallop: eighth + two sixteenths, root with octave jumps
        if part != 4:
            for k, step in enumerate([0, 2, 3, 4, 6, 7, 8, 10, 11, 12, 14, 15]):
                m = r + (12 if step in (4, 12) and part >= 2 else 0)
                g = 1.0 if step % 4 == 0 else 0.65
                s.add('gallop', spiccato(s, m, 0.12), s.t(bar, step / 4), pan=-0.3, gain=g)
                s.add('gallop', spiccato(s, m + 12, 0.12), s.t(bar, step / 4), pan=0.3, gain=g * 0.6)
        # held chords
        if part in (1, 2, 3, 5):
            for m in chord(c, 3):
                s.add('pad', choir(s, m, s.bar * 0.97, attack=0.25), s.t(bar), pan=s.rng.uniform(-0.6, 0.6), gain=0.3)
            s.add('low', strings(s, r - 12, s.bar, attack=0.05, release=0.3), s.t(bar), gain=0.6)
        if part == 4:
            for m in chord(c, 3):
                s.add('organ', organ(s, m, s.bar * 0.98), s.t(bar), gain=0.45)
            s.add('low', strings(s, r - 12, s.bar, attack=0.4, release=0.5), s.t(bar), gain=0.7)
            # harpsichord-like arpeggio
            notes = chord(c, 4)
            for i in range(8):
                m = notes[[0, 1, 2, 1][i % 4]] + (12 if i >= 4 else 0)
                s.add('guitar', pluck(s, m, 0.8, bright=0.9), s.t(bar, i * 0.5), pan=0.4 - 0.1 * i, gain=0.6)
        # drums
        if part == 0:
            for step in (0, 8):
                s.add('taiko', taiko(s), s.t(bar, step / 4), gain=0.9)
            if bar % 2 == 1:
                s.add('taiko', taiko(s, 1.6, 0.6), s.t(bar, 3.5), pan=0.3, gain=0.5)
        elif part == 4:
            for step in (0, 3):   # heartbeat
                s.add('taiko', taiko(s, 0.9), s.t(bar, step / 4), gain=0.7 if step == 0 else 0.45)
        else:
            for step in (0, 3, 8, 11):
                s.add('taiko', taiko(s), s.t(bar, step / 4), gain=1.0 if step in (0, 8) else 0.6)
            for step in (4, 12):
                s.add('perc', frame_drum(s), s.t(bar, step / 4), gain=0.8)
            if part >= 2:
                for step in (2, 6, 10, 14):
                    s.add('perc', frame_drum(s, 0.2), s.t(bar, step / 4), pan=0.4, gain=0.3)
        if bar % 8 == 7 and part != 4:
            for i, step in enumerate((12, 13, 14, 15)):
                s.add('taiko', taiko(s, 1.9 - 0.2 * i, 0.5), s.t(bar, step / 4), pan=-0.4 + 0.25 * i, gain=0.6)
        if bar % 8 == 7:
            s.add('fx', swell(s, s.bar), s.t(bar), gain=0.5)
        if bar % 8 == 0 and part != 4:
            s.add('bells', bell(s, midi('C3')), s.t(bar), gain=0.55)
    hb = lambda song, m, d: brass(song, m, d)
    play(s, 'brass', 16, theme, hb, pan=-0.1, legato=0.95)
    play(s, 'brass', 24, theme, hb, pan=-0.1, legato=0.95, shift=-12)
    play(s, 'choir', 24, theme2, lambda song, m, d: choir(song, m, d, attack=0.1, release=0.4), legato=0.97)
    play(s, 'choir', 32, 'C5:4 | Db5:4 | C5:4 | Db5:4 | Eb5:4 | F5:4 | D5:4 | B4:4',
         lambda song, m, d: choir(song, m, d, attack=0.6, release=0.8), pan=0.2)
    play(s, 'brass', 40, theme, hb, legato=0.95)
    play(s, 'choir', 40, theme2, lambda song, m, d: choir(song, m, d, attack=0.1, release=0.4), legato=0.97)
    mixdown(s, {
        'gallop': (0.42, lowpass(2500), 0.25),
        'low': (0.4, lowpass(700), 0.35),
        'pad': (0.3, formant(VOWEL_OH), 0.7),
        'organ': (0.4, lowpass(3500), 0.7),
        'guitar': (0.55, lowpass(6000), 0.6),
        'brass': (0.5, lowpass(3200), 0.45),
        'choir': (0.75, formant(VOWEL_AH), 0.8),
        'taiko': (1.0, lowpass(3000), 0.3),
        'perc': (0.35, None, 0.3),
        'bells': (0.35, lowpass(6000), 0.9),
        'fx': (0.15, None, 0.5),
    }, 3.2, 0.42, out, 0.13)


def ciclope(out):
    # "O Olho do Abismo": the boss. Organ, a chanting choir, taiko and brass in E Phrygian.
    s = Song(140, 32, 37)
    prog = ['Em', 'F', 'Em', 'F', 'Em', 'Dm', 'F', 'E']
    theme = ('E4:1 F4:0.5 E4:0.5 B4:2 | C5:1 B4:0.5 A4:0.5 F4:2 | G4:1 F4:0.5 E4:0.5 G4:1 B4:1 | '
             'A4:1.5 G4:0.5 F4:2 | E5:1 D5:0.5 C5:0.5 B4:2 | A4:1 F4:1 D4:2 | C5:1 A4:1 F4:1 C5:1 | '
             'B4:2 G#4:1 F4:1')
    for bar in range(32):
        part = bar // 8
        c = prog[bar % 8]
        notes = chord(c, 3)
        r = chord(c, 2)[0]
        for m in notes:
            s.add('organ', organ(s, m, s.bar * 0.98), s.t(bar), gain=0.5 if part != 1 else 0.35)
        s.add('organ', organ(s, r - 12, s.bar * 0.98), s.t(bar), gain=0.45)
        # low strings: eighths on E with the F half step leaning on it
        for i in range(8):
            m = r if i not in (3, 7) else r + 1
            s.add('gallop', spiccato(s, m, 0.14), s.t(bar, i * 0.5), pan=-0.2, gain=1.0 if i % 2 == 0 else 0.7)
        # chant: short syllables on chord tones
        if part >= 1:
            for beat, beats, idx in ((0, 0.5, 0), (0.5, 0.5, 0), (1, 1, 1), (2, 0.5, 2), (2.5, 0.5, 1), (3, 1, 0)):
                for k, m in enumerate(chord(c, 4)[:2]):
                    s.add('chant', chant(s, chord(c, 4)[idx] - 12 * k, beats * s.beat * 0.8), s.t(bar, beat),
                          pan=-0.4 + 0.8 * k, gain=0.45)
        # drums
        for step in range(0, 16, 2 if part < 2 else 1):
            if step % 4 == 0:
                s.add('taiko', taiko(s), s.t(bar, step / 4), gain=1.0 if step in (0, 8) else 0.75)
            elif part >= 2:
                s.add('taiko', taiko(s, 1.5 + 0.1 * (step % 3), 0.4), s.t(bar, step / 4), pan=0.3 - 0.2 * (step % 3), gain=0.35)
            else:
                s.add('taiko', taiko(s, 1.3, 0.5), s.t(bar, step / 4), pan=0.25, gain=0.4)
        if part >= 1:
            for step in (4, 12):
                s.add('perc', frame_drum(s), s.t(bar, step / 4), gain=0.8)
        if bar % 2 == 0:
            s.add('bells', bell(s, midi('E3'), 4.0), s.t(bar), pan=-0.2, gain=0.6)
        if bar % 8 == 7:
            s.add('fx', swell(s, s.bar), s.t(bar), gain=0.6)
    hb = lambda song, m, d: brass(song, m, d)
    play(s, 'brass', 16, theme, hb, legato=0.95)
    play(s, 'brass', 16, theme, hb, legato=0.95, shift=-12, pan=0.2, gain=0.6)
    play(s, 'brass', 24, theme, hb, legato=0.95, shift=-12)
    play(s, 'choir', 24, theme, lambda song, m, d: choir(song, m, d, attack=0.08, release=0.3), legato=0.97, shift=12)
    mixdown(s, {
        'organ': (0.42, lowpass(3000), 0.6),
        'gallop': (0.45, lowpass(2000), 0.25),
        'chant': (0.6, formant(VOWEL_AH), 0.6),
        'brass': (0.55, lowpass(3000), 0.4),
        'choir': (0.6, formant(VOWEL_AH), 0.8),
        'taiko': (1.0, lowpass(3000), 0.3),
        'perc': (0.35, None, 0.3),
        'bells': (0.4, lowpass(5000), 0.9),
        'fx': (0.15, None, 0.5),
    }, 3.0, 0.4, out, 0.13)


if __name__ == '__main__':
    {'menu': menu, 'arena': arena, 'ciclope': ciclope}[sys.argv[1]](sys.argv[2])
