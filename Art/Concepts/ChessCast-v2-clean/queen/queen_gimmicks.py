"""여왕 보스 기믹 모션 초안(120px 여왕 원본 팔레트 그대로) — 5기믹 × 4프레임.

1 ROYAL LINES  : 모음 → 양팔 벌림 → 금빛 선 발사 → 복귀
2 BOMBARDMENT  : 한 팔 들기 → 빛 구슬 → 빛기둥 낙하 → 복귀
3 GUARD ADVANCE: 지휘 → 근위병 소환 → 근위병 전진 → 휴식
4 FORTRESS     : 벽 세우기 → 유지 → 벽 사이 선 발사 → 복귀
5 CHECKMATE    : 안전지대 공개 → A 타격 → B 타격 → 탈진

여왕 몸은 행 단위 정수 이동(body_drop)만 쓰고, 팔은 소매(청록)·금 소맷단·크림 손을 새로 그려 붙인다.
원래 손은 소매 속(어두운 청록)으로 감춘다. 효과는 여왕 밖 넓은 칸에 그린다.

출력
- queen_gimmick_<name>.png : 여왕 몸만 120×120 × 4 (게임 애니메이션용)
- queen_gimmicks_sheet.png  : 효과 포함 발표용 시트(여왕 2배 = 게임 속 화소 비율, 근위병 64px 원본)
실행: python3 queen_gimmicks.py
"""
import math
import random
from pathlib import Path
from PIL import Image, ImageDraw
from queen_arms import paste_arm

HERE = Path(__file__).resolve().parent
S = 120
O = (7, 19, 27)
TEAL_D, TEAL, TEAL_M, TEAL_L = (4, 42, 50), (6, 69, 77), (12, 99, 109), (36, 125, 133)
GOLD_D, GOLD, GOLD_M, GOLD_L = (157, 102, 29), (206, 144, 41), (242, 183, 66), (255, 207, 104)
CREAM, WHITE = (250, 233, 204), (255, 255, 255)
CYAN = (18, 190, 214)
SAFE_A, SAFE_B = (90, 170, 255), (255, 92, 84)
STONE = [(110, 98, 92), (176, 164, 146), (222, 210, 186), (246, 238, 220)]

SHOULDER_L, SHOULDER_R = (47, 56), (73, 56)
SLEEVE_TOP, SLEEVE_BOTTOM = 62, 82
WAIST = 49


def blank(w=S, h=S):
    return Image.new("RGBA", (w, h))


def shift(img, dx=0, dy=0):
    out = blank(*img.size)
    out.paste(img, (dx, dy), img)
    return out


def body_drop(img, cut, d):
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


def remove_sleeve(img, side):
    """원래 소매(어깨 장식 아래~손)를 지운다. 가운데(몸통)에서 바깥으로 훑어 첫 외곽선 바깥을 비우고,
    몸통 쪽 외곽선 한 줄은 남긴다. side: -1 왼쪽, +1 오른쪽."""
    out = img.copy()
    p = out.load()
    for y in range(SLEEVE_TOP, SLEEVE_BOTTOM + 1):
        x = 60
        while 0 < x < S - 1 and not (p[x, y][3] and p[x, y][:3] == O):
            x += side
        # x = 몸통 바깥 첫 외곽선. 그 바깥은 모두 소매.
        xx = x + side
        while 0 <= xx < S:
            p[xx, y] = (0, 0, 0, 0)
            xx += side
    # 지운 자리 아래(망토 시작) 위쪽 가장자리에 외곽선을 다시 두른다
    for y in range(SLEEVE_BOTTOM + 1, SLEEVE_BOTTOM + 4):
        for x in range(S):
            if (x - 60) * side > 0 and p[x, y][3] and p[x, y][:3] != O and p[x, y - 1][3] == 0:
                p[x, y - 1] = O + (255,)
    return out


def draw_arm(img, shoulder, hand, drop=0):
    """어깨에서 손까지 끝으로 갈수록 넓어지는 소매 + 금 소맷단 + 크림 손. 새 픽셀 둘레에만 외곽선."""
    out = img.copy()
    p = out.load()
    sx, sy = shoulder[0], shoulder[1] + drop
    hx, hy = hand
    new = set()
    length = max(1, math.hypot(hx - sx, hy - sy))
    ux, uy = (hx - sx) / length, (hy - sy) / length
    nx, ny = -uy, ux                      # 소매의 한쪽 면(그늘 판정용)
    steps = int(length * 3)
    for i in range(steps + 1):
        t = i / steps
        if t > 0.78:
            break
        cx, cy = sx + (hx - sx) * t, sy + (hy - sy) * t
        r = 3.0 + 2.4 * t
        cuff = t > 0.68
        for yy in range(int(cy - r - 1), int(cy + r + 2)):
            for xx in range(int(cx - r - 1), int(cx + r + 2)):
                dx, dy = xx - cx, yy - cy
                if dx * dx + dy * dy > r * r or not (0 <= xx < S and 0 <= yy < S):
                    continue
                side = dx * nx + dy * ny
                if cuff:
                    c = GOLD_L if side < -r * 0.3 else GOLD_M if side < r * 0.4 else GOLD
                else:
                    c = TEAL_L if side < -r * 0.4 else TEAL_M if side < r * 0.3 else TEAL
                p[xx, yy] = c + (255,)
                new.add((xx, yy))
    # 손은 소맷단 바로 끝에 붙인다(소매 끝 중심 + 반지름만큼 앞)
    r_end = 3.0 + 2.4 * 0.78
    hx = round(sx + (hand[0] - sx) * 0.78 + ux * (r_end - 0.5))
    hy = round(sy + (hand[1] - sy) * 0.78 + uy * (r_end - 0.5))
    for yy in range(hy - 2, hy + 3):
        for xx in range(hx - 2, hx + 3):
            if (xx - hx) ** 2 + (yy - hy) ** 2 <= 6 and 0 <= xx < S and 0 <= yy < S:
                p[xx, yy] = (CREAM if (xx - hx) + (yy - hy) < 1 else (230, 210, 184)) + (255,)
                new.add((xx, yy))
    for x, y in list(new):
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (x + dx, y + dy)
            if q not in new and 0 <= q[0] < S and 0 <= q[1] < S and p[q][3] == 0:
                p[q] = O + (255,)
    return out


HANDS = {}


def pose(drop=0, left=None, right=None):
    """left/right: 팔 종류 이름(queen_arms.arm). 손 위치는 HANDS['L'/'R']에 남긴다."""
    base = Image.open(HERE.parent / "queen_boss.png").convert("RGBA")
    img = body_drop(base, WAIST, drop) if drop else base.copy()
    HANDS.clear()
    if left:
        img = remove_sleeve(img, -1)
        HANDS["L"] = paste_arm(img, left, SHOULDER_L, -1, drop)
    if right:
        img = remove_sleeve(img, +1)
        HANDS["R"] = paste_arm(img, right, SHOULDER_R, +1, drop)
    return img


def glint(img, color=WHITE):
    out = img.copy()
    p = out.load()
    for x in range(50, 70):
        for y in range(10, 34):
            c = p[x, y]
            if c[3] and c[2] > 170 and c[0] < 120:
                p[x, y] = color + (255,)
                return out
    return out


def dim(img, k=0.72):
    out = img.copy()
    p = out.load()
    for y in range(S):
        for x in range(S):
            c = p[x, y]
            if c[3] and c[:3] != O:
                p[x, y] = tuple(int(v * k) for v in c[:3]) + (255,)
    return out


# ---------------- 효과(넓은 칸, 여왕 1배 좌표) ----------------
CW, CH = 232, 176                 # 한 칸 크기
QX, QY = (CW - S) // 2, 18        # 칸 안 여왕 위치
GROUND = QY + 114


def cell():
    return blank(CW, CH)


def put_queen(c, q):
    c.alpha_composite(q, (QX, QY))


def sparks(c, cx, cy, radius, n, colors, seed, cross=True):
    rnd = random.Random(seed)
    p = c.load()
    for k in range(n):
        a = rnd.uniform(0, math.tau)
        r = rnd.uniform(radius * 0.4, radius)
        x, y = int(cx + math.cos(a) * r), int(cy + math.sin(a) * r * 0.8)
        col = rnd.choice(colors)
        pts = [(x, y)] + ([(x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)] if cross and k % 3 == 0 else [])
        for px_, py_ in pts:
            if 0 <= px_ < CW and 0 <= py_ < CH and p[px_, py_][3] == 0:
                p[px_, py_] = col + (255,)


def _put(c, x, y, col, a=255):
    if 0 <= x < c.width and 0 <= y < c.height:
        c.putpixel((x, y), col + (a,))


BEAM = [GOLD_L, GOLD_L, GOLD_M, GOLD, GOLD_D]   # 가운데 → 가장자리(흰 심지는 발사 순간 hot=True 때만)


def beam_h(c, x0, x1, cy, half=4, hot=False):
    """가로 금빛 선: 가운데 심지(발사 순간만 흰색), 바깥으로 금색. 가장자리 줄은 3px마다 끊겨 일렁인다."""
    lo, hi = min(x0, x1), max(x0, x1)
    for x in range(lo, hi + 1):
        for dy in range(-half, half + 1):
            band = min(len(BEAM) - 1, abs(dy) * (len(BEAM) - 1) // max(1, half))
            if abs(dy) == half and (x // 3) % 2:
                continue
            col = WHITE if hot and dy == 0 else BEAM[band]
            _put(c, x, cy + dy, col, 255 if band < 3 else 170)
        if x % 9 == 0:   # 결 사이 반짝이
            _put(c, x, cy - half - 2, GOLD_L); _put(c, x + 1, cy + half + 2, GOLD_M)
    start = x0 if x1 >= x0 else x0
    flare(c, start, cy, half + 3)


def beam_v(c, cx, y0, y1, half=4, hot=False):
    for y in range(y0, y1 + 1):
        for dx in range(-half, half + 1):
            band = min(len(BEAM) - 1, abs(dx) * (len(BEAM) - 1) // max(1, half))
            if abs(dx) == half and (y // 3) % 2:
                continue
            col = WHITE if hot and dx == 0 else BEAM[band]
            _put(c, cx + dx, y, col, 255 if band < 3 else 170)


def flare(c, cx, cy, r):
    """손끝/착지점 섬광: 십자 광선 + 마름모 빛."""
    for k in range(-r, r + 1):
        col = WHITE if abs(k) < r // 2 else GOLD_L
        _put(c, cx + k, cy, col); _put(c, cx, cy + k, col)
    for d in range(1, r // 2 + 1):
        for sx, sy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
            _put(c, cx + d * sx, cy + d * sy, GOLD_M)


def orb(c, cx, cy, r):
    """빛 구슬: 외곽선 없는 겹 원(가장자리 금 → 가운데 흰색) + 긴 십자 광선."""
    for y in range(cy - r - 1, cy + r + 2):
        for x in range(cx - r - 1, cx + r + 2):
            d = math.hypot(x - cx, y - cy)
            if d <= r + 0.5:
                col = WHITE if d < r * 0.45 else GOLD_L if d < r * 0.75 else GOLD_M if d < r else GOLD
                _put(c, x, y, col)
    for k in range(r + 2, r + 6):
        for sx, sy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            _put(c, cx + k * sx, cy + k * sy, GOLD_L if k < r + 4 else GOLD)
    for sx, sy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        _put(c, cx + (r + 2) * sx, cy + (r + 2) * sy, GOLD_M)


def floor_glow(c, cx, w, color=GOLD_M):
    """타격 지점 바닥: 금 테두리 빛판(위로 갈수록 좁아지는 사다리꼴) + 결."""
    y0 = GROUND - 2
    for i in range(10):
        y = y0 + i
        half = w - 8 + i
        for x in range(cx - half, cx + half + 1):
            edge = x in (cx - half, cx + half) or i in (0, 9)
            col = GOLD_D if edge else (GOLD_L if (x + i) % 10 < 2 else color)
            _put(c, x, y, col)
    flare(c, cx, y0 + 2, 9)


def wall(c, x, rise=1.0):
    """1칸 룩 성벽: 돌 몸통 + 위쪽 총안(톱니 3개) + 금 띠. rise로 땅에서 솟는 정도."""
    h = int(42 * rise)
    if h <= 0:
        return
    x0, x1, y1 = x - 11, x + 11, GROUND + 4
    y0 = y1 - h
    pix = {}
    for y in range(y0, y1 + 1):
        for xx in range(x0, x1 + 1):
            top = y < y0 + 5
            if top and ((xx - x0) // 5) % 2 == 1 and y0 + 5 - y > 0:   # 총안 홈
                continue
            col = STONE[1]
            if xx - x0 < 2:
                col = STONE[3]
            elif x1 - xx < 3:
                col = STONE[0]
            if not top and (y - y0) % 7 == 0:
                col = STONE[0]                                       # 돌줄
            if not top and (y - y0) % 7 != 0 and (xx - x0 + ((y - y0) // 7) * 5) % 11 == 0:
                col = STONE[0]                                       # 엇갈린 이음매
            if y == y0 + 6:
                col = GOLD_M if xx < x1 - 3 else GOLD
            pix[(xx, y)] = col
    for (xx, y), col in pix.items():
        _put(c, xx, y, col)
    for (xx, y) in pix:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            q = (xx + dx, y + dy)
            if q not in pix:
                _put(c, q[0], q[1], O)


def strike(c, cx, color, seed):
    """체크메이트 번개: 2~4px 계단으로 꺾이며 내려오는 3px 줄기(흰 심지) + 가지 하나 + 바닥 섬광."""
    rnd = random.Random(seed)
    light = color
    dark = tuple(v // 2 for v in color)

    def bolt(x, y, y_end, width):
        while y < y_end:
            step = rnd.randint(3, 6)
            dx = rnd.choice((-1, 1)) * rnd.randint(1, 3)
            for i in range(step):
                yy = y + i
                xx = x + (dx if i >= step // 2 else 0)
                for k in range(-width, width + 1):
                    _put(c, xx + k, yy, WHITE if k == 0 else light if abs(k) == 1 else color)
                for k in (-width - 1, width + 1):
                    _put(c, xx + k, yy, dark)
            x += dx
            y += step
        return x

    end_x = bolt(cx, 0, GROUND, 2)
    bolt(cx + rnd.choice((-8, 8)), 40, 70, 0)
    flare(c, end_x, GROUND, 8)
    sparks(c, end_x, GROUND, 14, 18, [color, WHITE, GOLD_L], seed + 1)


# ---------------- 칸(판정) 표시 ----------------
TILE = 32
FLOOR_Y = GROUND - 4          # 여왕이 선 줄의 바닥(정면 보기에서 한 줄 띠로 표시)


def tile_x(i):
    """여왕 칸 기준 i칸 옆 칸의 왼쪽 x."""
    return CW // 2 - TILE // 2 + i * TILE


def floor_tiles(c, cols, color, strength):
    """바닥 줄의 칸들을 색 테두리 + 사선으로 표시(strength 0~1: 예고=약하게, 발동=강하게)."""
    a_fill, a_line = int(70 * strength), int(110 + 145 * strength)
    for i in cols:
        x0 = tile_x(i) + 1
        for y in range(FLOOR_Y, FLOOR_Y + 12):
            for x in range(x0, x0 + TILE - 2):
                edge = y in (FLOOR_Y, FLOOR_Y + 11) or x in (x0, x0 + TILE - 3)
                hatch = (x + y) % 5 == 0
                if edge:
                    _put(c, x, y, color, a_line)
                elif hatch:
                    _put(c, x, y, color, a_fill + 40)
                elif a_fill:
                    _put(c, x, y, color, a_fill // 2)


ROW = range(-3, 4)            # 칸 하나 = 32px, 시트 칸 폭 200px → 여왕 좌우 3칸까지 보인다


# ---------------- 자세 보조 ----------------
def kneel(drop=6):
    """탈진: 상체가 망토 속으로 주저앉고(행 이동), 왕관이 오른쪽으로 기운다(위 행일수록 1px씩 더 밀기)."""
    base = Image.open(HERE.parent / "queen_boss.png").convert("RGBA")
    img = body_drop(base, WAIST + 6, drop)
    src = img.load()
    out = blank()
    p = out.load()
    crown_bottom = 30 + drop
    for y in range(S):
        dx = 0 if y >= crown_bottom else (1 if y >= crown_bottom - 10 else 2 if y >= crown_bottom - 18 else 3)
        for x in range(S):
            if src[x, y][3] and 0 <= x + dx < S:
                p[x + dx, y] = src[x, y]
    return dim(out, 0.82)


def lift(img, dy):
    """몸 전체를 위(-)/아래(+)로 정수 이동(떠오르기·반동)."""
    return shift(img, 0, dy)


def dizzy(c, cx, cy, f):
    """탈진 머리 위를 도는 작은 별 3개."""
    for k in range(3):
        a = f * 0.9 + k * math.tau / 3
        x, y = round(cx + 11 * math.cos(a)), round(cy + 3 * math.sin(a))
        col = GOLD_L if math.sin(a) > 0 else (150, 140, 120)
        for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
            _put(c, x + dx, y + dy, col if (dx, dy) == (0, 0) or math.sin(a) > 0 else (110, 104, 96))


FR_META = {}


def fr(q, dy=0, under=(), over=()):
    """한 칸: under 효과 → 여왕(dy만큼 이동) → over 효과."""
    c = cell()
    FR_META[id(c)] = (list(under), list(over))
    for fn in under:
        fn(c)
    put_queen(c, lift(q, dy) if dy else q)
    for fn in over:
        fn(c)
    return c


def H(side, dy=0):
    x, y = HANDS[side]
    return QX + x, QY + y + dy


# ---------------- 프레임 구성 ----------------
# 핵심 자세 8장은 생성 시안을 120px 여왕으로 옮긴 것(queen_keyposes.py, 머리는 기존 여왕 픽셀 이식).
# 손 위치(효과 기준점)는 120px 프레임 좌표로 직접 적어 둔다.
KEY_FILES = {
    "gather": "key_01_royal_lines_prepare.png", "spread": "key_02_royal_lines_release.png",
    "raise": "key_03_bombardment_prepare.png", "strike": "key_04_bombardment_strike.png",
    "command": "key_05_guard_command.png", "press": "key_06_fortress_ground_press.png",
    "float": "key_07_checkmate_float.png", "kneel": "key_08_exhausted_kneel.png",
}
KEY_HANDS = {
    "gather": {"C": (60, 60)}, "spread": {"L": (18, 47), "R": (105, 47)},
    "raise": {"L": (37, 15)}, "strike": {"L": (53, 85)},
    "command": {"R": (100, 47), "C": (50, 57)}, "press": {"L": (43, 107), "R": (75, 107)},
    "float": {"L": (37, 15), "R": (85, 15)}, "kneel": {"C": (60, 90)},
}


def key(name, mirror=False):
    img = Image.open(HERE / "keyposes" / KEY_FILES[name]).convert("RGBA")
    if mirror:
        from queen_keyposes import transplant_head
        img = transplant_head(img.transpose(Image.FLIP_LEFT_RIGHT))
    return img


def KH(name, side, dy=0, mirror=False):
    x, y = KEY_HANDS[name][side]
    if mirror:
        x = S - 1 - x
    return QX + x, QY + y + dy


def gimmicks():
    rows = []
    base = pose(0)
    idle_dip = body_drop(base, WAIST, 1)

    # 1 ROYAL LINES — 가슴 앞에 모음 → 십자로 활짝
    g, sp = key("gather"), key("spread")
    gc = KH("gather", "C")
    hl, hr = KH("spread", "L"), KH("spread", "R")
    lane = lambda s_: (lambda c: floor_tiles(c, ROW, GOLD_M, s_))
    frames = [
        fr(base),
        fr(g, 1, over=[lambda c: orb(c, gc[0], gc[1] + 1, 2)]),
        fr(g, -1, under=[lane(0.25)], over=[lambda c: orb(c, gc[0], gc[1] - 1, 4),
                                             lambda c: sparks(c, gc[0], gc[1], 14, 12, [GOLD_L, WHITE], 1)]),
        fr(sp, -1, under=[lane(0.5)], over=[lambda c: [flare(c, x, y - 1, 5) for x, y in (hl, hr)]]),
        fr(sp, 1, under=[lane(1.0), lambda c: beam_h(c, 0, hl[0] - 3, hl[1] + 1, 4, hot=True),
                         lambda c: beam_h(c, hr[0] + 3, CW, hr[1] + 1, 4, hot=True)],
           over=[lambda c: sparks(c, CW // 2, QY + 50, 90, 24, [GOLD_L, WHITE], 4, cross=False)]),
        fr(sp, under=[lane(0.6), lambda c: beam_h(c, 0, hl[0] - 3, hl[1], 2),
                      lambda c: beam_h(c, hr[0] + 3, CW, hr[1], 2)]),
        fr(idle_dip),
    ]
    bodies = [base, lift(g, 1), lift(g, -1), lift(sp, -1), lift(sp, 1), sp, idle_dip]
    rows.append(("royal_lines", bodies, frames,
                 ["IDLE", "GATHER", "CHARGE", "CROSS", "FIRE", "FADE", "RECOVER"],
                 [200, 160, 300, 260, 130, 160, 240]))

    # 2 BOMBARDMENT — 한 팔을 머리 위로 → 내리꽂음
    up, hit = key("raise"), key("strike")
    hu = KH("raise", "L")
    tgt = (-3, -2, -1)                          # 왼팔을 내리꽂으므로 화면 왼쪽 3칸
    tx = tile_x(-2) + TILE // 2
    target = lambda s_: (lambda c: floor_tiles(c, tgt, GOLD_M, s_))
    frames = [
        fr(base),
        fr(up, 1, over=[lambda c: orb(c, hu[0], hu[1] - 6, 2)]),
        fr(up, -1, under=[target(0.45)], over=[lambda c: orb(c, hu[0], hu[1] - 9, 6),
                                                 lambda c: sparks(c, hu[0], hu[1] - 9, 16, 10, [GOLD_L, WHITE], 6)]),
        fr(hit, 2, under=[target(1.0), lambda c: beam_v(c, tx, 0, GROUND + 4, 6, hot=True), lambda c: floor_glow(c, tx, 48)],
           over=[lambda c: sparks(c, tx, GROUND, 40, 34, [GOLD_L, WHITE, GOLD_M], 7)]),
        fr(hit, 1, under=[target(0.5), lambda c: beam_v(c, tx, 0, GROUND + 4, 2)],
           over=[lambda c: sparks(c, tx, GROUND - 6, 30, 16, [GOLD_M, GOLD], 8)]),
        fr(idle_dip),
    ]
    bodies = [base, lift(up, 1), lift(up, -1), lift(hit, 2), lift(hit, 1), idle_dip]
    rows.append(("bombardment", bodies, frames,
                 ["IDLE", "RAISE", "AIM 3x3", "STRIKE", "AFTER", "RECOVER"],
                 [200, 180, 340, 150, 200, 240]))

    # 3 GUARD ADVANCE — 한 손은 가슴, 한 팔은 옆을 가리킴
    cmd = key("command")
    hc = KH("command", "R")
    frames = [
        fr(base),
        fr(cmd, 1),
        fr(cmd, -1, over=[lambda c: flare(c, hc[0], hc[1], 6)],
           under=[lambda c: [beam_v(c, tile_x(i) + 16, 0, GROUND, 2) for i in (-2, 0, 2)]]),
        fr(cmd, under=[lambda c: [flare(c, tile_x(i) + 16, GROUND, 7) for i in (-2, 0, 2)]]),
        fr(cmd),
        fr(cmd),
        fr(base),
    ]
    bodies = [base, lift(cmd, 1), lift(cmd, -1), cmd, cmd, cmd, base]
    rows.append(("guard_advance", bodies, frames,
                 ["IDLE", "COMMAND", "SUMMON", "APPEAR", "ADVANCE", "ADVANCE 2", "REST"],
                 [200, 200, 180, 240, 200, 200, 240]))

    # 4 FORTRESS — 웅크려 두 손바닥으로 땅을 누름 → 벽이 솟고 벽 사이로 발사
    pr = key("press")
    hp = (KH("press", "L"), KH("press", "R"))
    wl, wr = tile_x(-3) + 16, tile_x(3) + 16          # 웅크린 망토(±2칸 가까이)보다 바깥
    walls = lambda r_: (lambda c: (wall(c, wl, r_), wall(c, wr, r_)))
    frames = [
        fr(base),
        fr(pr, -1),
        fr(pr, 1, under=[lambda c: floor_tiles(c, (-3, 3), GOLD_M, 0.6), walls(0.3)],
           over=[lambda c: [flare(c, x, y - 1, 4) for x, y in hp]]),
        fr(pr, under=[walls(0.75)], over=[lambda c: [sparks(c, x, GROUND - 6, 10, 10, [GOLD_L, WHITE], 9 + i) for i, x in enumerate((wl, wr))]]),
        fr(pr, under=[walls(1.0), lambda c: floor_tiles(c, (-2, -1, 1, 2), GOLD_M, 0.35)]),
        fr(pr, 1, under=[walls(1.0), lambda c: floor_tiles(c, (-2, -1, 1, 2), GOLD_M, 1.0),
                         lambda c: beam_h(c, wl + 12, CW // 2 - 26, GROUND - 32, 3, hot=True),
                         lambda c: beam_h(c, CW // 2 + 26, wr - 12, GROUND - 32, 3, hot=True)]),
        fr(idle_dip, under=[walls(1.0)]),
    ]
    bodies = [base, lift(pr, -1), lift(pr, 1), pr, pr, lift(pr, 1), idle_dip]
    rows.append(("fortress", bodies, frames,
                 ["IDLE", "CROUCH", "PRESS", "WALLS RISE", "HOLD", "FIRE", "RECOVER"],
                 [200, 140, 220, 160, 300, 130, 240]))

    # 5 CHECKMATE — 웅크림 → 양팔 들고 떠오름 → A·B 타격 → 무릎 꿇은 탈진
    fl, st = key("float"), key("strike")
    stb = key("strike", mirror=True)
    ko = key("kneel")
    safe_a, safe_b = (-2, 1), (-1, 2)
    zone_a = lambda s_: (lambda c: floor_tiles(c, safe_a, SAFE_A, s_))
    zone_b = lambda s_: (lambda c: floor_tiles(c, safe_b, SAFE_B, s_))
    outside = lambda safe: [i for i in ROW if i not in safe and i != 0]
    frames = [
        fr(base),
        fr(g, 2),
        fr(fl, -1, under=[zone_a(0.9), zone_b(0.9)],
           over=[lambda c: sparks(c, CW // 2, QY + 20, 60, 24, [GOLD_L, WHITE, SAFE_A, SAFE_B], 11)]),
        fr(fl, -3, under=[zone_a(0.7), zone_b(0.7)]),
        fr(st, 1, under=[zone_a(1.0), lambda c: [strike(c, tile_x(i) + 16, SAFE_A, 20 + i) for i in outside(safe_a)[::2]]]),
        fr(stb, 1, under=[zone_b(1.0), lambda c: [strike(c, tile_x(i) + 16, SAFE_B, 40 + i) for i in outside(safe_b)[1::2]]]),
        fr(ko, over=[lambda c: dizzy(c, CW // 2 + 3, QY + 22, 0)]),
        fr(ko, over=[lambda c: dizzy(c, CW // 2 + 3, QY + 22, 2)]),
    ]
    bodies = [base, lift(g, 2), lift(fl, -1), lift(fl, -3), lift(st, 1), lift(stb, 1), ko, ko]
    rows.append(("checkmate", bodies, frames,
                 ["IDLE", "CROUCH", "REVEAL A+B", "FLOAT", "STRIKE A", "STRIKE B", "EXHAUSTED", "EXHAUSTED 2"],
                 [200, 160, 380, 260, 150, 150, 440, 440]))
    return rows


def guards_for(name, frame_index):
    """근위병 64px 원본(2배 화면 기준). 소환 빛기둥 칸(-2, 0, +2)에서 나타나 아래(앞)로 전진."""
    if name != "guard_advance":
        return []
    idle = Image.open(HERE.parent / "64px" / "sheets" / "pawn_guard_idle.png").convert("RGBA")
    move = Image.open(HERE.parent / "64px" / "sheets" / "pawn_guard_move.png").convert("RGBA")
    g0 = idle.crop((0, 0, 64, 64))
    gm = [move.crop((64 * i, 0, 64 * i + 64, 64)) for i in range(4)]
    cols = [2 * (tile_x(i) + 16) - 32 for i in (-2, 0, 2)]
    g2 = 2 * GROUND
    if frame_index == 3:
        white = g0.copy()
        px = white.load()
        for y in range(64):
            for x in range(64):
                if px[x, y][3]:
                    px[x, y] = (255, 246, 214, 210)
        return [(white, x, g2 - 58 + (40 if i == 1 else 0)) for i, x in enumerate(cols)]
    if frame_index in (4, 5):
        step = 14 if frame_index == 4 else 30
        return [(gm[frame_index - 3], x, g2 - 58 + step + (40 if i == 1 else 0)) for i, x in enumerate(cols)]
    if frame_index == 6:
        return [(g0, x, g2 - 58 + 30 + (40 if i == 1 else 0)) for i, x in enumerate(cols)]
    return []


def arrows(img, name, frame_index):
    if name != "guard_advance" or frame_index not in (4, 5):
        return
    d = ImageDraw.Draw(img)
    for i, x in enumerate([2 * (tile_x(k) + 16) for k in (-2, 0, 2)]):
        gy = 2 * GROUND + 20 + (40 if i == 1 else 0) + (16 if frame_index == 5 else 0)
        if gy + 24 >= CH * 2:
            continue
        d.rectangle([x - 3, gy, x + 3, gy + 12], fill=GOLD_M + (255,), outline=O + (255,))
        d.polygon([(x - 9, gy + 12), (x + 9, gy + 12), (x, gy + 22)], fill=GOLD_L + (255,), outline=O + (255,))


def panel(frame, name, i):
    W2, H2 = CW * 2, CH * 2
    big = frame.resize((W2, H2), Image.NEAREST)
    for g, gx, gy in guards_for(name, i):
        big.alpha_composite(g, (gx, gy))
    arrows(big, name, i)
    out = Image.new("RGBA", (W2, H2), (18, 30, 42, 255))
    gd = ImageDraw.Draw(out)
    off = 2 * (tile_x(0) % TILE)                    # 격자를 여왕 칸에 맞춘다
    for gx in range(off, W2, 2 * TILE):
        gd.line([gx, 0, gx, H2], fill=(26, 42, 56, 255))
    for gy in range(2 * (FLOOR_Y % TILE), H2, 2 * TILE):
        gd.line([0, gy, W2, gy], fill=(26, 42, 56, 255))
    out.alpha_composite(big)
    return out


SMEAR_MS = 60


def smear(a, b):
    """두 자세 사이 잔상: 바뀐 픽셀만 바둑판 무늬로 섞는다(빠른 움직임의 도트 잔상). 같은 픽셀은 그대로."""
    out = blank()
    pa, pb, po = a.load(), b.load(), out.load()
    for y in range(S):
        for x in range(S):
            ca, cb = pa[x, y], pb[x, y]
            if ca == cb:
                po[x, y] = ca
            elif ca[3] and cb[3]:
                po[x, y] = cb if (x + y) % 2 == 0 else ca
            else:
                c = cb if cb[3] else ca
                po[x, y] = c if (x + y) % 2 == 0 else (0, 0, 0, 0)
    return out


def changed(a, b):
    """같은 자세를 위아래로 옮긴 것은 '바뀜'으로 보지 않는다: -4~4px 이동 중 가장 작은 차이."""
    best = None
    for dy in range(-4, 5):
        bb = shift(b, 0, dy)
        pa, pb = a.load(), bb.load()
        n = sum(1 for y in range(0, S, 2) for x in range(0, S, 2) if pa[x, y] != pb[x, y]) * 4
        best = n if best is None else min(best, n)
    return best


def add_smears(row):
    """자세가 크게 바뀌는(픽셀 400개 이상) 사이마다 60ms 잔상 프레임을 넣는다. 효과는 다음 프레임 것을 쓴다."""
    name, bodies, frames, labels, durs = row
    nb, nf, nl, nd, no = [], [], [], [], []
    for i in range(len(bodies)):
        nb.append(bodies[i]); nf.append(frames[i]); nl.append(labels[i]); nd.append(durs[i]); no.append(i)
        if i + 1 < len(bodies) and changed(bodies[i], bodies[i + 1]) >= 400:
            body = smear(bodies[i], bodies[i + 1])
            under, over = FR_META.get(id(frames[i + 1]), ([], []))
            c = cell()
            for fn in under:
                fn(c)
            c.alpha_composite(body, (QX, QY))
            nb.append(body); nf.append(c); nl.append("~"); nd.append(SMEAR_MS); no.append(i + 1)
    return name, nb, nf, nl, nd, no


def main():
    rows = [add_smears(r) for r in gimmicks()]
    for name, bodies, _, _, _, _ in rows:
        strip = blank(S * len(bodies), S)
        for i, b in enumerate(bodies):
            strip.alpha_composite(b, (i * S, 0))
        strip.save(HERE / f"queen_gimmick_{name}.png")

    pad, label_h = 10, 22
    W2, H2 = CW * 2, CH * 2
    cols = max(len(r[2]) for r in rows)
    sheet = Image.new("RGBA", (pad + cols * (W2 + pad), pad + len(rows) * (H2 + label_h + pad)), (12, 22, 32, 255))
    d = ImageDraw.Draw(sheet)
    for r, (name, _, frames, labels, durs, orig) in enumerate(rows):
        y = pad + r * (H2 + label_h + pad)
        d.text((pad, y + 2), f"{r + 1}. {name.upper().replace('_', ' ')}", fill=(120, 220, 240))
        imgs = []
        for i, f in enumerate(frames):
            x = pad + i * (W2 + pad)
            pnl = panel(f, name, orig[i])
            sheet.alpha_composite(pnl, (x, y + label_h))
            d.text((x + 8, y + label_h + H2 - 16), f"{labels[i]}  {durs[i]}ms", fill=(235, 235, 235))
            imgs.append(pnl.convert("RGB"))
        imgs[0].save(HERE / f"queen_gimmick_{name}.gif", save_all=True, append_images=imgs[1:], duration=durs, loop=0)
    sheet.save(HERE / "queen_gimmicks_sheet.png")
    sheet.resize((sheet.width // 2, sheet.height // 2), Image.NEAREST).save(HERE / "queen_gimmicks_sheet_half.png")


# 게임용 구간: 준비 클립은 예고 자세에서 멈추고, 발동 클립은 발사부터 대기 복귀까지 재생한다.
# 잔상(~)은 다음 프레임의 구간에 붙는다. (기믹, 구간, 시작 라벨, 끝 라벨)
SEGMENTS = [
    ("royal_lines", "windup", "IDLE", "CROSS"), ("royal_lines", "release", "FIRE", "RECOVER"),
    ("bombardment", "windup", "IDLE", "AIM 3x3"), ("bombardment", "release", "STRIKE", "RECOVER"),
    ("guard_advance", "windup", "IDLE", "APPEAR"), ("guard_advance", "release", "ADVANCE", "REST"),
    ("fortress", "windup", "IDLE", "HOLD"), ("fortress", "release", "FIRE", "RECOVER"),
    ("checkmate", "windup", "IDLE", "FLOAT"), ("checkmate", "strike_a", "STRIKE A", "STRIKE A"),
    ("checkmate", "strike_b", "STRIKE B", "STRIKE B"), ("checkmate", "exhausted", "EXHAUSTED", "EXHAUSTED 2"),
]


def export_game(out_dir):
    """Assets/Monsters/Queen/Lively/queen_gm_<기믹>_<구간>.png(120px 프레임 가로 시트)와 프레임 시간을 출력한다."""
    out_dir = Path(out_dir)
    rows = {r[0]: r for r in (add_smears(r) for r in gimmicks())}
    for name, seg, first, last in SEGMENTS:
        _, bodies, _, labels, durs, _ = rows[name]
        start = labels.index(first)
        while seg != "exhausted" and start > 0 and labels[start - 1] == "~":   # 앞 잔상은 이 구간 소속(반복 클립 제외)
            start -= 1
        end = labels.index(last)
        frames, ms = bodies[start:end + 1], durs[start:end + 1]
        if seg in ("windup",) and start == 0 and labels[0] == "IDLE":
            frames, ms = frames[1:], ms[1:]                 # 대기 프레임은 대기 상태가 맡는다
        strip = blank(S * len(frames), S)
        for i, f in enumerate(frames):
            strip.alpha_composite(f, (i * S, 0))
        strip.save(out_dir / f"queen_gm_{name}_{seg}.png")
        print(f"{name}_{seg}", ms)


if __name__ == "__main__":
    import sys as _sys
    if len(_sys.argv) > 2 and _sys.argv[1] == "--game":
        export_game(_sys.argv[2])
    else:
        main()
