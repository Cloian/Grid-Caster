"""여왕 보스(승인 v10, 120px·16색 그대로) 모션 시트. 픽셀을 늘리거나 돌리지 않고 행 단위 정수 이동만 쓴다.

- queen_idle.png   120 × 4 : 숨쉬기(상체 1px 내려옴), 왕관 보석 반짝임
- queen_move.png   120 × 4 : 여왕은 이동하지 않아 대기와 같은 그림(컨트롤러 형식 유지용)
- queen_attack.png 120 × 6 : 시전 — 웅크림 → 떠오르며 보석·금빛 마력 → 내려치며 방사 파편(4번째 = 발동) → 복귀
- queen_hit.png    120 × 4 : 흰색·붉은색 번쩍임 → 뒤(위)로 밀림 → 복귀
- queen_death.png  120 × 8 : 번쩍임 → 망토 속으로 무너짐 → 재 → 흩어짐

실행: python3 queen_motions.py <queen_boss.png> <out_dir>
"""
import math
import random
import sys
from pathlib import Path
from PIL import Image

S = 120
WHITE = (255, 255, 255)
RED = (224, 36, 44)
GOLD_L, CYAN_L = (255, 230, 128), (160, 240, 255)
ASH_D, ASH_M, ASH_L = (58, 52, 56), (96, 90, 92), (140, 134, 134)


def blank():
    return Image.new("RGBA", (S, S))


def strip(frames):
    out = Image.new("RGBA", (S * len(frames), S))
    for i, f in enumerate(frames):
        out.alpha_composite(f, (i * S, 0))
    return out


def shift(img, dy):
    out = blank()
    out.paste(img, (0, dy), img)
    return out


def body_drop(img, cut, d):
    """cut 위쪽(상체)을 d만큼 내려 망토 위에 겹친다(아래는 그대로) → 숨쉬기·주저앉기."""
    src = img.load()
    out = blank()
    px = out.load()
    for y in range(S):
        for x in range(S):
            if src[x, y][3] and y >= cut:
                px[x, y] = src[x, y]
    for y in range(cut - 1, -1, -1):
        for x in range(S):
            if src[x, y][3] and 0 <= y + d < S:
                px[x, y + d] = src[x, y]
    return out


def recolor(img, fn):
    out = img.copy()
    px = out.load()
    for y in range(S):
        for x in range(S):
            if px[x, y][3]:
                px[x, y] = fn(px[x, y][:3]) + (255,)
    return out


def darkest(img):
    return min({c[:3] for c in img.getdata() if c[3]}, key=sum)


def dots(img, pts, color):
    out = img.copy()
    px = out.load()
    for x, y in pts:
        if 0 <= x < S and 0 <= y < S and not px[x, y][3]:
            px[x, y] = color + (255,)
    return out


def sparkle(img, cx, cy, radius, count, color, seed):
    """(cx,cy) 둘레에 십자 모양 반짝이(3px)를 흩뿌린다. 빈 칸에만 그린다."""
    rnd = random.Random(seed)
    pts = []
    for k in range(count):
        a = 2 * math.pi * k / count + rnd.uniform(-0.2, 0.2)
        x, y = int(round(cx + math.cos(a) * radius)), int(round(cy + math.sin(a) * radius * 0.8))
        pts += [(x, y), (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)]
    return dots(img, pts, color)


def main(src, out):
    base = Image.open(src).convert("RGBA")
    bb = base.getbbox()
    cx = (bb[0] + bb[2]) // 2
    waist = bb[1] + (bb[3] - bb[1]) * 2 // 5          # 상체/망토 경계
    # 왕관 보석: 가장 채도 높은 청록 픽셀들
    px = base.load()
    gems = [(x, y) for y in range(bb[1], bb[1] + 30) for x in range(S)
            if px[x, y][3] and px[x, y][2] > 170 and px[x, y][0] < 120]
    glint = lambda img: dots(recolor(img, lambda c: c), [], WHITE) if not gems else _glint(img, gems)
    crown_y = bb[1] + 8

    idle = [base.copy(), body_drop(base, waist, 1), glint(body_drop(base, waist, 1)), base.copy()]
    move = [f.copy() for f in idle]
    attack = [
        body_drop(base, waist, 2),
        glint(shift(base, -2)),
        sparkle(glint(shift(base, -3)), cx, crown_y, 26, 8, GOLD_L, 1),
        sparkle(sparkle(shift(base, 1), cx, bb[1] + 50, 50, 12, CYAN_L, 2), cx, bb[1] + 50, 38, 10, WHITE, 3),
        sparkle(body_drop(base, waist, 1), cx, bb[1] + 50, 56, 8, CYAN_L, 4),
        base.copy(),
    ]
    out_c = darkest(base)
    white = recolor(base, lambda c: c if c == out_c else WHITE)
    red = recolor(base, lambda c: c if c == out_c else RED)
    hit = [white, red, shift(base, -2), base.copy()]

    lum = lambda c: sum(c) / 3
    pose = [body_drop(base, waist + 10 * t, 4 * t) for t in (1, 2, 3)]
    ash = recolor(pose[-1], lambda c: c if c == out_c else ASH_L if lum(c) > 150 else ASH_M if lum(c) > 80 else ASH_D)
    death = [white] + pose + [ash]
    rnd = random.Random(11)
    for keep in (0.55, 0.25, 0.06):
        img = ash.copy()
        p = img.load()
        for y in range(S):
            for x in range(S):
                if p[x, y][3] and rnd.random() > keep:
                    p[x, y] = (0, 0, 0, 0)
        death.append(shift(img, 1))

    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    for name, frames in (("idle", idle), ("move", move), ("attack", attack), ("hit", hit), ("death", death)):
        strip(frames).save(out / f"queen_{name}.png")
    print("gems", len(gems), "waist", waist, "bbox", bb)


def _glint(img, gems):
    out = img.copy()
    p = out.load()
    # 보석의 왼쪽 위 픽셀을 흰색으로(광원 왼쪽 위)
    top = min(gems, key=lambda g: (g[1], g[0]))
    for x, y in (top, (top[0] + 1, top[1])):
        if p[x, y][3]:
            p[x, y] = WHITE + (255,)
    return out


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
