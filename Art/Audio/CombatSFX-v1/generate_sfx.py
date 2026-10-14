"""Original procedural combat SFX sketches. No samples or external models."""
from pathlib import Path
import json
import math
import wave
import numpy as np

ROOT = Path(__file__).resolve().parent
SR = 48000
rng = np.random.default_rng(20261003)

def clock(seconds):
    return np.arange(round(seconds * SR), dtype=np.float64) / SR

def lowpass(x, cutoff):
    a = math.exp(-2 * math.pi * cutoff / SR)
    out = np.empty_like(x)
    value = 0.0
    for i, sample in enumerate(x):
        value = (1 - a) * sample + a * value
        out[i] = value
    return out

def noise(n, lo=100, hi=8000):
    x = rng.normal(0, 1, n)
    x = lowpass(x, hi)
    if lo:
        x -= lowpass(x, lo)
    return x

def env(t, attack=.003, decay=.10):
    return (1 - np.exp(-t / attack)) * np.exp(-t / decay)

def chirp(t, start, end, decay=.055, harmonics=(1.,)):
    f = end + (start - end) * np.exp(-t / decay)
    phase = 2 * np.pi * np.cumsum(f) / SR
    return sum(weight * np.sin((i + 1) * phase)
               for i, weight in enumerate(harmonics))

def ring(t, frequencies, decays, weights, attack=.0015):
    y = np.zeros(len(t))
    for f, d, w in zip(frequencies, decays, weights):
        y += w * np.sin(2 * np.pi * f * t) * env(t, attack, d)
    return y

def add(y, x, at):
    start = round(at * SR)
    end = min(len(y), start + len(x))
    if end > start:
        y[start:end] += x[:end - start]

def air(t, lo, hi, attack, decay, amp):
    return amp * noise(len(t), lo, hi) * env(t, attack, decay)

def finish(y, target_db=-20.0):
    y = np.asarray(y, dtype=np.float64)
    y -= np.mean(y)
    fade_in = min(len(y), round(.001 * SR))
    fade_out = min(len(y), round(.025 * SR))
    y[:fade_in] *= np.linspace(0, 1, fade_in)
    y[-fade_out:] *= np.linspace(1, 0, fade_out) ** 2
    # Gentle saturation keeps the different sound families at comparable levels.
    y = np.tanh(y * 1.05)
    rms = np.sqrt(np.mean(y * y))
    y *= 10 ** (target_db / 20) / max(rms, 1e-12)
    peak = np.max(np.abs(y))
    if peak > .78:
        y *= .78 / peak
    y[0] = y[-1] = 0
    return y

def spell_cast():
    t = clock(.36)
    y = .43 * chirp(t, 1050, 390, .065, (1, .12)) * env(t, .008, .070)
    y += air(t, 600, 5000, .010, .075, .45)
    sparkle = ring(t, [1175, 1762, 2350], [.075, .058, .035], [.13, .055, .025])
    add(y, sparkle, .045)
    return y

def spell_hit_soft():
    t = clock(.24)
    y = .80 * chirp(t, 190, 85, .018) * env(t, .0018, .038)
    y += air(t, 190, 2800, .0015, .018, .70)
    y += ring(t, [740, 1110], [.032, .025], [.13, .045])
    return y

def spell_hit_hard():
    t = clock(.30)
    y = .34 * chirp(t, 250, 120, .014) * env(t, .0015, .032)
    y += air(t, 1100, 6200, .0008, .009, .80)
    y += ring(t, [790, 1319, 2017, 2929], [.075, .061, .040, .022],
              [.24, .15, .075, .025])
    return y

def player_hurt():
    t = clock(.32)
    y = .62 * chirp(t, 300, 95, .027, (1, .24, .08)) * env(t, .002, .055)
    y += air(t, 130, 2100, .0013, .028, .70)
    # Lower two-note interruption distinguishes loss of player health from hits.
    accent = chirp(t, 155, 78, .018) * env(t, .003, .037) * .25
    add(y, accent, .048)
    return y

def knight_land():
    t = clock(.43)
    y = .85 * chirp(t, 135, 57, .022, (1, .20)) * env(t, .0025, .064)
    y += air(t, 80, 1900, .001, .029, .78)
    y += ring(t, [610, 957, 1483], [.049, .034, .025], [.14, .07, .04])
    rattle = ring(t, [803, 1201], [.029, .022], [.07, .035])
    add(y, rattle, .040)
    add(y, rattle * .40, .076)
    return y

def fire_trail_sweep():
    t = clock(.52)
    # Moving filters and a swell create a travelling flare, not an idle fire loop.
    broad = noise(len(t), 130, 4300)
    fine = noise(len(t), 1600, 6500)
    swell = (1 - np.exp(-t / .028)) * np.exp(-t / .085)
    y = (broad * .95 + fine * .17) * swell
    y += .12 * chirp(t, 280, 75, .055) * env(t, .020, .055)
    for at, level in [(0.018, .12), (.054, .10), (.090, .065), (.137, .038)]:
        ct = clock(.060)
        crackle = noise(len(ct), 1500, 7000) * env(ct, .0006, .005) * level
        add(y, crackle, at)
    return y

def rook_charge():
    t = clock(.50)
    tension_t = clock(.24)
    f = 160 + 220 * (tension_t / .24) ** 1.4
    phase = 2 * np.pi * np.cumsum(f) / SR
    tension = (np.sin(phase) * .13 + np.sin(phase * 2.08) * .045)
    tension *= np.sin(np.pi * tension_t / .24) ** 1.3
    y = np.zeros(len(t))
    add(y, tension, .024)
    for at, gain in [(.002, .9), (.115, .45), (.253, 1.0)]:
        ct = clock(.15)
        click = ring(ct, [510, 1373, 2450], [.018, .014, .009], [.23, .10, .035])
        click += noise(len(ct), 700, 5500) * env(ct, .0007, .004) * .40
        add(y, click * gain, at)
    return y

def rook_fire():
    t = clock(.37)
    y = .40 * chirp(t, 860, 185, .012, (1, .22)) * env(t, .001, .020)
    y += ring(t, [690, 1510, 2587], [.033, .021, .012], [.20, .13, .055])
    y += air(t, 500, 7000, .0007, .008, 1.15)
    flight = noise(len(t), 650, 4400) * env(t, .006, .035) * .45
    flight += chirp(t, 1380, 550, .032) * env(t, .006, .026) * .055
    add(y, flight, .012)
    return y

SPECS = [
    ("01_spell_cast", "플레이어 마법 발사·비행", "짧은 마법음과 빠른 비행", spell_cast),
    ("02_spell_hit_soft", "생물에게 마법 명중", "부드러운 몸 충격과 작은 마법 파열", spell_hit_soft),
    ("03_spell_hit_hard", "단단한 대상에게 마법 명중", "짧은 갑주 충격과 마법 파열", spell_hit_hard),
    ("04_player_hurt", "플레이어 일반 피격", "낮은 충격과 짧은 하강음", player_hurt),
    ("05_knight_land", "기갑기마 착지", "무게 있는 착지와 갑주 흔들림", knight_land),
    ("06_fire_trail_sweep", "아르마딜로 불길 펼침", "짧게 퍼지는 불꽃과 마찰", fire_trail_sweep),
    ("07_rook_charge", "쇠뇌 장전", "줄 당김과 걸쇠 잠김", rook_charge),
    ("08_rook_fire", "쇠뇌 발사", "빠른 줄 튕김과 날카로운 비행", rook_fire),
]

def write_wav(path, y):
    pcm = np.rint(np.clip(y, -1, 1) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(SR)
        out.writeframes(pcm.tobytes())

def metrics(y):
    rms = float(np.sqrt(np.mean(y * y)))
    tail = y[-round(.020 * SR):]
    return {
        "duration_seconds": round(len(y) / SR, 4),
        "peak_dbfs": round(20 * math.log10(max(float(np.max(np.abs(y))), 1e-12)), 2),
        "rms_dbfs": round(20 * math.log10(max(rms, 1e-12)), 2),
        "tail_20ms_rms_dbfs": round(20 * math.log10(max(float(np.sqrt(np.mean(tail * tail))), 1e-12)), 2),
        "dc_offset": round(float(np.mean(y)), 8),
        "clipped_samples": int(np.sum(np.abs(y) >= 1)),
        "first_sample": float(y[0]), "last_sample": float(y[-1]),
    }

manifest = {
    "version": "CombatSFX-v1",
    "created": "2026-10-03",
    "status": "listening_drafts_not_connected_to_game",
    "method": "original deterministic procedural synthesis",
    "source_samples": [],
    "format": {"sample_rate": SR, "channels": 1, "bit_depth": 16, "container": "WAV"},
    "generator": "generate_sfx.py",
    "assets": [],
}
playlist = [np.zeros(round(.30 * SR))]
for index, (name, title, description, create) in enumerate(SPECS, 1):
    y = finish(create())
    path = ROOT / (name + ".wav")
    write_wav(path, y)
    measured = metrics(y)
    assert measured["clipped_samples"] == 0
    assert abs(measured["dc_offset"]) < .001
    assert measured["tail_20ms_rms_dbfs"] < -42
    with wave.open(str(path), "rb") as check:
        assert check.getnchannels() == 1 and check.getsampwidth() == 2
        assert check.getframerate() == SR and check.getnframes() == len(y)
    manifest["assets"].append({
        "order": index, "file": path.name, "title": title,
        "description": description, **measured,
    })
    for repeat in range(3):
        playlist.append(y)
        playlist.append(np.zeros(round((.35 if repeat < 2 else 1.0) * SR)))
write_wav(ROOT / "00_audition_all_8.wav", np.concatenate(playlist))
manifest["audition"] = {
    "file": "00_audition_all_8.wav",
    "order": "01 through 08; each sound repeated three times",
    "duration_seconds": round(sum(len(x) for x in playlist) / SR, 3),
}
(ROOT / "asset-manifest.json").write_text(
    json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
)
print(json.dumps(manifest, ensure_ascii=False, indent=2))

