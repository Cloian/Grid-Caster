"""기사단장 모션 시트(128px 프레임, 32 PPU) — 키포즈 8장 + 정수 이동·잔상으로 만든다.

게임 배치: 기사단장은 3×3을 차지하고 논리 좌표(GridPosition)는 가운데 칸이다. Sprite 기준점은
가로 가운데(64)·세로 50/128(가운데 칸 중심)이고, 말 몸통 가운데를 기준점 위에, 발굽을 3×3 아래 끝(바닥 2px 위)보다 2px 위에 그린다.
왼쪽을 보는 시트(_l)는 좌우 반전 그림이다(상태 이름을 맞추려고 따로 둔다).

출력: <out>/kc_<clip>.png, kc_<clip>_l.png  (+ durations.json)
실행: python3 kc_motions.py <keyposes 폴더> <out 폴더>
"""
import json
import sys
from pathlib import Path
from PIL import Image

S = 128
KEY = 128
OFFSET_X = 0            # 키포즈의 말 가운데(64) = 캔버스 가운데
OFFSET_Y = (S - 5) - (KEY - 6)   # 발굽 아래 줄을 바닥 4px 위에
WHITE, RED = (255, 255, 255), (224, 36, 44)


def place(img, mirror=False):
    if mirror:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    out = Image.new("RGBA", (S, S))
    out.alpha_composite(img, (OFFSET_X, OFFSET_Y))
    return out


def lift(img, dy):
    out = Image.new("RGBA", img.size)
    out.paste(img, (0, dy), img)
    return out


def dip(img, cut, d=1):
    """cut 줄 위(기수 상체)를 d만큼 내린다 — 숨쉬기."""
    src = img.load()
    out = Image.new("RGBA", img.size)
    p = out.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            if src[x, y][3] and y >= cut:
                p[x, y] = src[x, y]
    for y in range(cut - 1, -1, -1):
        for x in range(w):
            if src[x, y][3] and y + d < h:
                p[x, y + d] = src[x, y]
    return out


def smear(a, b):
    out = Image.new("RGBA", a.size)
    pa, pb, po = a.load(), b.load(), out.load()
    w, h = a.size
    for y in range(h):
        for x in range(w):
            ca, cb = pa[x, y], pb[x, y]
            if ca == cb:
                po[x, y] = ca
            elif ca[3] and cb[3]:
                po[x, y] = cb if (x + y) % 2 == 0 else ca
            else:
                c = cb if cb[3] else ca
                po[x, y] = c if (x + y) % 2 == 0 else (0, 0, 0, 0)
    return out


def recolor(img, color):
    out = img.copy()
    p = out.load()
    dark = min((c[:3] for c in img.getdata() if c[3]), key=sum)
    for y in range(img.size[1]):
        for x in range(img.size[0]):
            if p[x, y][3] and p[x, y][:3] != dark:
                p[x, y] = color + (255,)
    return out


def dim(img, k):
    out = img.copy()
    p = out.load()
    for y in range(img.size[1]):
        for x in range(img.size[0]):
            c = p[x, y]
            if c[3]:
                p[x, y] = tuple(int(v * k) for v in c[:3]) + (c[3],)
    return out


def scatter(img, keep, seed):
    import random
    rnd = random.Random(seed)
    out = img.copy()
    p = out.load()
    for y in range(img.size[1]):
        for x in range(img.size[0]):
            if p[x, y][3] and rnd.random() > keep:
                p[x, y] = (0, 0, 0, 0)
    return out


def clips(keys):
    K = keys
    rider = OFFSET_Y + 70          # 대략 안장 높이: 이 줄 위가 기수 상체
    idle = K[5]
    return {
        "idle": ([idle, dip(idle, rider), dip(idle, rider), idle], [320, 200, 240, 200], True),
        "move": ([K[2], lift(K[2], -1), K[2], lift(K[2], 1)], [90, 90, 90, 90], True),
        "attack": ([K[3], smear(K[3], K[4]), K[4], K[4]], [120, 60, 160, 200], False),
        "hit": ([recolor(idle, WHITE), recolor(idle, RED), lift(idle, -1), idle], [50, 80, 110, 140], False),
        "death": ([recolor(K[8], WHITE), K[8], dim(K[8], 0.8), dim(K[8], 0.6), scatter(dim(K[8], 0.6), 0.55, 1),
                   scatter(dim(K[8], 0.6), 0.25, 2), scatter(dim(K[8], 0.6), 0.06, 3)], [80, 160, 160, 140, 120, 120, 160], False),
        # 패턴: 준비(마지막 프레임 유지) · 발동
        "charge_windup": ([smear(idle, K[1]), lift(K[1], 1), K[1]], [60, 180, 300], False),
        "charge_release": ([smear(K[1], K[2]), K[2], lift(K[2], -1), K[2], smear(K[2], K[7]), K[7], lift(K[7], 1)],
                           [60, 90, 90, 90, 60, 200, 200], False),
        "recover": ([K[8], dip(K[8], rider)], [440, 440], True),
        "sweep_windup": ([smear(idle, K[3]), lift(K[3], 1), K[3]], [60, 160, 280], False),
        "sweep_release": ([smear(K[3], K[4]), K[4], lift(K[4], 1), smear(K[4], idle)], [60, 160, 200, 60], False),
        "summon_windup": ([lift(idle, -1), lift(idle, -2)], [160, 300], False),
        "summon_release": ([smear(idle, K[6]), K[6], K[6], smear(K[6], idle)], [60, 260, 300, 60], False),
    }


def main(src, out):
    src, out = Path(src), Path(out)
    out.mkdir(parents=True, exist_ok=True)
    names = {1: "01_charge_prepare", 2: "02_charge_release", 3: "03_sweep_prepare", 4: "04_sweep_strike",
             5: "05_summon_prepare", 6: "06_summon_command", 7: "07_charge_brake", 8: "08_exhausted_recover"}
    raw = {k: Image.open(src / f"kc_{v}.png").convert("RGBA") for k, v in names.items()}
    meta = {}
    for mirror, suffix in ((False, ""), (True, "_l")):
        keys = {k: place(v, mirror) for k, v in raw.items()}
        for name, (frames, ms, loop) in clips(keys).items():
            if mirror and name in ("hit", "death", "move", "attack"):
                continue
            sheet = Image.new("RGBA", (S * len(frames), S))
            for i, f in enumerate(frames):
                sheet.alpha_composite(f, (i * S, 0))
            sheet.save(out / f"kc_{name}{suffix}.png")
            meta[name + suffix] = {"durations": ms, "loop": loop}
    (out / "durations.json").write_text(json.dumps(meta, indent=1))
    print(json.dumps({k: v["durations"] for k, v in meta.items()}))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
