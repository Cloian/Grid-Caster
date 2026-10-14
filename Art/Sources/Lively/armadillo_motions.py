"""불타는 아르마딜로(비숍) 모션과 불길 타일을 만든다. 기존 armadillo_sheet.png의 12색 팔레트만 쓴다.

- armadillo_idle.png    64px × 4  : 기존 대기 프레임 그대로
- armadillo_move.png    64px × 6  : 몸을 만 공이 굴러간다(판 이음새가 회전, 불꽃은 위로 솟구침)
- armadillo_attack.png  64px × 4  : 불꽃이 확 일어난다(비숍은 직접 공격하지 않아 컨트롤러 구성용)
- armadillo_hit.png     64px × 4  : 흰색 → 붉은색 번쩍임 → 뒤로 밀림 → 복귀
- armadillo_death.png   64px × 8  : 불이 꺼지고 재로 변해 흩어진다
- fire_trail.png        32px × 4  : 불길 칸(바닥 32px 타일과 같은 픽셀 밀도)

실행: python3 scripts/armadillo_motions.py <armadillo_sheet.png> <out_dir>
"""
import math
import random
import sys
from pathlib import Path
from PIL import Image

H = lambda h: tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))
OUT = H("#000000")
SHELL_D, SHELL_M = H("#663931"), H("#9B5B50")
BODY_L, BODY_D, BODY_M = H("#BF7933"), H("#865320"), H("#A5672A")
RED, ORANGE, YELLOW = H("#C41B1B"), H("#FA7A00"), H("#F0E621")
AMBER, LEMON = H("#F5AC35"), H("#FBF236")
FLAME = {RED, ORANGE, YELLOW, AMBER, LEMON, H("#C1BA2C")}
WHITE = (255, 255, 255)
ASH_D, ASH_M, ASH_L = (58, 52, 56), (96, 90, 92), (140, 134, 134)
S = 64


def frames_of(sheet, size):
    return [sheet.crop((i * size, 0, (i + 1) * size, size)) for i in range(sheet.width // size)]


def strip(frames, size):
    out = Image.new("RGBA", (size * len(frames), size))
    for i, f in enumerate(frames):
        out.alpha_composite(f, (i * size, 0))
    return out


def recolor(img, fn):
    out = img.copy()
    px = out.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = fn((r, g, b)) + (255,)
    return out


def flame_tongue(img, base, angle, length, width):
    """base에서 angle 방향으로 뻗는 불꽃 혀. 바깥 빨강, 중간 주황, 안쪽 노랑."""
    px = img.load()
    ux, uy = math.cos(angle), math.sin(angle)
    for i in range(int(length)):
        t = i / max(1, length - 1)
        half = width * (1 - t) ** 0.8
        cx, cy = base[0] + ux * i, base[1] + uy * i
        for w in range(-int(half) - 1, int(half) + 2):
            x, y = int(round(cx - uy * w)), int(round(cy + ux * w))
            if not (0 <= x < img.width and 0 <= y < img.height):
                continue
            edge = abs(w) / max(0.5, half + 0.5)
            color = YELLOW if edge < 0.35 and t < 0.6 else ORANGE if edge < 0.75 and t < 0.85 else RED
            if abs(w) <= half + 0.5:
                px[x, y] = color + (255,)


def outline(img):
    out = img.copy()
    src = img.load()
    px = out.load()
    for y in range(img.height):
        for x in range(img.width):
            if src[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < img.width and 0 <= ny < img.height and src[nx, ny][3]:
                    px[x, y] = OUT + (255,)
                    break
    return out


def roll_frame(i, n):
    img = Image.new("RGBA", (S, S))
    px = img.load()
    cx, cy, r = 32, 35, 22
    phi = -2 * math.pi * i / n  # 시계 방향 회전
    rnd = random.Random(100 + i)
    # 불꽃: 공 위쪽에서 위로 솟구친다(이동 방향과 무관하게 자연스럽게)
    for k, a in enumerate((-135, -110, -88, -65)):
        ang = math.radians(a + rnd.uniform(-6, 6))
        base = (cx + math.cos(ang) * (r - 2), cy + math.sin(ang) * (r - 2))
        tall = k in (1, 2)
        flame_tongue(img, base, math.radians(-90) * 0.6 + ang * 0.4,
                     rnd.uniform(13, 16) if tall else rnd.uniform(9, 12), rnd.uniform(4.6, 5.6))
    for y in range(S):
        for x in range(S):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if d > r:
                continue
            light = (-dx - dy) / (r * 1.41)
            color = SHELL_M if light > 0.15 else SHELL_D
            if light > 0.55 and d < r - 4:
                color = BODY_L
            # 판 이음새: 회전하는 띠
            s = dx * math.cos(phi) + dy * math.sin(phi)
            if (s + 100) % 7 < 1.0 and d < r - 1.5:
                color = OUT
            px[x, y] = color + (255,)
    # 회전을 보여 주는 머리·꼬리 돌기(밝은 갈색)
    for off, size in ((0.0, 3), (math.pi, 2)):
        a = phi + off + math.radians(200)
        hx, hy = cx + math.cos(a) * (r - 1), cy + math.sin(a) * (r - 1)
        for yy in range(-size, size + 1):
            for xx in range(-size, size + 1):
                if xx * xx + yy * yy <= size * size:
                    x, y = int(hx + xx), int(hy + yy)
                    if 0 <= x < S and 0 <= y < S:
                        px[x, y] = (BODY_L if yy < 0 else BODY_M) + (255,)
    # 굴러가는 먼지 2px
    if i % 2 == 0:
        for x in (cx - 16, cx - 12):
            px[x, cy + r] = BODY_D + (255,)
    return outline(img)


def flare(frame, grow):
    """불꽃 픽셀을 위·옆으로 grow만큼 키우고 안쪽을 밝힌다."""
    img = frame.copy()
    src = frame.load()
    px = img.load()
    for _ in range(grow):
        snap = img.copy().load()
        for y in range(1, S - 1):
            for x in range(1, S - 1):
                if snap[x, y][3] and snap[x, y][:3] in FLAME:
                    for dx, dy in ((0, -1), (-1, 0), (1, 0)):
                        if not snap[x + dx, y + dy][3]:
                            px[x + dx, y + dy] = RED + (255,)
    for y in range(S):
        for x in range(S):
            c = px[x, y][:3]
            if px[x, y][3] and c == RED and src[x, y][3] and src[x, y][:3] in FLAME:
                px[x, y] = ORANGE + (255,)
            elif px[x, y][3] and c == ORANGE:
                px[x, y] = YELLOW + (255,)
    return outline_only_new(img, frame)


def outline_only_new(img, ref):
    return img


def hit_frames(idle):
    white = recolor(idle, lambda c: WHITE)
    red = recolor(idle, lambda c: RED if c not in (OUT,) else OUT)
    shifted = Image.new("RGBA", (S, S))
    shifted.alpha_composite(idle, (2, 0))
    return [white, red, shifted, idle]


def death_frames(idle):
    out = [idle]
    # 불꽃이 아래에서부터 꺼진다: 위쪽 불꽃 픽셀부터 제거
    flame_rows = [y for y in range(S) for x in range(S) if idle.getpixel((x, y))[3] and idle.getpixel((x, y))[:3] in FLAME]
    top = min(flame_rows) if flame_rows else 0
    bottom = max(flame_rows) if flame_rows else S
    for cut in (0.45, 0.8, 1.01):
        limit = top + (bottom - top) * cut
        img = idle.copy()
        px = img.load()
        for y in range(S):
            for x in range(S):
                if px[x, y][3] and px[x, y][:3] in FLAME and y < limit:
                    px[x, y] = (0, 0, 0, 0)
        out.append(outline(clean_outline(img)))
    body = out[-1]
    ash = recolor(body, lambda c: OUT if c == OUT else ASH_L if c in (BODY_L, SHELL_M) else ASH_M if c in (BODY_M, BODY_D) else ASH_D)
    out.append(ash)
    rnd = random.Random(7)
    for keep in (0.6, 0.3, 0.08):
        img = ash.copy()
        px = img.load()
        for y in range(S):
            for x in range(S):
                if px[x, y][3] and rnd.random() > keep:
                    px[x, y] = (0, 0, 0, 0)
        out.append(img)
    return out


def clean_outline(img):
    """불꽃을 지운 뒤 남은 외곽선(검정) 중 몸통과 닿지 않는 것은 지운다."""
    out = img.copy()
    px = out.load()
    for y in range(S):
        for x in range(S):
            if px[x, y][3] and px[x, y][:3] == OUT:
                near = False
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < S and 0 <= ny < S and px[nx, ny][3] and px[nx, ny][:3] != OUT:
                            near = True
                if not near:
                    px[x, y] = (0, 0, 0, 0)
    return out


def fire_tile(i):
    img = Image.new("RGBA", (32, 32))
    rnd = random.Random(300 + i)
    px = img.load()
    # 바닥 불씨(낮은 띠)
    for x in range(3, 29):
        for y in range(24, 29):
            if abs(x - 16) < 12 - (y - 24) * 0.5:
                px[x, y] = (RED if y > 26 else ORANGE) + (255,)
    for cx, h in ((8, rnd.uniform(11, 15)), (15, rnd.uniform(18, 23)), (21, rnd.uniform(15, 19)), (26, rnd.uniform(9, 12))):
        flame_tongue(img, (cx + rnd.uniform(-1, 1), 27), math.radians(-90 + rnd.uniform(-8, 8)), h, rnd.uniform(3.2, 4.4))
    return outline(img)


if __name__ == "__main__":
    sheet = Image.open(sys.argv[1]).convert("RGBA")
    out = Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    idle = frames_of(sheet, S)
    strip(idle, S).save(out / "armadillo_idle.png")
    strip([roll_frame(i, 6) for i in range(6)], S).save(out / "armadillo_move.png")
    strip([flare(idle[0], 1), flare(idle[1], 2), flare(idle[2], 3), flare(idle[3], 1)], S).save(out / "armadillo_attack.png")
    strip(hit_frames(idle[0]), S).save(out / "armadillo_hit.png")
    strip(death_frames(idle[0]), S).save(out / "armadillo_death.png")
    strip([fire_tile(i) for i in range(4)], 32).save(out / "fire_trail.png")
    print("saved", sorted(p.name for p in out.glob("*.png")))
