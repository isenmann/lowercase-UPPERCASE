"""Procedural sound effects for lowercase / UPPERCASE.

Renders every game event in four styles (chip, ink, type, glass) as 44.1 kHz
16-bit mono WAVs, plus a sounds.json manifest for the listening page.
Everything is synthesised here; no samples, no third-party audio.

    python3 make_sfx.py OUT_DIR
"""
import json
import sys
import wave
from pathlib import Path

import numpy as np

SR = 44100
rng = np.random.default_rng(7)


# ---------------------------------------------------------------- primitives
def n_(dur):
    return max(1, int(round(dur * SR)))


def t_(dur):
    return np.arange(n_(dur)) / SR


def mf(m):
    """MIDI note to Hz."""
    return 440.0 * 2 ** ((m - 69) / 12)


def glide(f0, f1, dur, curve=1.0):
    x = np.linspace(0, 1, n_(dur)) ** curve
    return f0 * (f1 / f0) ** x


def cycles(f, n):
    return np.cumsum(np.broadcast_to(np.asarray(f, float), (n,))) / SR


def osc(kind, f, dur, duty=0.5):
    n = n_(dur)
    ph = cycles(f, n) % 1.0
    if kind == "sine":
        return np.sin(2 * np.pi * ph)
    if kind == "square":
        return np.where(ph < duty, 1.0, -1.0)
    if kind == "tri":
        return 4 * np.abs(ph - 0.5) - 1
    if kind == "saw":
        return 2 * ph - 1
    raise ValueError(kind)


def env(dur, attack=0.002, decay=0.1):
    t = t_(dur)
    a = np.clip(t / max(attack, 1e-5), 0, 1)
    d = np.exp(-np.maximum(t - attack, 0) / decay)
    return a * d


def svf(x, fc, q=0.707, mode="lp"):
    """Topology-preserving state variable filter; fc may vary per sample."""
    n = len(x)
    fc = np.clip(np.broadcast_to(np.asarray(fc, float), (n,)), 20, SR * 0.45)
    g = np.tan(np.pi * fc / SR)
    k = 1.0 / q
    a1 = 1 / (1 + g * (g + k))
    a2 = g * a1
    a3 = g * a2
    out = np.empty(n)
    ic1 = ic2 = 0.0
    xs = x.tolist(); A1 = a1.tolist(); A2 = a2.tolist(); A3 = a3.tolist()
    for i in range(n):
        v3 = xs[i] - ic2
        v1 = A1[i] * ic1 + A2[i] * v3
        v2 = ic2 + A2[i] * ic1 + A3[i] * v3
        ic1 = 2 * v1 - ic1
        ic2 = 2 * v2 - ic2
        if mode == "lp":
            out[i] = v2
        elif mode == "bp":
            out[i] = v1
        else:
            out[i] = xs[i] - k * v1 - v2
    return out


def white(dur):
    return rng.uniform(-1, 1, n_(dur))


def chipnoise(dur, rate):
    """Sample-and-hold noise like an 8-bit noise channel; rate may sweep."""
    n = n_(dur)
    idx = np.floor(cycles(rate, n)).astype(int)
    vals = rng.choice([-1.0, 1.0], idx[-1] + 2)
    return vals[idx]


def frames(a, fps=60):
    """Hold each value for one video frame (stair-stepped chip pitch/volume)."""
    a = np.asarray(a, float)
    step = SR // fps
    idx = (np.arange(len(a)) // step) * step
    return a[idx]


class Mix:
    def __init__(self):
        self.parts = []

    def add(self, x, at=0.0, gain=1.0):
        self.parts.append((int(at * SR), np.asarray(x) * gain))
        return self

    def render(self):
        if not self.parts:
            return np.zeros(1)
        n = max(o + len(x) for o, x in self.parts)
        out = np.zeros(n)
        for o, x in self.parts:
            out[o:o + len(x)] += x
        return out


# ---------------------------------------------------------------- style voices
def tone(st, f, dur, vel=1.0, dec=None, duty=None):
    """One pitched note (f may be an array for glides) in a style's voice."""
    n = n_(dur)
    f = np.atleast_1d(np.asarray(f, float))
    f = np.pad(f, (0, max(0, n - len(f))), mode="edge")[:n] if len(f) > 1 else np.full(n, f[0])
    t = t_(dur)
    if st == "chip":
        f = frames(f)
        d = duty if duty is not None else (0.25 if f.mean() > 400 else 0.5)
        x = osc("square", f, dur, d)
        e = frames(np.floor(env(dur, 0.002, dec or dur * 0.6) * 15) / 15)
        return 0.5 * vel * x * e
    if st == "ink":
        x = 0.8 * osc("sine", f, dur) + 0.25 * osc("tri", f, dur)
        x = svf(x * env(dur, 0.006, dec or dur * 0.4), 3500)
        return 0.7 * vel * x
    if st == "type":  # struck wood / marimba bar
        ph = cycles(f, n)
        x = np.sin(2 * np.pi * ph) * np.exp(-t / (dec or max(dur * 0.3, 0.02)))
        x += 0.35 * np.sin(2 * np.pi * 3.93 * ph) * np.exp(-t / 0.02)
        x *= np.clip(t / 0.001, 0, 1)
        click = svf(white(min(dur, 0.01)), 3500, 1.2, "bp") * env(min(dur, 0.01), 0.0003, 0.003)
        x[: len(click)] += 0.5 * click
        return 0.7 * vel * x
    if st == "glass":  # FM bell
        ph = cycles(f, n)
        index = 2.6 * np.exp(-t / 0.12) + 0.3
        x = np.sin(2 * np.pi * ph + index * np.sin(2 * np.pi * 3.5 * ph))
        return 0.5 * vel * x * env(dur, 0.002, dec or dur * 0.5)
    raise ValueError(st)


def noise(st, dur, fc, q=0.8, dec=None, vel=1.0):
    """A noise burst (fc may sweep) in a style's voice."""
    e = env(dur, 0.001, dec or dur * 0.35)
    if st == "chip":
        rate = np.asarray(fc, float) * 2
        return 0.45 * vel * chipnoise(dur, rate) * frames(np.floor(e * 15) / 15)
    if st == "ink":
        return 0.9 * vel * svf(white(dur), fc, 0.6) * e
    if st == "type":
        return 1.4 * vel * svf(white(dur), fc, max(q, 1.2), "bp") * e
    if st == "glass":
        return 1.6 * vel * svf(white(dur), fc, max(q, 3.0), "bp") * e
    raise ValueError(st)


def key(pitch=1.0, vel=1.0):
    """A typewriter key strike: bright clack, body knock, low thunk."""
    m = Mix()
    m.add(svf(white(0.03), 2600 * pitch, 1.3, "bp") * env(0.03, 0.0003, 0.006), gain=1.6)
    m.add(svf(white(0.05), 850 * pitch, 1.0, "bp") * env(0.05, 0.001, 0.015), gain=1.0)
    m.add(osc("sine", glide(170 * pitch, 110 * pitch, 0.05), 0.05) * env(0.05, 0.001, 0.014), gain=0.8)
    return m.render() * vel


def bell(f=2600, dur=1.0, vel=1.0):
    """Typewriter margin bell: inharmonic FM, long ring."""
    t = t_(dur)
    ph = cycles(f, len(t))
    index = 3.0 * np.exp(-t / 0.05) + 0.8
    x = np.sin(2 * np.pi * ph + index * np.sin(2 * np.pi * 1.41 * ph))
    return 0.45 * vel * x * env(dur, 0.0005, dur * 0.3)


def ratchet(count=10, span=0.25, accel=1.6, pitch=1.0, vel=0.6):
    """Carriage-return ratchet: a run of small clicks speeding up."""
    m = Mix()
    times = (np.linspace(0, 1, count) ** accel) * span
    for i, at in enumerate(times):
        m.add(svf(white(0.012), 3200 * pitch * (1 + i * 0.02), 2.0, "bp") * env(0.012, 0.0002, 0.002), at)
    return m.render() * vel


def shatter(count=14, span=0.3, lo=2200, hi=6000, vel=0.5):
    """Glass shards: short high bells scattered in time."""
    m = Mix()
    for _ in range(count):
        at = (rng.random() ** 1.8) * span
        m.add(tone("glass", rng.uniform(lo, hi), 0.12, rng.uniform(0.4, 1.0), dec=0.03), at)
    return m.render() * vel


def seq(st, notes, step, dur, vel=1.0, dec=None, start=0.0, mix=None):
    m = mix or Mix()
    for i, note in enumerate(notes):
        if note is None:
            continue
        m.add(tone(st, mf(note), dur, vel, dec), start + i * step)
    return m


def finish(st, x, peak):
    """Per-style polish, trim, fade, normalise."""
    x = np.asarray(x, float)
    if st == "chip":
        x = np.round(x * 48) / 48  # coarse bit depth
    if st == "glass":  # small room: decaying multi-tap echoes
        x = np.concatenate([x, np.zeros(n_(0.45))])
        wet = np.zeros_like(x)
        for d, g in [(0.023, .5), (0.037, .42), (0.051, .36), (0.073, .3), (0.097, .24), (0.131, .18), (0.173, .12), (0.229, .08)]:
            k = n_(d)
            wet[k:] += g * x[:-k]
        x = x + 0.45 * svf(wet, 5000)
    if st == "ink":
        x = svf(x, 7000)
    x = x - np.mean(x)
    m = np.max(np.abs(x)) or 1.0
    above = np.nonzero(np.abs(x) > m * 0.002)[0]
    x = x[: above[-1] + 1] if len(above) else x
    fade = min(len(x), n_(0.006))
    x[-fade:] *= np.linspace(1, 0, fade)
    x[: min(len(x), 16)] *= np.linspace(0, 1, min(len(x), 16))
    return x / np.max(np.abs(x)) * peak


# ---------------------------------------------------------------- the events
EVENTS = []


def event(group, eid, title, when, hook, peak):
    def wrap(fn):
        EVENTS.append(dict(group=group, id=eid, title=title, when=when, hook=hook, peak=peak, fn=fn))
        return fn
    return wrap


S = ("chip", "ink", "type", "glass")

# ---- menu
@event("Menu", "menu_move", "Move selection", "Arrow keys or d-pad move between menu or pause options.", "MainMenu, PauseMenu.Select", 0.3)
def _(st):
    if st == "type":
        return key(1.3, 0.6)
    f = {"chip": 1046, "ink": 1318, "glass": 2093}[st]
    return tone(st, f, 0.05 if st != "glass" else 0.15, dec=0.03 if st != "glass" else 0.05)


@event("Menu", "menu_confirm", "Confirm", "Enter, Space or A on a menu button.", "MainMenu, PauseMenu.Choose", 0.4)
def _(st):
    if st == "type":
        return Mix().add(key(1.0)).add(tone("type", mf(79), 0.25, 0.7), 0.03).render()
    notes = {"chip": [76, 83], "ink": [72, 79], "glass": [84, 91]}[st]
    return seq(st, notes, 0.055, 0.12 if st != "glass" else 0.35).render()


@event("Menu", "menu_back", "Back", "Esc or B leaves a submenu such as Chapters.", "MainMenu.ShowMain", 0.35)
def _(st):
    if st == "type":
        return Mix().add(key(0.8)).add(tone("type", mf(67), 0.2, 0.6), 0.03).render()
    notes = {"chip": [83, 76], "ink": [79, 72], "glass": [91, 84]}[st]
    return seq(st, notes, 0.055, 0.12 if st != "glass" else 0.3).render()


@event("Menu", "menu_start", "Start game", "Play is chosen and the screen fades to the first level.", "MainMenu.Load", 0.6)
def _(st):
    if st == "chip":
        return seq(st, [60, 64, 67, 72, 76, 79, 84], 0.04, 0.09).add(tone(st, mf(84), 0.35, 0.8), 0.28).render()
    if st == "ink":
        m = Mix()
        for i, n in enumerate([60, 64, 67, 72]):
            m.add(tone(st, mf(n), 0.9, 0.7, dec=0.5), i * 0.06)
        return m.add(tone(st, glide(mf(72), mf(84), 0.25), 0.25, 0.5), 0.0).render()
    if st == "type":
        return Mix().add(key(1.0)).add(ratchet(14, 0.35), 0.06).add(bell(2600, 1.1), 0.45).render()
    return seq(st, [72, 76, 79, 84, 88], 0.06, 0.6).render()


@event("Menu", "pause_open", "Pause", "Esc during play opens the pause popup.", "PauseMenu.Open", 0.4)
def _(st):
    if st == "type":
        return Mix().add(key(0.75)).add(tone("type", mf(55), 0.25), 0.02).render()
    if st == "chip":
        return Mix().add(tone(st, glide(880, 440, 0.07), 0.07)).add(tone(st, 440, 0.12), 0.07).render()
    notes = {"ink": [79, 72], "glass": [84, 79]}[st]
    return seq(st, notes, 0.07, 0.3).render()


@event("Menu", "pause_close", "Resume", "Resume closes the pause popup and play continues.", "PauseMenu.Close", 0.4)
def _(st):
    if st == "type":
        return Mix().add(tone("type", mf(55), 0.2)).add(key(1.0), 0.05).render()
    if st == "chip":
        return Mix().add(tone(st, glide(440, 880, 0.07), 0.07)).add(tone(st, 880, 0.1), 0.07).render()
    notes = {"ink": [72, 79], "glass": [79, 84]}[st]
    return seq(st, notes, 0.07, 0.3).render()


@event("Menu", "type_letter", "Type a letter", "Each character typed into the high score name row.", "GameOverScreen.Type", 0.3)
def _(st):
    if st == "type":
        return key(1.1)
    if st == "chip":
        return tone(st, 1568, 0.035, duty=0.125)
    return tone(st, {"ink": 1568, "glass": 2637}[st], 0.08, dec=0.025)


@event("Menu", "name_saved", "Name saved", "Enter saves the name into the high score table.", "GameOverScreen.ConfirmName", 0.5)
def _(st):
    if st == "type":
        return Mix().add(key(0.9)).add(bell(2600, 0.9), 0.04).render()
    if st == "chip":
        return seq(st, [72, 76, 79], 0.05, 0.08).add(tone(st, mf(84), 0.3), 0.15).render()
    return seq(st, [72, 76, 79, 84], 0.05, 0.7 if st == "glass" else 0.5).render()


# ---- movement
@event("Movement", "jump", "Jump", "The letter leaves the ground.", "LetterController.Jump", 0.45)
def _(st):
    if st == "chip":
        return tone(st, glide(300, 860, 0.13, 0.7), 0.13, dec=0.12)
    if st == "ink":
        return tone(st, glide(260, 600, 0.16, 0.6), 0.16, dec=0.1)
    if st == "type":
        return Mix().add(noise(st, 0.12, glide(700, 2800, 0.12), 2.0, 0.05)).add(tone("ink", glide(300, 620, 0.1), 0.1, 0.3)).render()
    return tone(st, glide(520, 1250, 0.16, 0.6), 0.2, dec=0.08)


@event("Movement", "land", "Land", "The letter touches down after a jump or fall.", "LetterController.UpdateGround", 0.4)
def _(st):
    if st == "chip":
        return Mix().add(noise(st, 0.05, 1500, dec=0.015)).add(tone(st, glide(110, 70, 0.06), 0.06), 0.0, 0.8).render()
    if st == "ink":
        return Mix().add(tone(st, glide(140, 55, 0.12), 0.12, dec=0.04)).add(noise(st, 0.06, 600, dec=0.015), 0, 0.5).render()
    if st == "type":
        return Mix().add(svf(white(0.06), 420, 0.8) * env(0.06, 0.001, 0.015), 0, 1.5).add(osc("sine", glide(110, 70, 0.07), 0.07) * env(0.07, 0.001, 0.02)).render()
    return Mix().add(tone(st, 196, 0.25, 0.6, dec=0.05)).add(noise("ink", 0.05, 700, dec=0.012), 0, 0.6).render()


@event("Movement", "to_upper", "Grow to UPPERCASE", "Shift is pressed: the letter becomes a capital.", "LetterController.EnterForm", 0.5)
def _(st):
    if st == "chip":
        f = glide(220, 660, 0.18) * np.where((np.arange(n_(0.18)) // (SR // 30)) % 2 == 0, 1.0, 1.5)
        return tone(st, f, 0.18, dec=0.2)
    if st == "ink":
        return Mix().add(tone(st, glide(200, 480, 0.2), 0.24, dec=0.14)).add(tone(st, glide(300, 720, 0.2), 0.24, 0.5, dec=0.14)).render()
    if st == "type":  # heavy shift key
        return Mix().add(key(0.55, 1.2)).add(ratchet(4, 0.05, 1.0, 0.6, 0.5), 0.02).add(osc("sine", glide(70, 110, 0.12), 0.12) * env(0.12, 0.003, 0.05), 0.0, 0.7).render()
    return seq(st, [55, 62, 67], 0.035, 0.35).render()


@event("Movement", "to_lower", "Shrink to lowercase", "Shift is released: the letter goes back to small.", "LetterController.EnterForm", 0.45)
def _(st):
    if st == "chip":
        f = glide(660, 260, 0.15) * np.where((np.arange(n_(0.15)) // (SR // 30)) % 2 == 0, 1.0, 1.5)
        return tone(st, f, 0.15, dec=0.15)
    if st == "ink":
        return Mix().add(tone(st, glide(560, 260, 0.16), 0.2, dec=0.1)).add(tone(st, glide(840, 390, 0.16), 0.2, 0.4, dec=0.1)).render()
    if st == "type":
        return Mix().add(osc("sine", glide(110, 160, 0.06), 0.06) * env(0.06, 0.002, 0.02), 0, 0.5).add(key(0.7, 0.9), 0.02).render()
    return seq(st, [79, 74, 67], 0.035, 0.3).render()


@event("Movement", "bump_block", "Bump a word", "The letter hits a word block from below.", "WordBlock.HitFromBelow", 0.55)
def _(st):
    if st == "chip":
        return Mix().add(tone(st, 196, 0.06, duty=0.5)).add(tone(st, 147, 0.1, duty=0.5), 0.045).add(noise(st, 0.03, 2500), 0, 0.4).render()
    if st == "ink":
        return Mix().add(tone(st, glide(190, 120, 0.12), 0.12, dec=0.05)).add(noise(st, 0.03, 2000, dec=0.006), 0, 0.5).render()
    if st == "type":
        return Mix().add(tone(st, 300, 0.15, dec=0.03)).add(key(0.9, 0.8)).render()
    return Mix().add(tone(st, 392, 0.3, dec=0.06)).add(noise("ink", 0.05, 500, dec=0.01), 0, 0.6).render()


@event("Movement", "bounce", "Spring off a word", "A springy word throws the letter up.", "WordBlock.Bounce, LetterController.Spring", 0.5)
def _(st):
    d = 0.32
    t = t_(d)
    wob = 1 + 0.12 * np.sin(2 * np.pi * 22 * t) * np.exp(-t / 0.15)
    if st == "type":  # jaw harp boing
        f = 110 * wob * glide(1, 1.6, d)
        x = osc("saw", f, d)
        fc = 600 + 2200 * (0.5 + 0.5 * np.sin(2 * np.pi * 9 * t)) * np.exp(-t / 0.2)
        return Mix().add(svf(x, fc, 4.0) * env(d, 0.002, 0.12)).add(key(1.0, 0.5)).render()
    base = {"chip": glide(150, 700, d, 0.5), "ink": glide(130, 520, d, 0.5), "glass": glide(260, 900, d, 0.5)}[st]
    return tone(st, base * wob, d, dec=0.16)


@event("Movement", "smash", "Smash a word", "The UPPERCASE letter breaks through a brittle word.", "WordBlock.Smash", 0.75)
def _(st):
    if st == "chip":
        return Mix().add(noise(st, 0.4, glide(5000, 600, 0.4), dec=0.14)).add(tone(st, glide(130, 40, 0.22), 0.22, dec=0.1), 0, 0.8).render()
    if st == "ink":
        return Mix().add(noise(st, 0.45, glide(3500, 300, 0.45), dec=0.12)).add(tone(st, glide(95, 40, 0.3), 0.3, dec=0.1), 0, 0.9).render()
    if st == "type":
        m = Mix().add(osc("sine", glide(90, 45, 0.15), 0.15) * env(0.15, 0.001, 0.05), 0, 1.0)
        for i in range(7):
            m.add(key(rng.uniform(0.7, 1.4), rng.uniform(0.4, 1.0)), (rng.random() ** 1.5) * 0.18)
        return m.render()
    return Mix().add(noise(st, 0.12, 4500, 2.0, dec=0.03), 0, 0.8).add(shatter(18, 0.32)).add(tone("ink", glide(120, 50, 0.2), 0.2, dec=0.06), 0, 0.8).render()


@event("Movement", "wall_cling", "Stick to a wall", "The letter grabs a sticky wall.", "LetterController.Cling", 0.35)
def _(st):
    if st == "chip":
        return Mix().add(tone(st, glide(320, 200, 0.06), 0.06, duty=0.125)).add(noise(st, 0.04, 900), 0, 0.5).render()
    if st == "ink":
        return Mix().add(noise(st, 0.12, glide(1400, 250, 0.12), dec=0.04)).add(tone(st, glide(240, 180, 0.08), 0.08, 0.4), 0).render()
    if st == "type":
        return Mix().add(noise(st, 0.09, glide(1500, 500, 0.09), 1.5, 0.03)).add(key(0.8, 0.4)).render()
    return Mix().add(tone(st, 880, 0.12, dec=0.025)).add(noise("ink", 0.1, glide(1200, 300, 0.1), dec=0.03), 0, 0.6).render()


@event("Movement", "hook_grab", "Grab a hook", "The letter hangs from a hook or comma.", "LetterController.TryGrab", 0.4)
def _(st):
    if st == "chip":
        return seq(st, [88, 93], 0.03, 0.05).render()
    if st == "ink":
        return Mix().add(tone(st, 1046, 0.08, dec=0.03)).add(noise(st, 0.02, 4000, dec=0.004), 0, 0.4).render()
    if st == "type":
        return Mix().add(svf(white(0.06), 5200, 6.0, "bp") * env(0.06, 0.0003, 0.02), 0, 3.0).add(key(1.2, 0.6)).render()
    return bell(2349, 0.4, 1.0)


@event("Movement", "hook_release", "Let go of a hook", "The letter jumps off a hook.", "LetterController.ReleaseHook", 0.4)
def _(st):
    if st == "chip":
        return tone(st, glide(600, 1250, 0.08), 0.08, dec=0.08)
    if st == "ink":
        return tone(st, glide(480, 900, 0.1), 0.1, dec=0.06)
    if st == "type":
        return noise(st, 0.1, glide(1500, 4200, 0.1), 2.0, 0.04)
    return tone(st, glide(900, 1800, 0.12), 0.15, dec=0.06)


@event("Movement", "door_open", "Door opens", "A word door swings open for 2.5 s.", "WordBlock.SetDoorOpen", 0.45)
def _(st):
    if st == "chip":
        return Mix().add(tone(st, glide(392, 523, 0.1), 0.1)).add(tone(st, 784, 0.18), 0.1).render()
    if st == "ink":
        m = Mix().add(tone(st, glide(300, 600, 0.25), 0.3, dec=0.15))
        return m.add(tone(st, mf(79), 0.5, 0.5), 0.15).render()
    if st == "type":
        return Mix().add(key(0.9)).add(svf(white(0.05), 4800, 5.0, "bp") * env(0.05, 0.0003, 0.012), 0.05, 2.0).add(tone(st, mf(60), 0.2), 0.08).add(tone(st, mf(67), 0.3), 0.18).render()
    return seq(st, [72, 79], 0.1, 0.5).render()


# ---- shooting and enemies
@event("Shooting and enemies", "power_block", "Power block", "The letter takes a power block and can now shoot asterisks.", "LetterShooter.Arm", 0.55)
def _(st):
    if st == "chip":
        return seq(st, [60, 64, 67, 72, 64, 67, 72, 76, 67, 72, 76, 79, 84], 0.032, 0.06).render()
    if st == "ink":
        m = Mix().add(tone(st, glide(300, 1200, 0.35, 0.7), 0.4, dec=0.25))
        return seq(st, [84, 88, 91], 0.05, 0.4, 0.4, start=0.25, mix=m).render()
    if st == "type":
        return Mix().add(key(1.0)).add(ratchet(9, 0.2, 1.0, 1.2), 0.04).add(bell(3100, 0.8), 0.26).render()
    return seq(st, [72, 79, 84, 88, 91, 96], 0.04, 0.45).render()


@event("Shooting and enemies", "shoot", "Shoot an asterisk", "The armed letter fires an asterisk.", "LetterShooter.Fire", 0.5)
def _(st):
    if st == "chip":
        return tone(st, glide(1600, 240, 0.12, 0.6), 0.12, dec=0.1, duty=0.25)
    if st == "ink":
        return tone(st, glide(1400, 420, 0.1, 0.6), 0.11, dec=0.06)
    if st == "type":
        return Mix().add(key(1.5, 1.1)).add(osc("sine", glide(900, 300, 0.05), 0.05) * env(0.05, 0.001, 0.02), 0.005, 0.4).render()
    return tone(st, glide(2400, 600, 0.12, 0.5), 0.16, dec=0.05)


@event("Shooting and enemies", "asterisk_pop", "Asterisk pops", "An asterisk hits a wall or flies out of range.", "Asterisk.Pop", 0.3)
def _(st):
    if st == "chip":
        return Mix().add(noise(st, 0.04, 6000, dec=0.012)).add(tone(st, 880, 0.03), 0, 0.6).render()
    if st == "ink":
        return tone(st, glide(650, 200, 0.05), 0.05, dec=0.02)
    if st == "type":
        return key(1.6, 0.6)
    return tone(st, 3136, 0.1, dec=0.025)


@event("Shooting and enemies", "enemy_shot", "Enemy shot", "An asterisk hits an enemy and it falls apart.", "Enemy.Kill", 0.6)
def _(st):
    if st == "chip":
        return Mix().add(noise(st, 0.22, glide(3000, 500, 0.22), dec=0.08)).add(tone(st, glide(440, 110, 0.2), 0.2, dec=0.12), 0, 0.8).render()
    if st == "ink":
        return Mix().add(noise(st, 0.2, glide(2500, 400, 0.2), dec=0.06), 0, 0.8).add(tone(st, glide(520, 150, 0.25), 0.25, dec=0.1)).render()
    if st == "type":
        m = Mix().add(osc("sine", glide(140, 60, 0.1), 0.1) * env(0.1, 0.001, 0.03), 0.06, 0.8)
        for i, p in enumerate([1.3, 1.0, 0.75]):
            m.add(key(p, 1.0 - i * 0.15), i * 0.035)
        return m.render()
    return Mix().add(shatter(10, 0.18, 1800, 4500)).add(tone(st, glide(1200, 300, 0.2), 0.25, 0.7, dec=0.08)).render()


@event("Shooting and enemies", "enemy_stomp", "Stomp an enemy", "The letter lands on an enemy and bounces off.", "LetterController.BounceOffEnemy", 0.6)
def _(st):
    if st == "chip":
        return Mix().add(noise(st, 0.04, 1200)).add(tone(st, glide(400, 820, 0.09), 0.09), 0.03).render()
    if st == "ink":
        return Mix().add(tone(st, glide(300, 120, 0.07), 0.07)).add(tone(st, glide(300, 640, 0.1), 0.1, 0.8), 0.06).render()
    if st == "type":
        return Mix().add(osc("sine", glide(120, 60, 0.07), 0.07) * env(0.07, 0.001, 0.02), 0, 1.0).add(key(0.9)).add(tone(st, mf(79), 0.15, 0.6), 0.05).render()
    return Mix().add(tone(st, 262, 0.2, dec=0.05)).add(tone(st, glide(600, 1400, 0.1), 0.2, 0.7, dec=0.06), 0.05).render()


# ---- collecting and progress
@event("Collecting and progress", "collect_hash", "Collect #", "The letter picks up a # (50 points).", "Collectible.Pop, LevelRun.Collect", 0.45)
def _(st):
    if st == "chip":
        return Mix().add(tone(st, mf(83), 0.06, dec=1)).add(tone(st, mf(88), 0.3, dec=0.15), 0.06).render()
    if st == "type":
        return Mix().add(tone(st, mf(84), 0.15)).add(tone(st, mf(88), 0.3), 0.06).add(bell(3500, 0.4, 0.3), 0.06).render()
    notes = {"ink": [84, 91], "glass": [88, 95]}[st]
    return seq(st, notes, 0.06, 0.4).render()


@event("Collecting and progress", "checkpoint", "Checkpoint", "The letter reaches a ; checkpoint.", "Checkpoint.MarkReached", 0.5)
def _(st):
    if st == "chip":
        return seq(st, [72, 76, 79], 0.07, 0.1).add(tone(st, mf(84), 0.4, dec=0.25), 0.21).render()
    if st == "ink":
        return seq(st, [67, 72, 76, 79], 0.07, 0.7, 0.8).render()
    if st == "type":
        return Mix().add(key(1.0)).add(tone(st, mf(76), 0.2), 0.03).add(bell(2600, 1.0), 0.1).render()
    return seq(st, [79, 84, 88, 91], 0.07, 0.8).render()


@event("Collecting and progress", "caps_unlock", "Caps Lock unlocked", "The Caps Lock key is pressed and its zone opens.", "CapsLockKey, CapsLockZone.Unlock", 0.6)
def _(st):
    if st == "chip":
        return Mix().add(noise(st, 0.05, 800)).add(seq(st, [48, 55, 60, 67, 72], 0.05, 0.1).add(tone(st, mf(72), 0.3), 0.2).render(), 0.04).render()
    if st == "ink":
        return Mix().add(tone(st, glide(120, 60, 0.1), 0.1)).add(seq(st, [60, 67, 72], 0.08, 0.8, 0.8).render(), 0.06).render()
    if st == "type":
        return Mix().add(key(0.45, 1.3)).add(svf(white(0.04), 4500, 5.0, "bp") * env(0.04, 0.0003, 0.01), 0.07, 2.2).add(tone(st, mf(60), 0.3), 0.1).add(tone(st, mf(67), 0.4), 0.2).render()
    m = Mix()
    for n in [60, 67, 72, 76]:
        m.add(tone(st, mf(n), 1.2, 0.6, dec=0.5), 0.0)
    return Mix().add(tone("ink", glide(100, 60, 0.08), 0.08)).add(m.render(), 0.03).render()


@event("Collecting and progress", "level_complete", "Level complete", "The letter slots into the blank and the sentence is whole.", "PrototypeFlow.OnSlotted", 0.65)
def _(st):
    if st == "chip":
        m = seq(st, [67, 72, 76, 79, None, 76, 79], 0.09, 0.1)
        return m.add(tone(st, mf(84), 0.6, dec=0.4), 0.63).add(tone(st, mf(72), 0.6, 0.4, dec=0.4, duty=0.5), 0.63).render()
    if st == "ink":
        m = Mix()
        for i, chord in enumerate([[60, 64, 67], [65, 69, 72], [67, 71, 74], [72, 76, 79, 84]]):
            for j, n in enumerate(chord):
                m.add(tone(st, mf(n), 1.2 if i == 3 else 0.5, 0.5, dec=0.5 if i == 3 else 0.2), i * 0.16 + j * 0.02)
        return m.render()
    if st == "type":
        m = Mix().add(ratchet(16, 0.4, 1.4))
        m = seq(st, [72, 76, 79, 84], 0.09, 0.3, start=0.4, mix=m)
        return m.add(bell(2600, 1.2), 0.75).render()
    return seq(st, [72, 76, 79, 84, 88, 91, 96], 0.07, 1.0, 0.8).render()


@event("Collecting and progress", "timer_tick", "Clock tick", "Once a second during the last 10 seconds of the 60 s clock.", "PrototypeFlow (new: low time warning)", 0.3)
def _(st):
    if st == "chip":
        return tone(st, 1760, 0.03, duty=0.5)
    if st == "ink":
        return tone(st, 1046, 0.06, dec=0.02)
    if st == "type":
        return svf(white(0.03), 3000, 8.0, "bp") * env(0.03, 0.0003, 0.006)
    return tone(st, 2637, 0.1, dec=0.02)


@event("Collecting and progress", "time_up", "Time up", "The 60 s clock runs out.", "PrototypeFlow (LevelRun.TimeIsUp)", 0.6)
def _(st):
    if st == "chip":
        return seq(st, [81, 76, 81, 76, 81, 76], 0.1, 0.09).add(tone(st, glide(mf(76), mf(52), 0.4), 0.4), 0.6).render()
    if st == "ink":
        return seq(st, [76, 72, 69, 64], 0.15, 0.6, 0.8).render()
    if st == "type":
        m = Mix()
        for i in range(10):
            m.add(bell(2600 if i % 2 == 0 else 2550, 0.25, 0.8), i * 0.06)
        return m.add(bell(2600, 1.0, 0.7), 0.6).render()
    return seq(st, [88, 84, 81, 76, 72], 0.12, 0.8).render()


# ---- dying
@event("Dying", "death_enemy", "Hit by an enemy", "The letter touches an enemy from the side.", "LetterController.TouchEnemy", 0.6)
def _(st):
    d = 0.6
    t = t_(d)
    vib = 1 + 0.06 * np.sin(2 * np.pi * 12 * t)
    if st == "chip":
        return Mix().add(tone(st, 880, 0.06)).add(tone(st, glide(880, 110, d) * vib, d, dec=0.4), 0.08).render()
    if st == "ink":
        return tone(st, glide(620, 150, d) * vib, d, dec=0.3)
    if st == "type":
        m = Mix().add(key(1.2)).add(key(0.7), 0.04)
        return seq(st, [67, 63, 60], 0.12, 0.3, start=0.12, mix=m).render()
    return Mix().add(tone(st, glide(1200, 200, d) * vib, d, dec=0.3)).add(tone(st, glide(1270, 212, d), d, 0.6, dec=0.3)).render()


@event("Dying", "burn", "Burn", "The letter touches a flame.", "LetterController.Burn", 0.6)
def _(st):
    d = 0.6
    crackle = Mix()
    for _ in range(26):
        crackle.add(svf(white(0.008), rng.uniform(1500, 5000), 2.0, "bp") * env(0.008, 0.0002, 0.0015), rng.random() * d, rng.uniform(0.4, 1.2))
    crackle = crackle.render()
    if st == "chip":
        return Mix().add(noise(st, d, glide(2000, 9000, d), dec=0.25)).add(tone(st, glide(400, 90, d), d, 0.7, dec=0.3)).render()
    if st == "ink":
        return Mix().add(noise(st, d, glide(4000, 1200, d), dec=0.25), 0, 0.6).add(crackle, 0, 1.2).add(tone(st, glide(500, 140, 0.4), 0.4, 0.6, dec=0.2)).render()
    if st == "type":
        return Mix().add(crackle, 0, 2.0).add(noise(st, d, 3000, 1.0, 0.2), 0, 0.5).add(key(1.0, 0.8)).render()
    return Mix().add(noise(st, d, glide(6000, 2000, d), 2.0, 0.25), 0, 0.7).add(tone(st, glide(900, 200, d), d, 0.7, dec=0.25)).add(crackle, 0, 0.8).render()


@event("Dying", "fall_off", "Fall off the screen", "The letter drops out of the bottom of the level.", "LetterController.FallOffScreen", 0.55)
def _(st):
    d = 1.0
    if st == "type":  # slide whistle, then a distant thunk
        return Mix().add(tone("ink", glide(1200, 140, d, 0.8), d, dec=0.6)).add(osc("sine", glide(90, 50, 0.15), 0.15) * env(0.15, 0.002, 0.05), d - 0.05, 0.5).render()
    f = {"chip": glide(900, 80, d, 0.8), "ink": glide(800, 90, d, 0.8), "glass": glide(1600, 160, d, 0.8)}[st]
    return tone(st, f, d, dec=0.6)


@event("Dying", "respawn", "Respawn", "The letter reappears at the start or at its last checkpoint.", "LetterController.Respawn", 0.45)
def _(st):
    if st == "chip":
        return seq(st, [72, 76, 79, 84, 88], 0.035, 0.07).render()
    if st == "ink":
        x = tone(st, glide(400, 800, 0.35), 0.35, dec=1.0)
        return x * np.linspace(0, 1, len(x)) ** 1.5
    if st == "type":
        return Mix().add(ratchet(6, 0.12, 1.0, 1.1)).add(key(1.0), 0.14).render()
    m = Mix()
    for i, n in enumerate([84, 88, 91, 96, 100]):
        m.add(tone(st, mf(n), 0.3, 0.3 + i * 0.15, dec=0.08), i * 0.04)
    return m.render()


@event("Dying", "life_lost", "Life lost", "One of the three a glyphs in the top bar dims.", "PrototypeFlow.LoseLife", 0.5)
def _(st):
    if st == "chip":
        return seq(st, [72, 67, 60], 0.08, 0.12).render()
    if st == "ink":
        return seq(st, [72, 67], 0.12, 0.6, 0.8).render()
    if st == "type":
        return Mix().add(key(0.6)).add(tone(st, mf(60), 0.3), 0.02).add(tone(st, mf(55), 0.4), 0.14).render()
    return seq(st, [84, 79, 72], 0.1, 0.6).render()


@event("Dying", "game_over", "Game over", "The last life is lost or the clock ran out; the GAME OVER screen shows.", "PrototypeFlow.ShowGameOver", 0.6)
def _(st):
    if st == "chip":
        m = seq(st, [67, 66, 65, 64], 0.22, 0.2)
        t = t_(0.9)
        return m.add(tone(st, mf(60) * (1 + 0.02 * np.sin(2 * np.pi * 6 * t)), 0.9, dec=0.5), 0.88).render()
    if st == "ink":
        return seq(st, [72, 67, 63, 60], 0.28, 1.2, 0.8, dec=0.45).render()
    if st == "type":
        m = seq(st, [67, 63, 60, 55], 0.3, 0.4)
        return m.add(bell(1300, 1.4, 0.6), 0.9).render()
    return seq(st, [79, 75, 72, 67], 0.3, 1.4, dec=0.6).render()


# ---- end of run
@event("End of the run", "game_finished", "Game finished", "Final is solved and the score screen shows.", "LevelTransition.endScreen", 0.65)
def _(st):
    melody = [60, 64, 67, 72, None, 69, 72, 77, None, 71, 74, 79, 84]
    if st == "chip":
        m = seq(st, melody, 0.11, 0.12)
        return m.add(tone(st, mf(84), 1.0, dec=0.6), 1.43).add(tone(st, mf(76), 1.0, 0.5, dec=0.6, duty=0.5), 1.43).render()
    if st == "ink":
        m = seq(st, melody, 0.12, 0.5, 0.6)
        for n in [72, 76, 79, 84]:
            m.add(tone(st, mf(n), 1.6, 0.5, dec=0.8), 1.56)
        return m.render()
    if st == "type":
        m = Mix().add(ratchet(18, 0.5, 1.4))
        m = seq(st, melody, 0.11, 0.3, start=0.5, mix=m)
        return m.add(bell(2600, 1.2), 1.95).add(bell(2600, 1.4), 2.15).render()
    return seq(st, melody + [88, 91, 96], 0.1, 1.2, 0.8).render()


@event("End of the run", "high_score", "New high score", "The run's score makes the top 10 and the name row opens.", "GameOverScreen.Show", 0.55)
def _(st):
    if st == "chip":
        return seq(st, [84, 88, 91, 96, 91, 96, 100], 0.045, 0.08).add(tone(st, mf(96), 0.4, dec=0.25), 0.32).render()
    if st == "type":
        m = Mix()
        for i in range(3):
            m.add(bell(2600 * (1 + i * 0.12), 0.6, 0.8), i * 0.12)
        return m.render()
    m = Mix()
    for i, n in enumerate([84, 91, 88, 96, 91, 100]):
        m.add(tone(st, mf(n), 0.6, 0.7, dec=0.25), i * 0.05)
    return m.render()


# Added after the first sets were picked; kept last so the noise of the events above stays the same.
@event("Shooting and enemies", "asterisk_blocked", "Asterisk blocked", "An asterisk bounces off an enemy it can't hurt.", "Asterisk.Fly", 0.45)
def _(st):
    if st == "chip":
        return Mix().add(tone(st, 196, 0.08, dec=0.05, duty=0.5)).add(tone(st, 1568, 0.03, 0.7, duty=0.125)).render()
    if st == "ink":
        return Mix().add(tone(st, glide(330, 280, 0.08), 0.08, dec=0.04)).add(tone(st, 1320, 0.04, 0.5, dec=0.015)).render()
    if st == "type":
        m = Mix().add(key(0.7, 0.9))
        ping = osc("sine", 1870, 0.12) + 0.5 * osc("sine", 1870 * 2.76, 0.12)
        return m.add(ping * env(0.12, 0.0005, 0.03), 0.004, 0.35).render()
    return Mix().add(tone(st, 1046, 0.15, dec=0.04)).add(tone(st, 1108, 0.15, 0.8, dec=0.04)).render()


@event("Shooting and enemies", "arrow_fire", "Arrow fired", "A launcher word fires an arrow.", "EnemyLauncher.Fire", 0.35)
def _(st):
    if st == "chip":
        return Mix().add(noise(st, 0.06, glide(1500, 4000, 0.06), dec=0.03)).add(tone(st, glide(300, 900, 0.07), 0.07, 0.7, duty=0.5)).render()
    if st == "ink":
        return Mix().add(noise(st, 0.08, glide(900, 3000, 0.08), dec=0.03), 0, 0.7).add(tone(st, glide(260, 700, 0.08), 0.08, dec=0.04)).render()
    if st == "type":  # a short whoosh after a soft key, like a tab stop
        m = Mix().add(key(1.15, 0.7))
        m.add(svf(white(0.09), glide(1200, 3500, 0.09), 1.5, "bp") * env(0.09, 0.01, 0.03), 0.01, 0.9)
        return m.render()
    return Mix().add(tone(st, glide(1500, 2600, 0.08), 0.12, dec=0.04)).render()


@event("Shooting and enemies", "riser_up", "Riser rises", "A riser comes up out of a hole between loose letters.", "EnemyRise", 0.35)
def _(st):
    if st == "chip":
        return tone(st, glide(180, 520, 0.12, 0.7), 0.13, dec=0.08, duty=0.5)
    if st == "ink":
        return tone(st, glide(200, 480, 0.14, 0.7), 0.15, dec=0.07)
    if st == "type":  # a key pushed up from below: low knock rising into a click
        m = Mix().add(osc("sine", glide(90, 220, 0.1), 0.1) * env(0.1, 0.004, 0.04), 0, 0.9)
        return m.add(key(1.05, 0.6), 0.07).render()
    return tone(st, glide(700, 1400, 0.14), 0.18, dec=0.06)


@event("Shooting and enemies", "drip_fall", "Drip falls", "A drip lets go of its ceiling.", "EnemyDrip", 0.35)
def _(st):
    if st == "chip":
        return tone(st, glide(1800, 500, 0.12, 0.5), 0.12, dec=0.08, duty=0.25)
    if st == "ink":
        return tone(st, glide(1500, 600, 0.1, 0.5), 0.12, dec=0.05)
    if st == "type":  # an ink drop: a quick falling plink
        ph = osc("sine", glide(1600, 700, 0.09, 0.4), 0.09)
        return Mix().add(ph * env(0.09, 0.001, 0.03), 0, 0.8).add(key(1.4, 0.3)).render()
    return tone(st, glide(2600, 1200, 0.1), 0.16, dec=0.05)


STYLES = [
    dict(id="chip", name="Chip", blurb="Square waves and stepped noise, stair-stepped at 60 fps like an 8-bit console."),
    dict(id="ink", name="Ink", blurb="Soft sine and triangle tones, smooth glides, rounded edges. Calm and modern."),
    dict(id="type", name="Type", blurb="Typewriter keys, carriage return, margin bell and wooden notes. The letters' own world."),
    dict(id="glass", name="Glass", blurb="Bright FM bells with a small room echo. Airy and sparkly."),
]


def write_wav(path, x):
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def main(out):
    out = Path(out)
    (out / "sfx").mkdir(parents=True, exist_ok=True)
    manifest = dict(styles=STYLES, events=[])
    for ev in EVENTS:
        lengths, peaks = {}, {}
        for st in S:
            x = finish(st, ev["fn"](st), ev["peak"])
            assert np.all(np.isfinite(x)), (ev["id"], st)
            write_wav(out / "sfx" / f"{ev['id']}_{st}.wav", x)
            lengths[st] = round(len(x) / SR, 3)
            bins = np.array_split(np.abs(x), 40)
            peaks[st] = [round(float(b.max()) if len(b) else 0.0, 2) for b in bins]
        manifest["events"].append({k: ev[k] for k in ("group", "id", "title", "when", "hook")} | {"lengths": lengths, "peaks": peaks})
        print(ev["id"], lengths)
    (out / "sounds.json").write_text(json.dumps(manifest, separators=(",", ":")))


if __name__ == "__main__":
    main(sys.argv[1])
