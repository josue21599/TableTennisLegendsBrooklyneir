"""Generates the original sound effects and music loops for Table Tennis Legends Brooklyneir.
Everything is synthesised from scratch (no samples), mono 16-bit 22.05 kHz WAV."""
import numpy as np, wave, os, sys
SR = 22050
OUT = sys.argv[1]
rng = np.random.default_rng(7)

def t(d): return np.arange(int(d * SR)) / SR
def env_exp(d, tau): return np.exp(-t(d) / tau)
def norm(x, peak=0.9):
    m = np.max(np.abs(x)) or 1.0
    return x / m * peak
def lowpass(x, a):
    y = np.zeros_like(x); acc = 0.0
    av = np.broadcast_to(np.asarray(a, dtype=float), x.shape)
    for i in range(len(x)):
        acc += av[i] * (x[i] - acc); y[i] = acc
    return y
def highpass(x, a): return x - lowpass(x, a)
def noise(d): return rng.uniform(-1, 1, int(d * SR))
def tone(f, d, shape='sine'):
    ph = 2 * np.pi * np.cumsum(np.full(int(d * SR), f) if np.isscalar(f) else f) / SR
    if shape == 'sine': return np.sin(ph)
    if shape == 'saw': return 2 * ((ph / (2 * np.pi)) % 1.0) - 1
    if shape == 'square': return np.sign(np.sin(ph))
    if shape == 'tri': return 2 * np.abs(2 * ((ph / (2 * np.pi)) % 1.0) - 1) - 1
def pad(x, d):
    n = int(d * SR)
    return np.concatenate([x, np.zeros(max(0, n - len(x)))])[:max(n, len(x))]
def mix(*parts):
    n = max(len(p) for p in parts); out = np.zeros(n)
    for p in parts: out[:len(p)] += p
    return out
def save(name, x, peak=0.9, folder='SFX'):
    x = norm(x, peak)
    fade = min(len(x), int(0.004 * SR))
    x[-fade:] *= np.linspace(1, 0, fade)
    path = os.path.join(OUT, folder, name + '.wav')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype('<i2').tobytes())

def tock(freq, dur, body=0.3, click=0.5, tau=0.012):
    tt = t(dur)
    f = freq * (1 + 0.15 * np.exp(-tt / 0.004))
    ping = tone(f, dur) * np.exp(-tt / tau)
    h2 = tone(f * 2.37, dur) * np.exp(-tt / (tau * 0.6)) * 0.35
    thump = tone(freq * 0.22, dur) * np.exp(-tt / (tau * 1.6)) * body
    cl = highpass(noise(dur), 0.5) * np.exp(-tt / 0.0015) * click
    return ping + h2 + thump + cl

def sweep(f0, f1, dur, shape='sine', curve=1.0):
    k = np.linspace(0, 1, int(dur * SR)) ** curve
    return tone(f0 + (f1 - f0) * k, dur, shape)

def arp(notes, step, dur_each, shape='tri', tau=0.12, detune=0.0):
    total = step * (len(notes) - 1) + dur_each
    out = np.zeros(int(total * SR) + 1)
    for i, n in enumerate(notes):
        tt = t(dur_each)
        s = tone(n, dur_each, shape) + 0.5 * tone(n * 2, dur_each, 'sine') * 0.4
        if detune: s += tone(n * (1 + detune), dur_each, shape) * 0.5
        s *= np.exp(-tt / tau) * np.minimum(1, tt * 400)
        a = int(i * step * SR); out[a:a + len(s)] += s
    return out

def midi(n): return 440.0 * 2 ** ((n - 69) / 12)

# ---------------- gameplay
save('hit_clean', tock(1250, 0.12, body=0.35, click=0.6))
save('hit_soft', tock(900, 0.11, body=0.25, click=0.3, tau=0.016), 0.7)
smash = mix(tock(760, 0.25, body=0.9, click=0.9, tau=0.02),
            lowpass(noise(0.18), 0.35) * env_exp(0.18, 0.03) * 0.8,
            sweep(1800, 600, 0.2) * env_exp(0.2, 0.05) * 0.3)
save('hit_smash', smash, 0.95)
save('bounce', tock(1900, 0.07, body=0.15, click=0.4, tau=0.007), 0.75)
save('serve', tock(1100, 0.11, body=0.3, click=0.5), 0.8)
net = mix(lowpass(noise(0.16), 0.25) * env_exp(0.16, 0.035), tone(320, 0.16) * env_exp(0.16, 0.02) * 0.4)
save('net', net, 0.7)
wn = noise(0.3); k = np.linspace(0, 1, len(wn))
whoosh = highpass(lowpass(wn, 0.08 + 0.3 * k), 0.02) * np.sin(np.pi * k) ** 1.5
save('whoosh', whoosh, 0.6)
save('point', arp([midi(76), midi(83)], 0.08, 0.35, 'tri', 0.15), 0.7)

# ---------------- powers
fire = mix(sweep(180, 900, 0.45, 'saw', 0.6) * np.sin(np.pi * np.linspace(0, 1, int(0.45 * SR))) * 0.5,
           lowpass(noise(0.5), 0.2) * env_exp(0.5, 0.18) * 0.7)
save('power_fire', fire)
slow = mix(sweep(900, 180, 0.6, 'tri', 0.7) * np.exp(-t(0.6) / 0.35), sweep(905, 182, 0.6, 'sine', 0.7) * 0.5 * np.exp(-t(0.6) / 0.35))
save('power_slow', slow, 0.75)
curve = mix(sweep(400, 1300, 0.35, 'tri', 1.6) * np.sin(np.pi * np.linspace(0, 1, int(0.35 * SR))), arp([midi(79)], 0.1, 0.25) * 0.4)
save('power_curve', curve, 0.75)
psm = mix(sweep(90, 500, 0.45, 'saw', 0.5) * np.exp(-t(0.45) / 0.3), tock(500, 0.4, body=1.0, click=0.8, tau=0.05) * 0.6)
save('power_smash', psm)
save('power_shield', arp([midi(60), midi(64), midi(67), midi(72)], 0.06, 0.4, 'tri', 0.25, 0.004), 0.75)
save('power_freeze', mix(arp([midi(96), midi(91), midi(88), midi(84)], 0.05, 0.35, 'sine', 0.18), highpass(noise(0.4), 0.6) * env_exp(0.4, 0.12) * 0.25), 0.7)
save('power_random', arp([midi(72), midi(79), midi(76), midi(84), midi(88)], 0.045, 0.25, 'square', 0.08), 0.6)
save('pickup', arp([midi(79), midi(86), midi(91)], 0.05, 0.3, 'tri', 0.12), 0.7)
save('special', mix(sweep(150, 1600, 0.6, 'saw', 1.4) * np.exp(-t(0.6) / 0.4) * 0.6, arp([midi(72), midi(76), midi(79), midi(84)], 0.07, 0.5, 'tri', 0.2)), 0.85)
save('shield_save', mix(sweep(1400, 400, 0.35, 'tri') * np.exp(-t(0.35) / 0.15), tock(600, 0.3, body=0.6, click=0.5, tau=0.03) * 0.6), 0.8)

# ---------------- ui & progression
save('ui_click', tock(2600, 0.05, body=0.0, click=0.3, tau=0.006), 0.45)
save('ui_back', tock(1500, 0.06, body=0.05, click=0.2, tau=0.008), 0.45)
save('ui_error', arp([midi(57), midi(53)], 0.08, 0.18, 'square', 0.06), 0.45)
save('coin', arp([midi(88), midi(95)], 0.06, 0.3, 'sine', 0.12), 0.55)
save('unlock', arp([midi(72), midi(76), midi(79), midi(84), midi(88)], 0.08, 0.6, 'tri', 0.3, 0.005), 0.8)
save('countdown', arp([midi(81)], 0.1, 0.22, 'sine', 0.08), 0.6)
save('countdown_go', arp([midi(88)], 0.1, 0.5, 'tri', 0.25, 0.006), 0.75)
win = mix(arp([midi(72), midi(76), midi(79), midi(84)], 0.11, 0.7, 'saw', 0.25, 0.004) * 0.6,
          arp([midi(60), midi(67)], 0.33, 0.9, 'tri', 0.5) * 0.5)
save('win', lowpass(win, 0.35), 0.85)
save('lose', lowpass(arp([midi(67), midi(63), midi(60), midi(55)], 0.18, 0.5, 'tri', 0.3), 0.3), 0.7)
# crowd cheer: band-limited noise with many amplitude-modulated 'voices'
d = 1.6; n = noise(d); crowd = lowpass(highpass(n, 0.05), 0.25)
mod = np.zeros(len(crowd))
for i in range(40):
    c = rng.uniform(0, d); w = rng.uniform(0.15, 0.6)
    tt = t(d); mod += np.exp(-((tt - c) / w) ** 2) * rng.uniform(0.3, 1.0)
tt = t(d)[:len(crowd)]; mod = mod[:len(crowd)]
crowd *= (0.4 + mod / mod.max()) * np.minimum(1, tt * 4) * np.minimum(1, (d - tt) * 2.5)
save('cheer', crowd, 0.6)

# ---------------- music (original loops)
def drum_kit(bpm, bars, swing=0.0, hat_level=0.12):
    beat = 60 / bpm; total = beat * 4 * bars; out = np.zeros(int(total * SR) + SR)
    kick = tone(55 + 90 * np.exp(-t(0.3) / 0.03), 0.3) * env_exp(0.3, 0.09)
    snare = lowpass(noise(0.2), 0.5) * env_exp(0.2, 0.05) + tone(190, 0.2) * env_exp(0.2, 0.03) * 0.5
    hat = highpass(noise(0.05), 0.7) * env_exp(0.05, 0.012)
    for b in range(bars * 4):
        a = int(b * beat * SR)
        if b % 4 in (0, 2) or (b % 8 == 7): out[a:a + len(kick)] += kick * 0.9
        if b % 4 in (1, 3): out[a:a + len(snare)] += snare * 0.6
        for h in range(2):
            ha = int((b + h * 0.5 + (swing if h else 0)) * beat * SR)
            out[ha:ha + len(hat)] += hat * hat_level * (1.3 if h == 0 else 0.8)
    return out[:int(total * SR)]

def synth_line(notes, bpm, bars, steps_per_beat, shape, tau, level, octave_mul=1.0, lp=0.2):
    beat = 60 / bpm; step = beat / steps_per_beat; total = beat * 4 * bars
    out = np.zeros(int(total * SR) + SR)
    for i, n in enumerate(notes):
        if n is None: continue
        a = int(i * step * SR); dur = step * 1.8
        tt = t(dur); s = tone(midi(n) * octave_mul, dur, shape) + tone(midi(n) * octave_mul * 1.005, dur, shape) * 0.6
        s *= np.exp(-tt / tau) * np.minimum(1, tt * 300)
        out[a:a + len(s)] += s * level
    return lowpass(out[:int(total * SR)], lp)

def chords(prog, bpm, bars, level):
    beat = 60 / bpm; total = beat * 4 * bars; out = np.zeros(int(total * SR) + SR)
    for i, ch in enumerate(prog):
        a = int(i * beat * 4 * SR); dur = beat * 4
        tt = t(dur); s = np.zeros(len(tt))
        for n in ch: s += tone(midi(n), dur, 'saw') * 0.3 + tone(midi(n) * 1.003, dur, 'tri') * 0.4
        s *= np.minimum(1, tt * 8) * np.exp(-tt / 3.0)
        out[a:a + len(s)] += s * level
    return lowpass(out[:int(total * SR)], 0.08)

def loop(name, bpm, prog, bass_pattern, lead, hat):
    bars = len(prog)
    drums = drum_kit(bpm, bars, 0.08, hat)
    bass = synth_line(bass_pattern, bpm, bars, 2, 'saw', 0.18, 0.55, 0.5, 0.12)
    pads = chords(prog, bpm, bars, 0.35)
    ld = synth_line(lead, bpm, bars, 4, 'square', 0.08, 0.16, 1.0, 0.25)
    n = min(len(drums), len(bass), len(pads), len(ld))
    music = drums[:n] + bass[:n] + pads[:n] + ld[:n]
    # make the loop seamless: crossfade the tail into the head
    save(name, music, 0.8, 'Music')

# Menu: relaxed street groove (F minor-ish).
prog_menu = [[53, 56, 60], [49, 53, 56], [56, 60, 63], [51, 55, 58]] * 2
bass_menu = []
for ch in prog_menu: r = ch[0] - 12; bass_menu += [r, None, r, r + 7, None, r, r + 10, r + 7]
lead_menu = []
for bi, ch in enumerate(prog_menu):
    pat = [ch[2] + 12, None, ch[1] + 12, None, ch[0] + 12, None, None, None, ch[2] + 12, None, ch[1] + 12, ch[0] + 12, None, None, None, None]
    lead_menu += pat if bi % 2 == 0 else [None] * 16
loop('menu_loop', 92, prog_menu, bass_menu, lead_menu, 0.08)

# Match: energetic arcade drive (A minor-ish).
prog_match = [[57, 60, 64], [53, 57, 60], [48, 52, 55], [55, 59, 62]] * 2
bass_match = []
for ch in prog_match: r = ch[0] - 12; bass_match += [r, r, r + 12, r, r, r + 12, r + 7, r + 10]
lead_match = []
for bi, ch in enumerate(prog_match):
    up = [ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[1] + 12] * 4
    lead_match += up if bi % 2 == 1 else [ch[2] + 12, None, None, ch[2] + 12, None, ch[1] + 12, None, ch[0] + 12] * 2
loop('match_loop', 120, prog_match, bass_match, lead_match, 0.14)
print('audio done')
