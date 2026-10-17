"""64px 체스 적(나이트·룩·폰 근위병) 모션 시트. 기본 포즈 한 장에서 픽셀을 늘리거나 돌리지 않고
행·열 단위 정수 이동만으로 만든다(도트가 깨지지 않게).

- 나이트: idle 4(숨쉬기·꼬리) / move 5(웅크림→박차기→공중→앞발 치켜든 하강) / attack 4(착지 내려찍기) / hit 4 / death 8
- 룩:     idle 4(눈빛 깜빡임·턱 들썩) / move 4(벽 속 들썩임) / attack 6(쇠뇌 당김→발사→재정비) / hit 4 / death 8
- 폰:     idle 4(숨쉬기) / move 6(짧은 발 교대 걸음) / attack 8(몸째 방패 밀치기) / hit 4 / death 8
- 폭탄병: 근위병에서 방패를 걷어낸 폰. idle 4(심지 불꽃) / move 6 / attack 6(치켜들어 던짐, 4번째 = 발동) / hit 4 / death 8

실행: python3 chess64_motions.py <base_dir(knight.png,rook.png,pawn_guard.png)> <out_dir>
"""
import math
import random
import sys
from pathlib import Path
from PIL import Image

S = 64
WHITE = (255, 255, 255)
RED = (224, 36, 44)
ASH_D, ASH_M, ASH_L = (58, 52, 56), (96, 90, 92), (140, 134, 134)
DUST = (176, 160, 140)
SPARK, SPARK_L = (250, 122, 0), (240, 230, 33)


def blank():
    return Image.new("RGBA", (S, S))


def strip(frames):
    out = Image.new("RGBA", (S * len(frames), S))
    for i, f in enumerate(frames):
        out.alpha_composite(f, (i * S, 0))
    return out


def shift(img, dx=0, dy=0):
    out = blank()
    out.paste(img, (dx, dy), img)
    return out


def region_shift(img, test, dx=0, dy=0):
    """test(x,y)가 참인 픽셀을 (dx,dy)만큼 옮겨 나머지 위에 덮는다."""
    src = img.load()
    out = img.copy()
    px = out.load()
    moved = []
    for y in range(S):
        for x in range(S):
            if src[x, y][3] and test(x, y):
                moved.append((x + dx, y + dy, src[x, y]))
    for x, y, c in moved:
        if 0 <= x < S and 0 <= y < S:
            px[x, y] = c
    return out


def body_drop(img, cut, d):
    """cut 위쪽(몸통)을 d만큼 아래로 내린다. 다리(cut 아래)는 바닥에 그대로 → 웅크림/숨쉬기."""
    if d == 0:
        return img.copy()
    src = img.load()
    out = blank()
    px = out.load()
    for y in range(S):
        for x in range(S):
            if not src[x, y][3]:
                continue
            ny = y + d if y < cut else y
            if 0 <= ny < S:
                px[x, ny] = src[x, y]
    # 다리 위 겹친 부분: 몸통이 다리를 덮도록 한 번 더
    for y in range(cut - 1, -1, -1):
        for x in range(S):
            if src[x, y][3] and 0 <= y + d < S:
                px[x, y + d] = src[x, y]
    return out


def shear(img, k, cx=32, dy=0):
    """열마다 정수만큼 위아래로 밀어 몸을 기울인다(열 단위라 도트가 유지된다). k<0이면 오른쪽이 들린다."""
    src = img.load()
    out = blank()
    px = out.load()
    for x in range(S):
        off = int(round((x - cx) * k)) + dy
        for y in range(S):
            if src[x, y][3] and 0 <= y + off < S:
                px[x, y + off] = src[x, y]
    return out


def move_region(img, test, dx=0, dy=0, fill=None):
    """test 영역을 떼어 (dx,dy)로 옮긴다. 비워진 자리는 fill 색(없으면 투명)으로 채운다."""
    src = img.load()
    out = img.copy()
    px = out.load()
    cells = [(x, y, src[x, y]) for y in range(S) for x in range(S) if src[x, y][3] and test(x, y)]
    for x, y, _ in cells:
        px[x, y] = (fill + (255,)) if fill else (0, 0, 0, 0)
    for x, y, c in cells:
        if 0 <= x + dx < S and 0 <= y + dy < S:
            px[x + dx, y + dy] = c
    return out


def speed_lines(img, lines, color=None):
    """lines: (x0, x1, y) 가로 또는 (x, y0, y1, 'v') 세로 속도선. 이미 칠해진 칸은 건드리지 않는다."""
    out = img.copy()
    px = out.load()
    c = (color or DUST) + (255,)
    for ln in lines:
        pts = [(x, ln[2]) for x in range(ln[0], ln[1] + 1)] if len(ln) == 3 else [(ln[0], y) for y in range(ln[1], ln[2] + 1)]
        for x, y in pts:
            if 0 <= x < S and 0 <= y < S and not px[x, y][3]:
                px[x, y] = c
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
        if 0 <= x < S and 0 <= y < S:
            px[x, y] = color + (255,)
    return out


def dust_puff(img, x0, y, step, flip=False):
    """착지·내려찍기 먼지: step 0→2로 퍼진다."""
    pts = []
    for i in range(3 + step):
        dx = 2 + i * 2
        pts += [(x0 - dx, y - (i % 2)), (x0 + dx, y - ((i + 1) % 2))]
    return dots(img, pts, DUST)


def is_dark(c):
    """외곽선·그림자 얼굴처럼 아주 어두운 색. 번쩍임에서도 남겨 실루엣이 무너지지 않게 한다."""
    return sum(c) < 90


def hit_frames(base, back):
    white = recolor(base, lambda c: c if is_dark(c) else WHITE)
    red = recolor(base, lambda c: c if is_dark(c) else RED)
    return [white, red, shift(base, back, 0), base.copy()]


def death_frames(base, collapse):
    """collapse(img, t): 쓰러지는 자세. 그다음 재로 바뀌어 흩어진다."""
    out_c = darkest(base)
    pose = [collapse(base, t) for t in (1, 2, 3)]
    lum = lambda c: sum(c) / 3
    ash = recolor(pose[-1], lambda c: c if c == out_c else ASH_L if lum(c) > 150 else ASH_M if lum(c) > 80 else ASH_D)
    frames = [hit_frames(base, 0)[0]] + pose + [ash]
    rnd = random.Random(11)
    for keep in (0.55, 0.25, 0.06):
        img = ash.copy()
        px = img.load()
        for y in range(S):
            for x in range(S):
                if px[x, y][3] and rnd.random() > keep:
                    px[x, y] = (0, 0, 0, 0)
        frames.append(shift(img, 0, 1))
    return frames[:8]


# ---------------- 나이트 ----------------
def knight(base):
    CUT = 46  # 다리 시작
    tail = lambda x, y: x <= 12 and y < CUT
    idle = [base.copy(), body_drop(base, CUT, 1), region_shift(body_drop(base, CUT, 1), tail, 0, 1), base.copy()]
    # 도약: 웅크림 → 앞이 들리며 뜀 → 최고점 → 앞이 숙여지며 내려옴 → 착지 웅크림 + 먼지 → 복귀
    legs = lambda x, y: y >= CUT
    back_legs = lambda x, y: y >= CUT and x < 32
    front_legs = lambda x, y: y >= CUT and x >= 32

    def stretch(img):  # 뒷다리는 뒤로, 앞다리는 앞으로 뻗는다
        return move_region(move_region(img, back_legs, -2, -1), front_legs, 2, -2)

    def tuck(img, d):  # 다리를 몸 쪽으로 접는다(몸통이 다리를 덮음)
        body = move_region(img, legs, 0, 0)
        out = move_region(img, lambda x, y: y < CUT, 0, 0)
        out = move_region(out, legs, 0, -d)
        out.alpha_composite(body)
        return out

    trail = lambda img, y0: speed_lines(img, [(0, 5, y0), (2, 8, y0 + 6), (0, 4, y0 + 12)])
    # 이동(도약, 게임에서 0.34초 포물선과 함께 재생): 웅크림 → 박차기 → 상승 → 최고점 → 앞발을 치켜든 채 하강
    move = [
        body_drop(base, CUT, 3),
        dust_puff(shear(stretch(base), -0.16, 32, -3), 14, 62, 0),
        trail(shear(tuck(base, 3), -0.08, 32, -7), 22),
        trail(shear(tuck(base, 4), 0.0, 32, -8), 20),
        trail(shear(base, -0.18, 18, -3), 24),
    ]
    # 공격(착지 순간 시작): 앞발 내려찍기 + 충격 → 먼지 → 찌그러짐에서 복귀. 피해와 십자 이펙트는 첫 프레임.
    attack = [
        dots(dust_puff(shear(body_drop(base, CUT, 3), 0.05, 18, 0), 52, 62, 2), [(58, 56), (60, 54), (61, 58), (56, 53)], SPARK_L),
        dust_puff(body_drop(base, CUT, 2), 40, 62, 2),
        dust_puff(body_drop(base, CUT, 1), 40, 62, 1),
        base.copy(),
    ]
    death = death_frames(base, lambda b, t: shear(body_drop(b, CUT, 2 * t), 0.04 * t, 20, 0))
    return dict(idle=idle, move=move, attack=attack, hit=hit_frames(base, -2), death=death)


# ---------------- 룩 ----------------
def rook(base):
    src = base.load()
    eyes = {(x, y) for y in range(S) for x in range(S) if src[x, y][3] and src[x, y][:3][0] > 200 and src[x, y][:3][1] < 140}
    hot = lambda img: dots(img, [p for p in eyes if (p[0] + p[1]) % 3 == 0], SPARK_L)
    dim = lambda img: dots(img, list(eyes), (150, 70, 30))
    JAW = 44  # 아래턱(쇠뇌 아래) 시작 행

    def gape(img, d):
        """아래턱을 d만큼 내려 입을 벌린다. 벌어진 틈은 입 속 색(외곽선 색)으로 채운다."""
        if d == 0:
            return img.copy()
        out_c = darkest(img)
        s = img.load()
        out = img.copy()
        px = out.load()
        for y in range(S - 1, JAW - 1, -1):
            for x in range(S):
                if s[x, y][3] and y + d < S:
                    px[x, y + d] = s[x, y]
        for y in range(JAW, JAW + d):
            for x in range(S):
                if s[x, y][3] and 12 <= x <= 51:
                    px[x, y] = out_c + (255,)
        return out

    idle = [base.copy(), hot(base), gape(hot(base), 1), dim(base)]
    move = [base.copy(), shift(base, -1, 0), shift(base, 0, -1), shift(base, 1, 0)]
    cx = 32
    bolt = lambda img, y0, n: dots(img, [(cx, y) for y in range(y0, y0 + n)] + [(cx - 1, y0 + n - 1), (cx + 1, y0 + n - 1)], WHITE)
    attack = [
        gape(base, 1),
        hot(gape(shift(base, 0, -1), 2)),                                         # 시위 당김
        hot(gape(shift(base, 0, -1), 2)),
        dots(bolt(gape(shift(base, 0, 1), 2), 50, 12), [(cx - 2, 50), (cx + 2, 50), (cx, 48)], SPARK_L),  # 발사
        gape(base, 1),
        base.copy(),
    ]

    def crumble(b, t):
        # 위에서부터 돌이 무너져 내린다: 위쪽 행을 t에 비례해 아래로 압축
        # 갓돌이 내려앉고(위쪽 행 압축) 몸이 한쪽으로 기운다
        return shear(body_drop(b, 40, 3 * t), 0.03 * t, 32, 0)

    hit = hit_frames(base, 0)
    hit[2] = shift(base, 0, -2)  # 벽에 박혀 있어 옆이 아니라 위로 들썩인다
    return dict(idle=idle, move=move, attack=attack, hit=hit, death=death_frames(base, crumble))


# ---------------- 폰 근위병 ----------------
def pawn(base):
    bb = base.getbbox()
    FEET = bb[3] - 7
    mid = (bb[0] + bb[2]) // 2
    left = lambda x, y: y >= FEET and x < mid
    right = lambda x, y: y >= FEET and x >= mid

    def step(img, foot, lift):
        # 한 발을 lift만큼 든다
        out = img.copy()
        s = img.load()
        px = out.load()
        for y in range(S):
            for x in range(S):
                if s[x, y][3] and foot(x, y):
                    px[x, y] = (0, 0, 0, 0)
        for y in range(S):
            for x in range(S):
                if s[x, y][3] and foot(x, y) and y - lift >= 0:
                    px[x, y - lift] = s[x, y]
        return out

    idle = [base.copy(), body_drop(base, FEET, 1), body_drop(base, FEET, 1), base.copy()]
    move = [
        step(shift(base, 0, -1), left, 2), step(shift(base, 0, -2), left, 3), body_drop(base, FEET, 1),
        step(shift(base, 0, -1), right, 2), step(shift(base, 0, -2), right, 3), body_drop(base, FEET, 1),
    ]
    # 방패 = 금테 직사각형(외곽선 포함)
    gold = {c[:3] for c in base.getdata() if c[3] and c[0] > 230 and 150 < c[1] < 200}
    gpx = [(x, y) for y in range(S) for x in range(S) if base.getpixel((x, y))[:3] in gold and y > bb[1] + 16]
    sx0, sx1 = min(x for x, _ in gpx) - 1, max(x for x, _ in gpx) + 1
    sy0, sy1 = min(y for _, y in gpx) - 1, max(y for _, y in gpx) + 1
    shadow = darkest(base)

    def push(img, d, off=0):
        """방패(off만큼 이미 내려간 상태)를 몸에서 d만큼 더 민다. 틈이 생기지 않게 d는 1~2px로 둔다."""
        return move_region(img, lambda x, y: sx0 <= x <= sx1 and sy0 + off <= y <= sy1 + off, 0, d, shadow)

    lunge = lambda d: body_drop(base, FEET, d)  # 발은 땅에 두고 몸 전체가 앞으로 쏠린다

    def flash_rim(img, d):  # 방패 금테(가장자리 2px)만 하얗게 번쩍. d = 방패가 내려간 거리
        out = img.copy()
        px = out.load()
        for y in range(sy0 + d, sy1 + d + 1):
            for x in range(sx0, sx1 + 1):
                edge = min(x - sx0, sx1 - x, y - sy0 - d, sy1 + d - y) <= 2
                if edge and 0 <= y < S and px[x, y][3] and px[x, y][:3] in gold:
                    px[x, y] = WHITE + (255,)
        return out

    def burst(img, d, n):  # 방패 아래 두 모서리에서 바깥·아래로 튀는 충격선 n칸
        pts = []
        by = sy1 + d - 2
        for i in range(1, n + 1):
            pts += [(sx0 - 1 - i, by + i // 2), (sx1 + 1 + i, by + i // 2)]
            pts += [(sx0 - 1 - i, by - 3 - i), (sx1 + 1 + i, by - 3 - i)]
        return _burst(img, pts)

    def _burst(img, pts):
        out = img.copy()
        px = out.load()
        for x, y in pts:
            if 0 <= x < S and 0 <= y < S and not px[x, y][3]:
                px[x, y] = WHITE + (255,)
        return out

    rise = lambda img: speed_lines(img, [(sx0 - 3, sy0, sy0 + 8, 'v'), (sx1 + 3, sy0, sy0 + 8, 'v'),
                                         (sx0 - 5, sy0 + 4, sy0 + 12, 'v'), (sx1 + 5, sy0 + 4, sy0 + 12, 'v')])
    attack = [
        body_drop(base, FEET, 1),                                                 # 자세 낮춤
        push(shift(base, 0, -2), -1, -2),                                         # 몸을 젖히며 방패를 끌어당김
        dots(push(shift(base, 0, -2), -1, -2), [(sx0 + 1, sy0 - 2), (sx0 + 2, sy0 - 2), (sx0 + 1, sy0 - 1)], WHITE),  # 장전 반짝
        burst(rise(push(lunge(3), 1, 3)), 4, 3),                                  # 몸째 확 밀치기(발사)
        burst(flash_rim(push(lunge(3), 1, 3), 4), 4, 5),                          # 충격: 테두리 번쩍 + 큰 충격선
        push(lunge(2), 1, 2),                                                     # 버팀
        lunge(1),
        base.copy(),
    ]
    death = death_frames(base, lambda b, t: body_drop(b, FEET, 2 * t))
    hit = hit_frames(base, 0)
    hit[2] = shift(base, 0, -2)  # 정면을 보고 있어 뒤(위)로 밀린다
    return dict(idle=idle, move=move, attack=attack, hit=hit, death=death)


# ---------------- 폭탄병(근위병 그림에서 방패를 걷어내고 폭탄을 든 폰) ----------------
def bomber_parts(guard):
    """근위병에서 방패를 지우고 망토 몸통·벨트를 그린 몸, 그리고 따로 움직일 폭탄 레이어를 만든다."""
    pal = {c[:3] for c in guard.getdata() if c[3]}
    by = lambda rgb: min(pal, key=lambda c: sum((a - b) ** 2 for a, b in zip(c, rgb)))
    OUT, CAPE_D, CAPE, CAPE_L = by((7, 19, 27)), by((4, 42, 50)), by((6, 69, 77)), by((12, 99, 109))
    IVORY, IVORY_D, GOLD, GOLD_D, IRON, BROWN = (by((243, 238, 221)), by((221, 213, 184)), by((242, 183, 66)),
                                                 by((206, 144, 41)), by((98, 110, 117)), by((157, 102, 29)))
    body = guard.copy()
    px = body.load()
    x0, x1, y0, y1 = 21, 43, 24, 54
    # 1) 방패 자리는 뒤쪽 망토(어두운 청록)로 덮는다.
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if px[x, y][3]:
                px[x, y] = (OUT if x in (x0, x1) or y == y1 else CAPE_D) + (255,)
    # 2) 앞쪽 몸통: 위가 좁고 아래가 넓은 로브(가운데 상아 튜닉, 금색 허리띠)
    for y in range(y0, y1):
        t = (y - y0) / (y1 - 1 - y0)
        left, right = int(round(26 - 3 * t)), int(round(38 + 3 * t))
        for x in range(left, right + 1):
            if x in (left, right) or y == y0:
                c = OUT
            else:
                u = (x - left) / max(1, right - left)
                c = CAPE_L if u < 0.25 else CAPE if u < 0.7 else CAPE_D
                if 31 <= x <= 33:
                    c = IVORY if x < 33 else IVORY_D
                if y in (46, 47):
                    c = GOLD if y == 46 else GOLD_D
                    if 31 <= x <= 33:
                        c = IVORY
            px[x, y] = c + (255,)
    # 3) 소매와 양손(폭탄을 잡는 손)
    for side, hx in ((-1, 24), (1, 38)):
        for y in range(26, 34):
            x = (26 if side < 0 else 38) + side * ((y - 26) // 4)
            px[x, y] = CAPE_D + (255,)
        for y in range(34, 39):
            for x in range(hx, hx + 3):
                px[x, y] = (IVORY if y < 37 else IVORY_D) + (255,)
    bomb = Image.new("RGBA", (S, S))
    bp = bomb.load()
    cx, cy, r = 32, 36, 6.5
    for y in range(S):
        for x in range(S):
            d = ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2) ** 0.5
            if d <= r:
                lit = (x + 0.5 - cx) + (y + 0.5 - cy)
                c = IVORY_D if lit < -6 else IRON if lit < -1 else OUT if d > r - 1.2 else CAPE_D
                bp[x, y] = c + (255,)
            elif d <= r + 1:
                bp[x, y] = OUT + (255,)
    for i, (x, y) in enumerate(((36, 29), (37, 28), (38, 27), (39, 26))):   # 심지
        bp[x, y] = BROWN + (255,)
    spark = [(40, 25), (41, 24), (40, 23), (39, 24), (41, 26)]
    return body, bomb, spark, (GOLD, IVORY)


def bomber(guard):
    body, bomb, spark, (gold, ivory) = bomber_parts(guard)
    bb = body.getbbox()
    FEET = bb[3] - 7
    mid = (bb[0] + bb[2]) // 2

    def compose(b, bomb_dy=0, lit=0, holding=True):
        out = b.copy()
        if holding:
            layer = shift(bomb, 0, bomb_dy)
            out.alpha_composite(layer)
            pts = [(x, y + bomb_dy) for x, y in spark]
            out = dots(out, pts[: 2 + lit], gold if lit % 2 == 0 else ivory)
        return out

    left = lambda x, y: y >= FEET and x < mid
    right = lambda x, y: y >= FEET and x >= mid

    def step(img, foot, lift):
        out = img.copy()
        s_ = img.load()
        px = out.load()
        for y in range(S):
            for x in range(S):
                if s_[x, y][3] and foot(x, y):
                    px[x, y] = (0, 0, 0, 0)
        for y in range(S):
            for x in range(S):
                if s_[x, y][3] and foot(x, y) and y - lift >= 0:
                    px[x, y - lift] = s_[x, y]
        return out

    base = compose(body)
    idle = [base, compose(body_drop(body, FEET, 1), 1, 1), compose(body_drop(body, FEET, 1), 1, 2), compose(body, 0, 1)]
    move = [step(compose(shift(body, 0, -1), -1), left, 2), step(compose(shift(body, 0, -2), -2), left, 3),
            compose(body_drop(body, FEET, 1), 1),
            step(compose(shift(body, 0, -1), -1), right, 2), step(compose(shift(body, 0, -2), -2), right, 3),
            compose(body_drop(body, FEET, 1), 1)]
    pal = {c[:3] for c in body.getdata() if c[3]}
    cape = min(pal, key=lambda c: sum((a - b) ** 2 for a, b in zip(c, (6, 69, 77))))
    hands = lambda x, y: 34 <= y <= 38 and (24 <= x <= 26 or 38 <= x <= 40)
    raised = move_region(body, hands, 0, -10, cape)
    attack = [
        compose(body_drop(body, FEET, 1), 1),                                     # 자세 낮춤
        compose(move_region(body, hands, 0, -5, cape), -6, 2),
        compose(raised, -11, 3),                                                  # 머리 위로 치켜듦
        speed_lines(compose(raised, 0, 0, holding=False), [(30, 4, 12, 'v'), (34, 2, 10, 'v')], ivory),  # 던짐(발동)
        compose(body, 0, 0, holding=False),
        base,
    ]
    out_c = darkest(base)
    hit = [recolor(base, lambda c: c if c == out_c else WHITE), recolor(base, lambda c: c if c == out_c else RED),
           shift(base, 0, -2), base.copy()]
    death = death_frames(base, lambda b, t: body_drop(b, FEET, 2 * t))
    return dict(idle=idle, move=move, attack=attack, hit=hit, death=death), base


# ---------------- 향로 사제(비숍) · 시안 기반 ----------------
EMBER, EMBER_L, SMOKE = (250, 122, 0), (240, 230, 33), (176, 168, 160)


FLAME_R = (214, 64, 24)


def censer_fire(img, seed, strength=1):
    """향로(왼쪽 아래 금색 덩어리)를 프레임마다 찾아 위에 일렁이는 불꽃, 구멍 불빛, 날아오르는 불씨를 그린다."""
    px = img.load()
    gold = [(x, y) for y in range(38, S) for x in range(0, 25)
            if px[x, y][3] and px[x, y][0] > 200 and 120 < px[x, y][1] < 200 and px[x, y][2] < 110]
    if not gold:
        return img
    # 사슬 고리(한 줄 1~2칸)는 빼고, 금색이 한 줄에 3칸 이상인 행부터 향로 몸통으로 본다.
    rows = {}
    for x, y in gold:
        rows.setdefault(y, []).append(x)
    body_rows = sorted(y for y, xs in rows.items() if len(xs) >= 3)
    if not body_rows:
        return img
    top, bottom = body_rows[0], body_rows[-1]
    body = [x for y in body_rows for x in rows[y]]
    x0, x1 = min(body), max(body)
    cx = (x0 + x1) // 2
    out = img.copy()
    p = out.load()
    rnd = random.Random(seed)

    def put(x, y, c, only_empty=True):
        if 0 <= x < S and 0 <= y < S and (not only_empty or not p[x, y][3]):
            p[x, y] = c + (255,)

    # 1) 향로 구멍 불빛: 향로 가운데 높이의 어두운 칸을 주황·노랑으로
    mid = (top + bottom) // 2
    for x in range(x0 + 1, x1):
        for y in (mid, mid + 1):
            if p[x, y][3] and sum(p[x, y][:3]) < 200:
                put(x, y, EMBER_L if (x + seed) % 3 == 0 else EMBER, only_empty=False)
    # 2) 뚜껑 위 불꽃: 아래가 넓고 위가 뾰족한 물방울. 바깥 붉은 주황 → 주황 → 흰빛 노랑 심지로 금색 향로와 구분한다.
    CORE = (255, 246, 190)
    height = 5 + (seed % 3) + strength
    sway = (-1, 0, 1, 0)[seed % 4]
    for h in range(height):
        y = top - 1 - h
        t = h / max(1, height - 1)
        half = 2 if t < 0.35 else 1 if t < 0.75 else 0
        shift_x = sway if t >= 0.5 else 0
        for dx in range(-half, half + 1):
            edge = abs(dx) == half and half > 0
            if t >= 0.85:
                c = FLAME_R
            elif edge:
                c = FLAME_R
            elif abs(dx) == 0 and t < 0.6:
                c = CORE
            else:
                c = EMBER
            put(cx + dx + shift_x, y, c, only_empty=False)
    # 3) 날아오르는 불씨: 향로 위·옆에서 위로 흩어진다
    for k in range(4 + 2 * strength):
        x = cx + rnd.randint(-6, 6)
        y = top - rnd.randint(3, 12 + 4 * strength)
        put(x, y, rnd.choice((EMBER, EMBER_L, EMBER_L, FLAME_R)))
    return out


def priest(base):
    """향로(x12~23, y35~56)만 따로 흔든다. 향로는 늘 타오르고(불꽃·구멍 불빛·날리는 불씨),
    이동 중에는 향로 아래로 불씨가 떨어진다(지나간 길의 불길)."""
    censer = lambda x, y: x <= 23 and 35 <= y <= 56
    bb = base.getbbox()
    FEET = bb[3] - 4

    def swing(img, dx, dy=0):
        return move_region(img, censer, dx, dy)

    def smoke(img, phase):
        pts = [(16 + (phase % 2), 33 - phase), (18, 31 - phase), (15, 30 - phase)]
        return dots(img, pts[: 1 + phase % 3], SMOKE)

    def embers(img, dx, n):
        pts = [(17 + dx, 58), (15 + dx, 60), (19 + dx, 61), (16 + dx, 62), (18 + dx, 59)][:n]
        return dots(dots(img, pts[::2], EMBER), pts[1::2], EMBER_L)

    idle = [smoke(swing(base, 0), 0), smoke(swing(body_drop(base, FEET, 1), -1), 1),
            smoke(swing(base, 0), 2), smoke(swing(body_drop(base, FEET, 1), 1), 1)]
    move = [embers(swing(shift(base, 0, -1), -2, -1), -2, 2), embers(swing(shift(base, 0, -2), -1, -2), -1, 4),
            embers(swing(shift(base, 0, -1), 1, -1), 1, 3), embers(swing(base, 2), 2, 2),
            embers(swing(shift(base, 0, -1), 1, -1), 1, 4), embers(swing(shift(base, 0, -2), -1, -2), -1, 3)]

    def burst(img, cx, cy, r, color):
        pts = []
        for k in range(10):
            a = 2 * math.pi * k / 10
            pts.append((int(round(cx + math.cos(a) * r)), int(round(cy + math.sin(a) * r * 0.8))))
        return dots(img, pts, color)

    attack = [
        swing(body_drop(base, FEET, 1), 1, 1),
        swing(base, 2, -3),
        burst(swing(shift(base, 0, -1), 3, -6), 20, 40, 5, EMBER_L),        # 치켜들어 불씨를 터뜨림(발동)
        burst(burst(swing(base, 2, -4), 20, 42, 7, EMBER), 20, 42, 9, EMBER_L),
        swing(base, 1, -1),
        base.copy(),
    ]
    # 향로는 늘 타오른다. 이동·공격 때는 불꽃과 불씨가 더 세다.
    idle = [censer_fire(f, 11 + i, 1) for i, f in enumerate(idle)]
    move = [censer_fire(f, 31 + i, 2) for i, f in enumerate(move)]
    attack = [censer_fire(f, 51 + i, 2 if i in (2, 3) else 1) for i, f in enumerate(attack)]
    hit = hit_frames(base, -2)

    def collapse(b, t):
        dropped = move_region(b, censer, -1, min(5, 2 * t))
        return body_drop(dropped, FEET, 2 * t)

    death = death_frames(base, collapse)
    return dict(idle=idle, move=move, attack=attack, hit=hit, death=death)


# ---------------- 폭탄병 · 시안 기반(폭탄 x30~43, y28~42) ----------------
def bomber_ref(base):
    pal = {c[:3] for c in base.getdata() if c[3]}
    near = lambda rgb: min(pal, key=lambda c: sum((a - b) ** 2 for a, b in zip(c, rgb)))
    bomb_colors = {near((39, 49, 57)), near((118, 129, 144)), near((21, 30, 37)), near((52, 60, 65))}
    src = base.load()
    bomb = lambda x, y: 30 <= x <= 43 and 28 <= y <= 42 and src[x, y][:3] in bomb_colors
    torso = near((3, 72, 93))
    bb = base.getbbox()
    FEET = bb[3] - 7
    mid = (bb[0] + bb[2]) // 2
    spark = [(37, 27), (38, 26), (36, 26)]

    def lit(img, k):
        return dots(img, spark[: 1 + k % 3], EMBER_L if k % 2 else EMBER)

    def step(img, left, lift):
        foot = (lambda x, y: y >= FEET and x < mid) if left else (lambda x, y: y >= FEET and x >= mid)
        return move_region(img, foot, 0, -lift)

    def lift(img, dy, dx=0):
        return move_region(img, bomb, dx, -dy, torso)

    def throw_lines(img):
        return speed_lines(img, [(44, 46, 20), (45, 48, 24), (46, 49, 28)], WHITE)

    no_bomb = move_region(base, bomb, 0, -200, torso)   # 캔버스 밖으로 보내 지운다
    idle = [lit(base, 0), lit(body_drop(base, FEET, 1), 1), lit(body_drop(base, FEET, 1), 2), lit(base, 1)]
    move = [step(shift(base, 0, -1), True, 2), step(shift(base, 0, -2), True, 3), body_drop(base, FEET, 1),
            step(shift(base, 0, -1), False, 2), step(shift(base, 0, -2), False, 3), body_drop(base, FEET, 1)]
    attack = [
        body_drop(base, FEET, 1),
        lit(lift(base, 4), 1),
        lit(lift(shift(base, 0, -1), 8, 2), 2),
        throw_lines(shift(no_bomb, 0, 1)),                                     # 던짐(발동)
        no_bomb,
        base.copy(),
    ]
    hit = hit_frames(base, 0)
    hit[2] = shift(base, 0, -2)
    death = death_frames(base, lambda b, t: body_drop(b, FEET, 2 * t))
    return dict(idle=idle, move=move, attack=attack, hit=hit, death=death)


if __name__ == "__main__":
    src, out = Path(sys.argv[1]), Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    for name, fn in (("knight", knight), ("rook", rook), ("pawn_guard", pawn)):
        base = Image.open(src / f"{name}.png").convert("RGBA")
        for clip, frames in fn(base).items():
            strip(frames).save(out / f"{name}_{clip}.png")
    # 시안 기반 폭탄병·향로 사제(사용자 제공 시안을 ref_grid_to_sprite.py로 64px 변환)
    if (src / "bomber_ref.png").exists():
        base = Image.open(src / "bomber_ref.png").convert("RGBA")
        base.save(src / "bomber.png")
        for clip, frames in bomber_ref(base).items():
            strip(frames).save(out / f"bomber_{clip}.png")
    if (src / "bishop_priest.png").exists():
        for clip, frames in priest(Image.open(src / "bishop_priest.png").convert("RGBA")).items():
            strip(frames).save(out / f"bishop_priest_{clip}.png")
    print("saved", len(list(out.glob("*.png"))), "sheets")
