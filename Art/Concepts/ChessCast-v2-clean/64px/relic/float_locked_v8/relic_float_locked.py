"""한 칸 위에 떠 있는 유물 보석(32×48, 칸당 32px 밀도) + 8프레임 대기 모션.

아래 32px이 그 칸. 바닥에는 숨 쉬듯 크기가 변하는 그림자와 희미한 빛 고리, 보석은 위아래로 떠다니고
작은 빛 조각이 보석 둘레를 돈다. 외곽선은 맵 타일과 같은 색.
잠김(위험 제단 첫 상태): 보석이 어둡게 가라앉고 쇠사슬 두 줄이 X자로 감싸며 가운데 자물쇠가 달린다.
풀림: 자물쇠가 떨리고 빛난 뒤 사슬이 끊어져 흩어진다(6프레임, 한 번 재생).
보석 색 = 등급(빌드 창 글자색과 같은 계열: 일반 은백, 희귀 파랑, 전설 금).
위험 제단 = 보석에 금이 가고, 바닥 고리가 핏빛이며, 어두운 붉은 기운이 피어오른다.
사용: python3 relic_float_locked.py OUT_DIR
"""
import math
import sys
from PIL import Image

W, H = 32, 48
O = (30, 22, 34)
GEMS = {  # 어둠→밝음
    "common": [(60, 66, 84), (110, 122, 140), (170, 182, 198), (214, 224, 234), (250, 252, 255)],
    "rare": [(16, 40, 92), (30, 90, 170), (60, 150, 240), (140, 205, 255), (230, 245, 255)],
    "legendary": [(110, 60, 10), (180, 110, 20), (240, 170, 50), (255, 214, 110), (255, 248, 214)],
}
BLOOD = [(60, 8, 18), (100, 16, 30), (150, 28, 40), (200, 50, 56)]
CRACK = (84, 10, 22)
IRON = [(40, 34, 46), (74, 66, 80), (120, 112, 124), (170, 162, 170)]
# 위험 보석의 금: 오른쪽 위에서 내려오는 큰 금 + 왼쪽 면으로 뻗는 가지 두 개. 마지막 값이 True면 붉은 빛이 샌다
CRACKS = [(18, -7, 0), (18, -6, 0), (19, -5, 0), (19, -4, 0), (18, -3, 0), (18, -2, 0), (19, -1, 1), (19, 0, 0),
          (18, 1, 0), (18, 2, 0), (19, 3, 0), (19, 4, 0), (18, 5, 0), (18, 6, 0),
          (17, -2, 0), (16, -2, 0), (15, -1, 0), (14, -1, 0), (13, 0, 0),           # 가지 1
          (17, 3, 0), (16, 4, 1), (15, 4, 0), (14, 5, 0), (13, 6, 0),               # 가지 2
          (14, -5, 0), (13, -4, 0)]                                                 # 왼쪽 위 잔금
CHIP = [(21, -1), (21, 0), (20, 1), (21, 1)]   # 오른쪽 모서리가 떨어져 나간 자리
FRAMES = 8
LIFT = [0, 1, 2, 3, 3, 2, 1, 0]
GROUND = 39  # 그림자·빛 고리 중심 줄


def draw_lock(put, mid, f, unlock):
    """X자 쇠사슬 + 가운데 자물쇠. unlock 단계: 0 빛남, 1 떨림, 2 끊김, 3~5 조각이 흩어지며 사라짐."""
    shake = (1 if f % 2 else -1) if unlock == 1 else 0
    spread = max(0, unlock - 1) * 2
    alpha = 255 if unlock < 3 else [255, 255, 255, 200, 120, 50][unlock]
    for sx, sy, ex, ey in ((4, mid - 9, 27, mid + 8), (27, mid - 9, 4, mid + 8)):
        n = max(abs(ex - sx), abs(ey - sy))
        for i in range(n + 1):
            t = i / n
            x, y = round(sx + (ex - sx) * t), round(sy + (ey - sy) * t)
            if unlock >= 2 and 0.38 < t < 0.62:
                continue  # 가운데가 끊어진다
            push = (-spread if t < 0.5 else spread) if unlock >= 2 else 0
            x += push + shake
            y += (push // 2 if sx < ex else -push // 2) if unlock >= 2 else 0
            link = (i // 2) % 2
            put(x, y, IRON[3 if link else 1], alpha); put(x, y + 1, IRON[2 if link else 0], alpha)
            put(x, y - 1, O, alpha); put(x, y + 2, O, alpha)
    if unlock >= 2:
        if unlock <= 3:  # 끊어지는 순간 흰 섬광
            for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (2, 0), (-2, 0)):
                put(16 + dx, mid + 1 + dy, (255, 250, 230), 255 if unlock == 2 else 140)
        return
    cx, cy = 15 + shake, mid
    flash = unlock == 0
    for y in range(cy - 4, cy - 1):  # 고리
        for x in (cx - 1, cx + 2):
            put(x, y, IRON[3] if flash else IRON[2])
    put(cx, cy - 4, IRON[3]); put(cx + 1, cy - 4, IRON[2])
    for y in range(cy - 1, cy + 5):  # 몸통
        for x in range(cx - 2, cx + 4):
            c = IRON[3] if flash else IRON[2] if x < cx else IRON[1]
            if x in (cx - 2, cx + 3) or y in (cy - 1, cy + 4):
                c = O
            put(x, y, c)
    glow = BLOOD[3] if (f % 4 < 2 or flash) else BLOOD[2]
    put(cx, cy + 1, glow); put(cx + 1, cy + 1, glow); put(cx, cy + 2, glow)  # 붉게 빛나는 열쇠 구멍


def frame(rarity, risky, f, locked=False, unlock=-1):
    """locked: 잠긴 대기 상태. unlock>=0: 풀리는 연출의 몇 번째 프레임(0~5)."""
    G = GEMS[rarity]
    if locked or 0 <= unlock < 3:
        G = [G[0], G[0], G[1], G[2], G[3]]  # 잠긴 동안은 한 단계 어둡게 가라앉는다
    R = BLOOD if risky else G
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    p = img.load()

    def put(x, y, c, a=255):
        if 0 <= x < W and 0 <= y < H:
            p[x, y] = c + (a,)

    lift = LIFT[f] if not locked else [0, 0, 1, 1, 1, 1, 0, 0][f]
    # 바닥 빛 고리(점선, 천천히 돈다)
    for i in range(28):
        t = i / 28 * math.tau + f * math.tau / 56
        x, y = round(15.5 + 12 * math.cos(t)), round(GROUND + 3.5 * math.sin(t))
        if risky or (i + f) % 4 != 0:  # 위험 고리는 끊김 없이 이어진다
            put(x, y, R[3] if math.sin(t) > 0 else R[2], 255 if risky else 230 if math.sin(t) > 0 else 160)
    # 그림자: 보석이 높이 뜨면 작아진다
    rx, ry = 7 - lift // 2, 2
    for y in range(GROUND - ry, GROUND + ry + 1):
        for x in range(W):
            if ((x - 15.5) / rx) ** 2 + ((y - GROUND) / (ry + 0.5)) ** 2 <= 1:
                put(x, y, O, 110)
    # 보석
    # 위아래가 대칭인 마름모: 가운데 줄에서 폭 14, 위아래 끝으로 갈수록 일정하게 좁아진다
    top, mid, bot = 8 - lift, 19 - lift, 30 - lift
    gem = {}
    for y in range(top, bot + 1):
        half = max(1, round(7 * (1 - abs(y - mid) / (mid - top + 1))))
        for x in range(16 - half, 16 + half):
            left = x < 16
            c = (G[3] if left else G[2]) if y < mid else (G[2] if left else G[1])
            if x in (15, 16):
                c = G[4] if y < mid else G[3] if y < mid + 4 else G[2]
            if y == mid:
                c = G[3] if left else G[1]
            gem[(x, y)] = c
    for xy in ((13, mid - 3), (13, mid - 2), (14, mid - 4)):
        gem[xy] = G[4]
    if risky:
        bright = {(x, dy) for x, dy, _ in CRACKS}
        for x, dy, leak in CRACKS:
            if (x, mid + dy) in gem:
                gem[(x, mid + dy)] = (BLOOD[3] if f % 4 < 2 else BLOOD[2]) if leak else CRACK
            # 금 바로 위/왼쪽 면은 깨진 단면이라 밝게 빛을 받는다
            for hx, hy in ((x - 1, dy), (x, dy - 1)):
                if (hx, hy) not in bright and (hx, mid + hy) in gem and (hx + hy) % 2 == 0:
                    gem[(hx, mid + hy)] = G[4] if hy < 0 else G[3]
        for dx, dy in CHIP:
            gem.pop((dx, mid + dy), None)
    # 빛 조각 3개가 기울어진 타원으로 보석 둘레를 돈다(뒤쪽은 보석에 가려진다)
    orbit = []
    for k in range(3):
        t = f / FRAMES * math.tau + k * math.tau / 3
        x, y = round(15.5 + 10 * math.cos(t)), round(mid + 2 + 3 * math.sin(t))
        orbit.append((x, y, math.sin(t) > 0))
    if risky:
        for x0, y0, ph in ((9, 30, 0), (22, 28, 3), (12, 20, 5), (21, 16, 2), (8, 12, 6), (24, 9, 1)):
            t = (f + ph) % FRAMES
            if t < 5:
                put(x0 + (1 if t in (2, 3) else 0), y0 - lift - t, BLOOD[2] if t < 2 else BLOOD[1] if t < 4 else BLOOD[0],
                    230 if t < 3 else 150)
    for x, y, front in orbit:
        if not front:
            put(x, y, G[2])
    for (x, y), c in gem.items():
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            if (x + dx, y + dy) not in gem:
                put(x + dx, y + dy, O)
    for (x, y), c in gem.items():
        put(x, y, c)
    if risky and not locked:
        # 떨어져 나간 조각 두 개가 모서리 옆에 떠 있다
        for k, (sx, sy) in enumerate(((25, mid - 3), (25, mid + 3))):
            yy = sy + (1 if (f + k * 3) % 8 < 4 else 0)
            put(sx, yy, G[2]); put(sx + 1, yy, G[1]); put(sx, yy + 1, G[1])
            for ox, oy in ((sx - 1, yy), (sx + 2, yy), (sx, yy - 1), (sx + 1, yy - 1), (sx - 1, yy + 1), (sx + 1, yy + 1), (sx, yy + 2)):
                if p[ox, oy][3] == 0:
                    put(ox, oy, O)
    if locked or 0 <= unlock < 6:
        draw_lock(put, mid, f, unlock)
    if locked:
        return img
    for x, y, front in orbit:
        if front:
            put(x, y, G[4]); put(x - 1, y, G[3], 160); put(x + 1, y, G[3], 160)
    # 꼭대기 반짝임(가끔)
    if f in (2, 3):
        cx, cy = 19, top - 1
        put(cx, cy, G[4])
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            put(cx + dx, cy + dy, G[3] if f == 3 else G[2], 200)
    return img


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    for rarity in GEMS:
        for risky in (False, True):
            name = f"relic_{rarity}_{'risky' if risky else 'safe'}"
            fs = [frame(rarity, risky, f) for f in range(FRAMES)]
            fs[0].save(f"{out}/{name}.png")
            sheet = Image.new("RGBA", (W * FRAMES, H))
            for i, im in enumerate(fs):
                sheet.paste(im, (i * W, 0))
            sheet.save(f"{out}/{name}_idle.png")
        locked = [frame(rarity, True, f, locked=True) for f in range(FRAMES)]
        unlock = [frame(rarity, True, k % FRAMES, unlock=k) for k in range(6)]
        for label, fs in (("locked", locked), ("unlock", unlock)):
            sheet = Image.new("RGBA", (W * len(fs), H))
            for i, im in enumerate(fs):
                sheet.paste(im, (i * W, 0))
            sheet.save(f"{out}/relic_{rarity}_risky_{label}.png")
