"""한 칸 위에 떠 있는 유물 보석(32×48, 칸당 32px 밀도) + 8프레임 대기 모션.

아래 32px이 그 칸. 바닥에는 숨 쉬듯 크기가 변하는 그림자와 희미한 빛 고리, 보석은 위아래로 떠다니고
작은 빛 조각이 보석 둘레를 돈다. 안전=청록, 위험=핏빛(금 간 보석). 외곽선은 맵 타일과 같은 색.
사용: python3 relic_float.py OUT_DIR
"""
import math
import sys
from PIL import Image

W, H = 32, 48
O = (30, 22, 34)
GEMS = {
    "safe": [(8, 40, 52), (14, 92, 110), (34, 160, 176), (120, 226, 230), (220, 255, 250)],
    "risky": [(40, 6, 14), (110, 14, 28), (188, 30, 42), (240, 96, 80), (255, 210, 190)],
}
FRAMES = 8
LIFT = [0, 1, 2, 3, 3, 2, 1, 0]
GROUND = 39  # 그림자·빛 고리 중심 줄


def frame(kind, f):
    G = GEMS[kind]
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
        if (i + f) % 4 != 0:
            put(x, y, G[3] if math.sin(t) > 0 else G[2], 230 if math.sin(t) > 0 else 160)
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
    if kind == "risky":
        for xy in ((17, mid - 5), (18, mid - 4), (18, mid - 3), (19, mid - 2), (19, mid - 1), (18, mid), (18, mid + 1), (19, mid + 2), (19, mid + 3), (18, mid + 4)):
            gem[xy] = G[0]
    # 빛 조각 3개가 기울어진 타원으로 보석 둘레를 돈다(뒤쪽은 보석에 가려진다)
    orbit = []
    for k in range(3):
        t = f / FRAMES * math.tau + k * math.tau / 3
        x, y = round(15.5 + 10 * math.cos(t)), round(mid + 2 + 3 * math.sin(t))
        orbit.append((x, y, math.sin(t) > 0))
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
    for kind in GEMS:
        fs = [frame(kind, f) for f in range(FRAMES)]
        fs[0].save(f"{out}/relic_float_{kind}.png")
        sheet = Image.new("RGBA", (W * FRAMES, H))
        for i, im in enumerate(fs):
            sheet.paste(im, (i * W, 0))
        sheet.save(f"{out}/relic_float_{kind}_idle.png")
        big = []
        for im in fs:
            bg = Image.new("RGBA", (W, H), (198, 160, 121, 255))
            bg.alpha_composite(im)
            big.append(bg.resize((W * 8, H * 8), Image.NEAREST))
        big[0].save(f"{out}/relic_float_{kind}.gif", save_all=True, append_images=big[1:], duration=130, loop=0)
