"""나이트 착지 십자칸 이펙트(32px 타일 크기, 맵 사암 팔레트).

- knight_quake_crack.png 32px × 8 : 바닥 층(캐릭터 아래). 충격 구덩이 → 갈라지는 균열 → 균열이 남는다.
- knight_quake_dust.png  32px × 8 : 캐릭터 위 층. 튀는 돌조각 → 피어오르는 먼지구름 → 흩어짐.
땅 흔들림은 게임 코드(카메라 흔들림 + 이펙트 떨림)가 맡는다.

실행: python3 knight_quake.py <out_dir>
"""
import math
import random
import sys
from pathlib import Path
from PIL import Image

H = lambda h: tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))
DEEP, DARK, MID = H("#281C2A"), H("#46303C"), H("#684A4C")
SAND_D, SAND, SAND_L, SAND_H = H("#8C685A"), H("#B0886A"), H("#DCBA90"), H("#F0D8AE")
CREAM = H("#FFF4DC")
T, N = 32, 8
C = (15.5, 16.5)


def canvas():
    return Image.new("RGBA", (T, T))


def put(px, x, y, c):
    x, y = int(round(x)), int(round(y))
    if 0 <= x < T and 0 <= y < T:
        px[x, y] = c + (255,)


def crack_paths(seed=5):
    """중심에서 가장자리로 뻗는 들쭉날쭉한 균열 4줄과 잔가지."""
    rnd = random.Random(seed)
    paths = []
    for k, base in enumerate((-140, -40, 40, 140)):
        ang = math.radians(base + rnd.uniform(-20, 20))
        x, y = C
        pts = []
        for step in range(16):
            ang += rnd.uniform(-0.45, 0.45)
            x += math.cos(ang)
            y += math.sin(ang) * 0.75  # 비스듬한 시점: 세로는 짧게
            pts.append((x, y))
        paths.append(pts)
        # 잔가지
        bx, by = pts[6]
        ang2 = ang + rnd.choice((-1, 1)) * 0.9
        twig = []
        for _ in range(5):
            bx += math.cos(ang2); by += math.sin(ang2) * 0.75
            twig.append((bx, by))
        paths.append(twig)
    return paths


def crack_frame(i, paths):
    img = canvas()
    px = img.load()
    grow = (0.25, 0.55, 0.85, 1, 1, 1, 1, 1)[i]
    # 충격 구덩이(납작한 타원): 테두리 어둡게, 안쪽 더 어둡게
    rx, ry = (3, 4, 5, 5, 5, 5, 5, 5)[i], (2, 2, 3, 3, 3, 3, 3, 3)[i]
    for y in range(T):
        for x in range(T):
            d = ((x + 0.5 - C[0]) / rx) ** 2 + ((y + 0.5 - C[1]) / ry) ** 2
            if d <= 1:
                px[x, y] = (DEEP if d < 0.45 else DARK) + (255,)
            elif d <= 1.7 and y < C[1]:
                px[x, y] = SAND_D + (255,)  # 위쪽 테두리: 들린 흙
            elif d <= 1.7:
                px[x, y] = SAND_L + (255,)  # 아래쪽 테두리: 빛 받는 면
    for n, pts in enumerate(paths):
        twig = n % 2 == 1
        if twig and grow < 0.85:
            continue
        upto = max(1, int(len(pts) * grow))
        for j, (x, y) in enumerate(pts[:upto]):
            put(px, x, y, DEEP if j < len(pts) * 0.6 and not twig else DARK)
            if not twig and j < 6:  # 중심 가까이는 2px로 굵게
                put(px, x + 1, y, DEEP)
            # 균열 아래 가장자리 하이라이트(빛은 왼쪽 위 → 틈 아래쪽 면이 밝다)
            if not twig and j % 2 == 0:
                bx, by = int(round(x)), int(round(y)) + 1
                if 0 <= bx < T and 0 <= by < T and not px[bx, by][3]:
                    px[bx, by] = SAND_L + (255,)
    # 남은 균열 프레임(5~7)은 같은 그림: 게임에서 잠깐 유지한 뒤 흐려진다.
    return img


def puff(px, cx, cy, r, shade_bottom=True):
    for y in range(T):
        for x in range(T):
            d = math.hypot(x + 0.5 - cx, (y + 0.5 - cy) * 1.15)
            if d <= r:
                lit = (x + 0.5 - cx) + (y + 0.5 - cy) * 1.2  # 왼쪽 위 빛
                c = CREAM if lit < -r * 0.45 else SAND_H if lit < r * 0.35 else SAND_L
                if shade_bottom and y + 0.5 - cy > r * 0.55:
                    c = SAND
                px[x, y] = c + (255,)


def dust_frame(i, rnd_seed=9):
    img = canvas()
    px = img.load()
    rnd = random.Random(rnd_seed)
    # 퍼지는 먼지 덩어리 6개: (각도, 거리)
    blobs = [(a, rnd.uniform(0.8, 1.2)) for a in (200, 250, 300, 340, 20, 160)]
    spread = (2, 5, 8, 10, 11, 12, 12, 12)[i]
    rise = (0, 1, 2, 4, 6, 8, 9, 10)[i]
    radius = (2.0, 3.0, 4.0, 4.6, 4.4, 3.6, 2.6, 1.5)[i]
    for a, k in blobs:
        ang = math.radians(a)
        cx = C[0] + math.cos(ang) * spread * k
        cy = C[1] + 4 + math.sin(ang) * spread * k * 0.5 - rise * (0.6 if abs(math.cos(ang)) > 0.7 else 1.0)
        puff(px, cx, cy, radius * (1.0 if k > 1 else 0.85))
    # 먼지 덩어리 바깥 테두리(흙 그림자색): 사암 바닥 위에서도 읽히게
    src = img.copy().load()
    for y in range(T):
        for x in range(T):
            if src[x, y][3]:
                continue
            if any(0 <= x + dx < T and 0 <= y + dy < T and src[x + dx, y + dy][3]
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                px[x, y] = (MID if i < 5 else SAND_D) + (255,)
    # 튀는 돌조각(0~3프레임): 포물선
    if i <= 3:
        for k, (vx, vy) in enumerate(((-5, -7), (4, -8), (-2, -9), (6, -5), (-6, -4))):
            t = (i + 1) / 4
            x = C[0] + vx * t * 1.6
            y = C[1] + vy * t * 1.6 + 9 * t * t * 1.6
            put(px, x, y, DARK)
            put(px, x + 1, y, MID)
            put(px, x, y - 1, SAND_L if k % 2 else SAND)
    return img


if __name__ == "__main__":
    out = Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    paths = crack_paths()
    for name, fn in (("knight_quake_crack", lambda i: crack_frame(i, paths)), ("knight_quake_dust", dust_frame)):
        sheet = Image.new("RGBA", (T * N, T))
        for i in range(N):
            sheet.alpha_composite(fn(i), (i * T, 0))
        sheet.save(out / f"{name}.png")
    print("saved")
