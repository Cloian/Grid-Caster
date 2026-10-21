"""한 칸 위에 떠 있는 유물 보석(32×48, 칸당 32px 밀도) + 8프레임 대기 모션.

아래 32px이 그 칸. 바닥에는 숨 쉬듯 크기가 변하는 그림자와 희미한 빛 고리, 보석은 위아래로 떠다니고
작은 빛 조각이 보석 둘레를 돈다. 외곽선은 맵 타일과 같은 색.
보석 색 = 등급(빌드 창 글자색과 같은 계열: 일반 은백, 희귀 파랑, 전설 금).
위험 제단 = 보석에 금이 가고, 바닥 고리가 핏빛이며, 어두운 붉은 기운이 피어오른다.
사용: python3 relic_float_rarity.py OUT_DIR
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
FRAMES = 8
LIFT = [0, 1, 2, 3, 3, 2, 1, 0]
GROUND = 39  # 그림자·빛 고리 중심 줄


def frame(rarity, risky, f):
    G = GEMS[rarity]
    R = BLOOD if risky else G
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    p = img.load()

    def put(x, y, c, a=255):
        if 0 <= x < W and 0 <= y < H:
            p[x, y] = c + (a,)

    lift = LIFT[f]
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
        for xy in ((17, mid - 5), (18, mid - 4), (18, mid - 3), (19, mid - 2), (19, mid - 1), (18, mid), (18, mid + 1), (19, mid + 2), (19, mid + 3), (18, mid + 4)):
            gem[xy] = CRACK
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
