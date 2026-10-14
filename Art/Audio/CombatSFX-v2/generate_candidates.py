"""64 deliberate audio directions: CC0 material edits + original synthesis."""
from pathlib import Path
import csv, hashlib, io, json, math, wave, zipfile
import numpy as np
from scipy import signal
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parent
SR = 48000
RNG = np.random.default_rng(20261003)
CACHE = {}
USED = set()
LAYERS = []

def time(seconds):
    return np.arange(round(seconds * SR)) / SR

def wav_read(p):
    sr, raw = wavfile.read(p)
    if raw.dtype != np.int16:
        raise ValueError(f"Unexpected source format: {p}: {raw.dtype}")
    y = raw.astype(np.float64) / 32768
    if y.ndim > 1:
        y = y.mean(axis=1)
    if sr != SR:
        common = math.gcd(sr, SR)
        y = signal.resample_poly(y, SR // common, sr // common)
    return y

def band(y, lo=0, hi=20000):
    if lo > 0:
        y = signal.sosfilt(signal.butter(2, lo, "highpass", fs=SR, output="sos"), y)
    if hi < SR / 2:
        y = signal.sosfilt(signal.butter(2, hi, "lowpass", fs=SR, output="sos"), y)
    return y

def material(key, speed=1., cap=.5, lo=35, hi=18000, reverse=False, offset=0):
    pack, name = key.split(":")
    if key not in CACHE:
        if pack == "f":
            path = ROOT / "sources" / "fire-1.wav"
        else:
            folder = "kenney_rpg-audio" if pack == "r" else "kenney_impact-sounds"
            path = ROOT / "sources" / "decoded" / folder / (name + ".wav")
        y = wav_read(path)
        if pack != "f":
            active = np.flatnonzero(np.abs(y) > np.max(np.abs(y)) * .018)
            if len(active):
                y = y[max(0, active[0] - 96):min(len(y), active[-1] + 1000)]
        CACHE[key] = y
    USED.add(key)
    y = CACHE[key].copy()
    if offset:
        y = y[round(offset * SR):]
    if reverse:
        y = y[::-1]
    if speed != 1:
        n = max(1, round(len(y) / speed))
        y = np.interp(np.arange(n) * speed, np.arange(len(y)), y)
    y = band(y, lo, hi)
    y = y[:round(cap * SR)]
    if len(y):
        y /= max(np.max(np.abs(y)), 1e-9)
        fade = min(len(y) // 3, round(.013 * SR))
        y[:min(64, len(y))] *= np.linspace(0, 1, min(64, len(y)))
        y[-fade:] *= np.linspace(1, 0, fade) ** 2
    return y

def env(t, attack=.003, decay=.10):
    return (1 - np.exp(-t / attack)) * np.exp(-t / decay)

def fm(seconds, hz=500, ratio=1.5, index=1.5, decay=.10, drop=.5, attack=.003):
    t = time(seconds)
    freq = hz * (drop + (1 - drop) * np.exp(-t / .055))
    phase = 2 * np.pi * np.cumsum(freq) / SR
    mod = index * np.exp(-t / (decay * .6)) * np.sin(phase * ratio)
    return np.sin(phase + mod) * env(t, attack, decay)

def modes(seconds, frequencies, decay=.12):
    t = time(seconds)
    y = np.zeros(len(t))
    for i, hz in enumerate(frequencies):
        y += np.sin(2 * np.pi * hz * t) * env(t, .0015, decay / (1 + .35 * i)) / (1 + i)
    return y

def whoosh(seconds, bright=2500, peak=.06, width=.05, roughness=0):
    t = time(seconds)
    x = band(RNG.normal(size=len(t)), 160, bright)
    shape = np.exp(-.5 * ((t - peak) / width) ** 2)
    if roughness:
        shape *= .7 + .3 * np.sin(2 * np.pi * roughness * t) ** 2
    return x * shape / max(float(np.std(x)), 1e-6) * .35

def pluck(seconds, hz=200, damping=.992, brightness=.4):
    n = round(seconds * SR)
    period = max(8, round(SR / hz))
    buf = RNG.uniform(-1, 1, period)
    out = np.zeros(n)
    at = 0
    for i in range(n):
        out[i] = buf[at]
        nxt = (at + 1) % period
        buf[at] = damping * ((1 - brightness) * buf[at] + brightness * buf[nxt])
        at = nxt
    out = band(out, 50, 7500)
    out *= np.exp(-time(seconds) / .16)
    return out

def granular(key, seconds, grain=.035, spacing=.025, speed=1, offset=0):
    x = material(key, cap=2., speed=speed, offset=offset)
    y = np.zeros(round(seconds * SR))
    size = round(grain * SR)
    if len(x) < size:
        x = np.pad(x, (0, size - len(x)))
    window = np.hanning(size)
    for k, at in enumerate(np.arange(0, seconds - grain, spacing)):
        start = (k * 2917) % max(1, len(x) - size)
        g = x[start:start + size] * window * (.88 ** k)
        a = round(at * SR)
        y[a:a + size] += g
    return y

def echoes(x, taps=(.041, .078), gains=(.17, .08)):
    y = np.zeros(len(x) + round((max(taps) + .05) * SR))
    y[:len(x)] = x
    for at, gain in zip(taps, gains):
        a = round(at * SR)
        layer = band(x, 100, 6000)
        y[a:a + len(x)] += layer * gain
    return y

class Mix:
    def __init__(self, seconds=.75):
        self.y = np.zeros(round(seconds * SR))

    def add(self, y, gain=1, at=0, role="body", source="original synthesis"):
        a = round(at * SR)
        b = min(len(self.y), a + len(y))
        if b > a:
            self.y[a:b] += y[:b - a] * gain
        LAYERS.append({"role": role, "source": source, "gain": gain, "at_seconds": at})
        return self

    def sample(self, key, gain=1, at=0, role="body", **kw):
        return self.add(material(key, **kw), gain, at, role, key)

def magic(v):
    m = Mix(.72)
    if v == 0:
        m.sample("r:cloth1", .30, cap=.20, speed=1.5, role="motion")
        m.sample("i:impactGlass_light_000", .28, .02, speed=1.35, role="attack")
        m.add(echoes(modes(.27, [1320,1980,2640], .065)), .27, .025, "tail")
    elif v == 1:
        m.sample("r:knifeSlice", .43, cap=.26, speed=1.35, lo=180, role="motion")
        m.add(whoosh(.28, 3600, .06, .035), .52, role="body")
        m.add(fm(.22, 520, .51, .20, .042), .11, .012, "attack")
    elif v == 2:
        m.sample("r:bookOpen", .25, cap=.18, speed=1.4, role="attack")
        m.sample("r:bookFlip2", .32, .055, cap=.24, speed=1.3, reverse=True, role="motion")
        m.add(echoes(granular("i:impactGlass_light_002", .30, .024, .045, 1.6)), .34, .025, "tail")
    elif v == 3:
        m.add(fm(.22, 950, 2.71, 5.5, .028, .12), .44, role="attack")
        m.sample("r:cloth4", .24, .018, speed=1.6, cap=.18, lo=1300, role="motion")
        m.add(modes(.20, [1800,2800], .027), .06, .01, "tail")
    elif v == 4:
        m.sample("i:impactBell_heavy_001", .50, cap=.30, speed=2.3, lo=600, role="attack")
        m.sample("r:cloth2", .24, .018, cap=.18, speed=1.5, role="motion")
        m.add(echoes(modes(.29, [880,1320], .078)), .13, .015, "tail")
    elif v == 5:
        m.sample("r:cloth3", .38, cap=.28, speed=.88, lo=70, hi=2300, role="motion")
        m.add(fm(.39, 260, .77, 2.4, .12, .24, .025), .37, .015, "body")
        m.add(echoes(granular("r:creak1", .30, .055, .040, .9)), .20, .02, "tail")
    elif v == 6:
        m.add(echoes(granular("r:handleSmallLeather2", .33, .022, .022, 1.2)), .65, role="body")
        m.add(fm(.31, 620, .33, 2.7, .070, .55, .02), .16, .015, "tail")
        m.sample("r:clothBelt", .16, cap=.14, speed=1.4, role="attack")
    else:
        m.sample("r:handleCoins2", .24, cap=.17, reverse=True, speed=1.4, role="attack")
        for at, hz in [(.03, 740), (.067, 988), (.11, 1480)]:
            m.add(modes(.14, [hz, hz*1.51], .035), .25, at, "body")
        m.sample("r:knifeSlice2", .27, .035, cap=.20, speed=1.7, role="motion")
    return m.y

def soft_hit(v):
    m = Mix(.55)
    sources = [
        ("i:impactSoft_medium_000", 1.05, 4500),
        ("r:dropLeather", 1.25, 2700),
        ("i:impactPunch_medium_002", 1.35, 4200),
        ("r:chop", 1.05, 3800),
        ("r:clothBelt2", 1.6, 3000),
        ("i:impactSoft_heavy_003", .94, 3000),
        ("i:impactPunch_heavy_001", .8, 2400),
        ("i:impactGeneric_light_003", 1.55, 6000),
    ]
    key, speed, hi = sources[v]
    m.sample(key, .83, cap=.30, speed=speed, hi=hi, role="body")
    if v == 0:
        m.sample("r:cloth2", .19, .008, cap=.10, speed=1.8, role="attack")
    elif v == 1:
        m.sample("r:handleSmallLeather", .33, .025, cap=.15, speed=1.4, role="tail")
    elif v == 2:
        m.add(fm(.14, 185, 1.2, .7, .030, .48), .14, .005, "body")
        m.add(granular("r:cloth1", .18, .018, .030, 1.8), .13, .02, "tail")
    elif v == 3:
        m.sample("i:impactSoft_medium_003", .35, .015, cap=.10, role="attack")
    elif v == 4:
        m.sample("r:knifeSlice", .47, cap=.18, speed=1.8, lo=500, role="motion")
    elif v == 5:
        m.add(echoes(modes(.19, [1010,1515], .035)), .16, .012, "tail")
    elif v == 6:
        m.sample("i:impactSoft_heavy_001", .31, .031, cap=.19, speed=.8, hi=1100, role="body")
    else:
        m.add(pluck(.15, 320, .983, .4), .14, .005, "tail")
    return m.y

def hard_hit(v):
    m = Mix(.66)
    spec = [
        ("i:impactPlate_light_000", 1.05, 8000),
        ("r:metalPot1", 1.12, 5500),
        ("i:impactMining_001", 1.45, 4600),
        ("i:impactPlate_medium_002", 1.7, 7000),
        ("i:impactBell_heavy_002", 1.24, 4400),
        ("i:impactMetal_medium_003", .95, 7200),
        ("i:impactGlass_heavy_001", 1.18, 8400),
        ("i:impactMetal_heavy_004", .69, 2400),
    ]
    key, speed, hi = spec[v]
    m.sample(key, .81, speed=speed, cap=.33, hi=hi, role="body")
    if v in (0,1,5):
        m.sample("r:metalClick", .16, cap=.065, speed=1.4 + .1*v, role="attack")
    elif v == 2:
        m.sample("i:impactWood_medium_001", .25, cap=.16, speed=1.3, hi=1300, role="attack")
    elif v == 3:
        m.add(modes(.19, [1700,2870], .05), .10, .008, "tail")
    elif v == 4:
        m.sample("i:impactPunch_medium_003", .18, speed=1.2, cap=.10, hi=1200, role="attack")
    elif v == 6:
        m.add(granular("i:impactGlass_light_004", .24, .016, .027, 1.25), .15, .025, "tail")
    else:
        m.sample("i:impactPlate_heavy_003", .24, speed=.86, cap=.23, hi=2800, role="tail")
    return m.y

def hurt(v):
    m = Mix(.64)
    keys = [
        "i:impactPunch_medium_000", "i:impactSoft_medium_004",
        "i:impactPunch_medium_004", "i:impactPunch_heavy_003",
        "r:chop", "r:dropLeather", "r:knifeSlice2", "i:impactGeneric_light_001",
    ]
    m.sample(keys[v], .78, cap=.27, speed=[1,.95,1.15,.82,1.3,1.1,1.5,1.1][v],
             hi=[2700,4200,3800,1800,4000,2500,7000,2500][v], role="body")
    if v == 0:
        m.sample("r:cloth3", .24, .012, speed=1.6, cap=.14, role="tail")
    elif v == 1:
        m.add(fm(.18, 320, .5, .7, .040, .15), .17, .008, "feedback")
    elif v == 2:
        m.add(echoes(fm(.21, 730, 1.9, 2.3, .039, .35), (.019,.053), (.13,.06)), .22, .014, "feedback")
    elif v == 3:
        m.sample("r:clothBelt2", .22, .018, speed=1.3, cap=.14, role="tail")
    elif v == 4:
        m.add(modes(.17, [155,112], .03), .16, .012, "feedback")
    elif v == 5:
        m.sample("r:beltHandle2", .32, .03, speed=1.5, cap=.18, role="tail")
    elif v == 6:
        m.sample("i:impactPunch_medium_001", .40, .055, cap=.13, hi=2300, role="body")
    else:
        m.add(fm(.22, 390, 1., .15, .057, .20), .27, .006, "feedback")
        m.add(fm(.14, 130, 1., .1, .035, .55), .10, .075, "feedback")
    return m.y

def land(v):
    m = Mix(.88)
    if v == 0:
        m.sample("i:impactPunch_heavy_002", .78, speed=.73, hi=1700, cap=.28)
        m.sample("i:impactMetal_medium_000", .28, .018, speed=.90, cap=.21, role="gear")
    elif v == 1:
        m.sample("i:footstep_wood_002", .70, cap=.15, speed=.85, role="front-foot")
        m.sample("i:footstep_concrete_004", .79, .042, cap=.23, speed=.78, role="rear-foot")
        m.sample("r:handleCoins", .20, .06, cap=.18, speed=.95, role="gear")
    elif v == 2:
        m.sample("i:impactPlate_heavy_000", .80, cap=.34, speed=.64, hi=3400)
        m.sample("i:impactMetal_light_002", .24, .041, cap=.16, speed=.9, role="gear")
    elif v == 3:
        m.sample("i:impactMining_004", .72, cap=.39, speed=.85, hi=3300)
        m.sample("i:footstep_concrete_000", .53, .016, cap=.23, speed=.69, role="attack")
    elif v == 4:
        m.sample("r:dropLeather", .60, cap=.24, speed=.75)
        m.sample("r:footstep00", .58, .016, cap=.21, speed=.7, role="attack")
        m.sample("r:clothBelt", .23, .048, cap=.23, speed=.87, role="gear")
    elif v == 5:
        m.sample("i:impactSoft_heavy_004", .71, cap=.28, speed=.8)
        m.sample("i:footstep_grass_003", .33, .027, cap=.26, speed=.83, role="dust")
        m.add(whoosh(.28, 1900, .025, .07), .10, .013, "dust")
    elif v == 6:
        m.sample("i:footstep_concrete_001", .80, cap=.19, speed=.85, role="attack")
        m.sample("r:metalLatch", .25, .018, cap=.13, speed=.85, role="gear")
    else:
        m.sample("i:impactWood_heavy_004", .57, cap=.28, speed=.72)
        m.sample("i:impactMetal_heavy_001", .65, .02, cap=.31, speed=.72, role="gear")
        m.sample("r:metalPot2", .17, .08, cap=.26, speed=.84, hi=3500, role="tail")
    # Low body is deliberately secondary to the material, not a shared drum preset.
    if v in (0,2,7):
        m.add(fm(.26, [91,78,70][[0,2,7].index(v)], 1, .08, .040, .72), .11, .005, "weight")
    return m.y

def fire(v):
    m = Mix(.90)
    lengths = [.37,.46,.27,.52,.47,.32,.57,.43]
    length = lengths[v]
    x = material("f:fire", cap=length, offset=[.08,.40,.82,1.12,1.60,.24,1.90,.63][v],
                 speed=[1.2,.85,1.7,1.1,1.3,.7,.78,1.4][v], lo=130,
                 hi=[5700,3200,7000,5300,6000,3400,2700,6200][v])
    t = np.arange(len(x)) / SR
    attack = [.006,.035,.001,.016,.004,.002,.055,.008][v]
    decay = [.13,.14,.065,.13,.13,.09,.16,.10][v]
    x *= (1-np.exp(-t/attack))*np.exp(-t/decay)
    m.add(x, .85, role="crackle", source="f:fire")
    if v == 0:
        m.sample("r:cloth4", .38, speed=1.6, cap=.22, lo=600, role="motion")
    elif v == 1:
        m.add(whoosh(.36, 2400, .10, .072), .44, role="roar")
        m.sample("r:cloth1", .21, .025, speed=1.2, cap=.28, role="motion")
    elif v == 2:
        m.sample("r:bookFlip3", .43, speed=2.2, cap=.12, lo=700, role="ignition")
        m.add(whoosh(.20, 6200, .012, .023), .25, role="roar")
    elif v == 3:
        m.sample("r:knifeSlice", .35, speed=1.2, cap=.32, role="motion")
        m.sample("r:knifeSlice2", .24, .10, speed=1.6, cap=.23, role="second sweep")
    elif v == 4:
        m.add(granular("f:fire", .35, .017, .034, 1.5), .47, .035, "embers")
        m.sample("r:cloth2", .16, speed=1.8, cap=.13, role="motion")
    elif v == 5:
        m.add(whoosh(.23, 1700, .024, .044), .68, role="roar")
        m.sample("i:impactSoft_heavy_000", .13, speed=.9, cap=.12, hi=900, role="ignition")
    elif v == 6:
        m.add(whoosh(.42, 1500, .14, .09), .41, role="roar")
        m.sample("r:cloth3", .20, .032, speed=.9, cap=.31, hi=2000, role="motion")
    else:
        m.sample("r:bookFlip1", .43, speed=1.9, cap=.17, lo=600, role="motion")
        m.add(whoosh(.28, 5400, .047, .043, 37), .30, role="roar")
    return m.y

def charge(v):
    m = Mix(.94)
    if v == 0:
        m.sample("r:metalClick", .55, speed=1.15, cap=.12, role="attack")
        m.sample("r:metalLatch", .77, .13, cap=.20, role="lock")
        m.sample("r:creak1", .19, .018, speed=1.5, cap=.20, role="tension")
    elif v == 1:
        m.sample("r:creak2", .75, speed=1.4, cap=.32, role="tension")
        m.sample("i:impactWood_light_002", .32, .27, cap=.15, role="lock")
    elif v == 2:
        for at, key, gain in [(.00,"r:metalClick",.42),(.055,"r:metalLatch",.42),
                               (.12,"r:metalClick",.31),(.19,"r:metalLatch",.65)]:
            m.sample(key, gain, at, speed=1.8, cap=.09, role="ratchet")
    elif v == 3:
        m.sample("r:beltHandle1", .70, cap=.26, speed=1.15, role="tension")
        m.sample("r:handleSmallLeather", .42, .065, cap=.23, speed=1.3, role="tension")
        m.sample("r:metalLatch", .38, .28, cap=.13, speed=1.15, role="lock")
    elif v == 4:
        m.sample("r:metalLatch", .90, speed=1.25, cap=.16, role="lock")
        m.add(pluck(.18, 180, .990, .65), .12, .005, "string")
    elif v == 5:
        m.sample("r:creak3", .68, speed=.83, cap=.35, role="tension")
        m.sample("i:impactMetal_medium_004", .43, .31, speed=.9, cap=.24, role="lock")
        m.sample("i:impactWood_medium_003", .22, .32, speed=.85, cap=.14, role="body")
    elif v == 6:
        m.sample("r:drawKnife2", .46, speed=1.5, cap=.18, role="slide")
        m.sample("r:metalClick", .63, .16, speed=1.7, cap=.13, role="lock")
    else:
        m.sample("r:clothBelt2", .34, cap=.20, role="tension")
        m.sample("r:creak1", .50, .02, cap=.28, speed=1.05, role="tension")
        m.add(pluck(.26, 130, .996, .25)[::-1], .22, .016, "string tension")
        m.sample("r:metalLatch", .62, .32, speed=.95, cap=.19, role="lock")
    return m.y

def shot(v):
    m = Mix(.79)
    f = [190,265,360,125,430,175,310,230][v]
    m.add(pluck(.30, f, [.993,.990,.986,.996,.981,.995,.988,.993][v], .43),
          [.36,.22,.62,.29,.43,.23,.28,.35][v], .005, "bowstring")
    if v == 0:
        m.sample("i:impactWood_medium_000", .59, cap=.14, speed=1.4, role="release")
        m.sample("r:knifeSlice", .26, .012, cap=.20, speed=1.8, role="flight")
    elif v == 1:
        m.sample("r:metalLatch", .70, cap=.16, speed=1.4, role="release")
        m.sample("r:drawKnife1", .29, .012, cap=.15, speed=1.7, role="flight")
    elif v == 2:
        m.sample("r:cloth4", .18, .015, cap=.16, speed=1.6, role="flight")
        m.sample("r:metalClick", .32, cap=.09, speed=1.8, role="release")
    elif v == 3:
        m.sample("i:impactWood_heavy_002", .49, cap=.22, speed=1.15, role="release")
        m.sample("i:impactMetal_light_004", .23, .022, cap=.12, speed=1.2, role="recoil")
        m.add(whoosh(.27, 2300, .045, .037), .23, .007, "flight")
    elif v == 4:
        m.sample("r:knifeSlice2", .45, cap=.16, speed=2.0, lo=400, role="flight")
        m.sample("r:metalClick", .18, cap=.07, speed=1.8, role="release")
    elif v == 5:
        m.sample("r:metalLatch", .53, cap=.13, speed=1.3, role="release")
        m.sample("i:impactTin_medium_002", .37, .033, cap=.15, speed=1.25, role="recoil")
        m.sample("r:clothBelt", .19, .06, cap=.14, speed=1.6, role="flight")
    elif v == 6:
        m.sample("r:knifeSlice", .48, cap=.20, speed=1.45, lo=650, role="flight")
        m.add(whoosh(.23, 5200, .032, .027), .38, role="flight")
        m.sample("i:impactWood_light_004", .20, cap=.08, speed=1.8, role="release")
    else:
        m.sample("r:metalLatch", .50, cap=.15, speed=1.15, role="release")
        m.sample("r:cloth2", .26, .018, cap=.20, speed=1.5, role="flight")
        m.add(echoes(pluck(.20, 230, .991, .4), (.038,.071,.10), (.14,.08,.03)),
              .16, .02, "short room tail")
    return m.y

GROUPS = [
    ("M", "01_magic", "마법 발사·비행", magic,
     ["유리 결정","공기 탄","책장 시전","전기 스파크","맑은 종","암흑 파동","유기적인 맥동","룬 연타"]),
    ("S", "02_soft_hit", "생물에게 마법 명중", soft_hit,
     ["천의 둔탁함","가죽 충격","촉촉한 충격","찰싹 타격","바람 관통","작은 마법 파열","묵직한 몸 충격","가벼운 톡"]),
    ("H", "03_hard_hit", "단단한 대상에게 명중", hard_hit,
     ["철판","갑주","돌","도자기 질감","청동 울림","쇠테","유리 갑주","둔한 금속"]),
    ("P", "04_player_hurt", "플레이어 피격", hurt,
     ["몸과 천","짧은 찌름","마법 충격","둔타","낮은 경고","가죽과 몸","날카로운 스침","게임형 하강"]),
    ("K", "05_knight_land", "기갑기마 착지", land,
     ["갑주와 쿵","발굽 이중 착지","쇳덩이","돌바닥 착지","가죽 갑주","먼지 착지","짧은 착지","육중한 갑주"]),
    ("F", "06_fire", "불길 펼침", fire,
     ["횃불 스윽","불꽃 파도","짧은 점화","회전 불길","불씨 연속","터지는 불길","부드러운 화르륵","거친 부채질"]),
    ("C", "07_rook_charge", "쇠뇌 장전", charge,
     ["걸쇠","목재 장력","금속 톱니","가죽줄 당김","짧은 잠금","큰 쇠뇌","정밀 기계","장력 후 잠금"]),
    ("R", "08_rook_fire", "쇠뇌 발사", shot,
     ["목재 쇠뇌","강철 쇠뇌","날카로운 현","묵직한 볼트","가벼운 휙","쇠기구 반동","공기 절단","짧은 잔향"]),
]

def finish(x):
    x = band(x, 35, 18000)
    x -= np.mean(x)
    # Trim excess silence while retaining quiet material tails.
    rms_curve = np.sqrt(np.convolve(x*x, np.ones(240)/240, mode="same"))
    active = np.flatnonzero(rms_curve > max(float(np.max(rms_curve))*.006, .00003))
    if not len(active):
        raise ValueError("Empty sound")
    x = x[max(0, active[0]-96):min(len(x), active[-1]+1440)]
    fade = min(round(.022*SR), len(x)//4)
    x[:72] *= np.linspace(0,1,72)
    x[-fade:] *= np.linspace(1,0,fade)**2
    rms = np.sqrt(np.mean(x*x))
    # Fair audition gain; transient limiting precedes a final safety ceiling.
    x *= .110 / max(rms,1e-12)
    x = .79 * np.tanh(x/.79)
    x *= .105 / max(np.sqrt(np.mean(x*x)), 1e-12)
    if np.max(np.abs(x))>.82:
        x *= .82/np.max(np.abs(x))
    x[0]=x[-1]=0
    return x

def wav_bytes(y):
    b=io.BytesIO()
    with wave.open(b,"wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(np.rint(np.clip(y,-1,1)*32767).astype("<i2").tobytes())
    return b.getvalue()

def stats(y):
    peak=float(np.max(np.abs(y)))
    rms=float(np.sqrt(np.mean(y*y)))
    tail=float(np.sqrt(np.mean(y[-480:]**2)))
    spec=np.abs(np.fft.rfft(y*np.hanning(len(y))))
    freq=np.fft.rfftfreq(len(y),1/SR)
    return {
        "duration_seconds":round(len(y)/SR,3),
        "peak_dbfs":round(20*np.log10(max(peak,1e-12)),2),
        "rms_dbfs":round(20*np.log10(max(rms,1e-12)),2),
        "tail_10ms_dbfs":round(20*np.log10(max(tail,1e-12)),2),
        "spectral_centroid_hz":round(float(np.sum(spec*freq)/max(np.sum(spec),1e-12))),
        "dc_offset":round(float(np.mean(y)),7),
        "clipped_samples":int(np.count_nonzero(np.abs(y)>=1)),
        "first_sample":float(y[0]),"last_sample":float(y[-1])
    }

def main():
    global USED, LAYERS
    candidates=ROOT/"candidates"; candidates.mkdir(exist_ok=True)
    audition=ROOT/"auditions"; audition.mkdir(exist_ok=True)
    manifest={"version":"CombatSFX-v2","date":"2026-10-03","status":"audition-concepts-not-game-integrated",
              "method":"source editing + material layering + original synthesis + gain control",
              "format":{"container":"WAV","sample_rate":SR,"channels":1,"bit_depth":16},
              "sources":[
                  {"id":"r","title":"Kenney RPG Audio","license":"CC0","url":"https://kenney.nl/assets/rpg-audio"},
                  {"id":"i","title":"Kenney Impact Sounds","license":"CC0","url":"https://kenney.nl/assets/impact-sounds"},
                  {"id":"f","title":"Fire Crackling","author":"AntumDeluge","license":"CC0","url":"https://opengameart.org/content/fire-crackling/"}
              ],
              "groups":[],"assets":[]}
    all_audio=[np.zeros(round(.5*SR))]
    hashes=set()
    for prefix,folder,title,create,names in GROUPS:
        group_audio=[np.zeros(round(.25*SR))]
        starts=[]
        folder_path=candidates/folder; folder_path.mkdir(exist_ok=True)
        for v,name in enumerate(names):
            USED=set();LAYERS=[]
            y=finish(create(v))
            file=folder_path/(f"{prefix}{v+1}.wav")
            data=wav_bytes(y); file.write_bytes(data)
            digest=hashlib.sha256(data).hexdigest()
            assert digest not in hashes, "Identical candidates"
            hashes.add(digest)
            measurements=stats(y)
            assert measurements["clipped_samples"]==0
            assert abs(measurements["dc_offset"])<.003
            assert measurements["tail_10ms_dbfs"]<-32
            assert .06<measurements["duration_seconds"]<1.2
            with wave.open(str(file),"rb") as w:
                assert w.getframerate()==SR and w.getnchannels()==1 and w.getsampwidth()==2
            asset={"id":f"{prefix}{v+1}","group":prefix,"category":title,"name":name,
                   "file":str(file.relative_to(ROOT)),"sources":sorted(USED),
                   "layers":LAYERS,"sha256":digest,**measurements}
            starts.append({"id":asset["id"],"name":name,
                           "start_seconds":round(sum(len(a) for a in group_audio)/SR,3)})
            manifest["assets"].append(asset)
            for n in range(2):
                group_audio.extend([y,np.zeros(round((.35 if n==0 else .9)*SR))])
        result=np.concatenate(group_audio)
        group_file=audition/(f"{prefix}_all_8.wav")
        group_file.write_bytes(wav_bytes(result))
        manifest["groups"].append({"id":prefix,"title":title,
            "file":str(group_file.relative_to(ROOT)),"candidate_count":8,
            "duration_seconds":round(len(result)/SR,3),
            "order":starts,"repeats_per_candidate":2})
        all_audio.extend([result,np.zeros(round(1.2*SR))])
        print(prefix,title,"8 designs",len(result)/SR,"seconds")
    combined=audition/"00_all_64.wav"
    combined.write_bytes(wav_bytes(np.concatenate(all_audio)))
    manifest["total_candidates"]=len(manifest["assets"])
    manifest["unique_sha256"]=len(hashes)
    manifest["combined_audition"]=str(combined.relative_to(ROOT))
    assert manifest["total_candidates"]==64
    (ROOT/"asset-manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    with (ROOT/"candidate-index.csv").open("w",newline="",encoding="utf-8-sig") as out:
        writer=csv.writer(out); writer.writerow(["ID","종류","후보명","길이(초)","파일"])
        for a in manifest["assets"]:
            writer.writerow([a["id"],a["category"],a["name"],a["duration_seconds"],a["file"]])
    (ROOT/"qa-report.json").write_text(json.dumps({
        "passed":True,"decoded_wav":64,"unique_files":64,"clipped_samples":0,
        "sample_rate":SR,"channels":1,"bit_depth":16,
        "limits":"Signal checks do not prove perceptual quality or gameplay fit."
    },ensure_ascii=False,indent=2)+"\n",encoding="utf-8")

if __name__=="__main__":
    main()
