"""64px 체스 적(나이트·룩·폰 근위병) 모션 시트. 기본 포즈 한 장에서 픽셀을 늘리거나 돌리지 않고
행·열 단위 정수 이동만으로 만든다(도트가 깨지지 않게).

- 나이트: idle 4(숨쉬기·꼬리) / move 5(웅크림→박차기→공중→앞발 치켜든 하강) / attack 4(착지 내려찍기) / hit 4 / death 8
- 룩:     idle 4(눈빛 깜빡임·턱 들썩) / move 4(벽 속 들썩임) / attack 6(쇠뇌 당김→발사→재정비) / hit 4 / death 8
- 폰:     idle 4(숨쉬기) / move 6(짧은 발 교대 걸음) / attack 8(몸째 방패 밀치기) / hit 4 / death 8

실행: python3 chess64_motions.py <base_dir(knight.png,rook.png,pawn_guard.png)> <out_dir>
"""
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


def hit_frames(base, back):
    out_c = darkest(base)
    white = recolor(base, lambda c: c if c == out_c else WHITE)
    red = recolor(base, lambda c: c if c == out_c else RED)
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


if __name__ == "__main__":
    src, out = Path(sys.argv[1]), Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    for name, fn in (("knight", knight), ("rook", rook), ("pawn_guard", pawn)):
        base = Image.open(src / f"{name}.png").convert("RGBA")
        for clip, frames in fn(base).items():
            strip(frames).save(out / f"{name}_{clip}.png")
    print("saved", len(list(out.glob("*.png"))), "sheets")
