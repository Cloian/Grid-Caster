"""가로 3칸 유물 제단(96×96, 칸당 32px 밀도 = 퀸 보스와 같은 배율) + 6프레임 대기 모션.

맨 아래 줄 3칸이 제단이 차지하는 칸: 양쪽은 화로 기둥(막힌 칸), 가운데는 제단(밟으면 획득).
그 위 두 칸 높이는 기둥·왕관·보석이 솟아 오르는 공간. 팔레트는 맵(모래색 바닥·벽돌)에서 가져오고,
안전=사암 + 청록 천·불꽃·보석, 위험=검붉은 벽돌 돌 + 핏빛 천·불꽃·보석.
사용: python3 relic_shrine.py OUT_DIR
"""
import sys
from PIL import Image

W = H = 96
O = (30, 22, 34)
SHADOW = (30, 22, 34, 90)
GOLD = [(120, 74, 30), (176, 120, 44), (222, 168, 64), (248, 210, 112)]
STONE = {
    "safe": [(140, 103, 90), (198, 160, 121), (219, 186, 143), (239, 214, 172)],
    "risky": [(46, 30, 38), (70, 48, 60), (99, 65, 66), (132, 89, 74)],
}
SEAM = {"safe": (158, 120, 98), "risky": (36, 24, 32)}
CLOTH = {
    "safe": [(8, 40, 52), (14, 80, 96), (24, 120, 136)],
    "risky": [(52, 8, 18), (100, 16, 30), (150, 28, 40)],
}
INSIDE = {"safe": (99, 65, 66), "risky": (24, 16, 24)}
GEMS = {
    "safe": [(8, 40, 52), (14, 92, 110), (34, 160, 176), (120, 226, 230), (220, 255, 250)],
    "risky": [(40, 6, 14), (110, 14, 28), (188, 30, 42), (240, 96, 80), (255, 210, 190)],
}


def blank():
    return [[None] * W for _ in range(H)]


def put(px, x, y, c):
    if 0 <= x < W and 0 <= y < H:
        px[y][x] = c


def outline(px, keep=()):
    out = [row[:] for row in px]
    for y in range(H):
        for x in range(W):
            if px[y][x] is not None:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < W and 0 <= ny < H:
                    c = px[ny][nx]
                    if c is not None and c != O and len(c) == 3 and c not in keep:
                        out[y][x] = O
                        break
    return out


def shade(ramp, x, x0, x1):
    if x - x0 < 2:
        return ramp[3]
    if x1 - x < 3:
        return ramp[0]
    return ramp[2] if x - x0 < (x1 - x0) * 0.45 else ramp[1]


def block(px, ramp, x0, x1, y0, y1, seam=None, seam_every=0, top=True):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            c = shade(ramp, x, x0, x1)
            if top and y == y0:
                c = ramp[3] if x < x1 - 3 else ramp[2]
            elif seam and seam_every and (x - x0) % seam_every == seam_every - 1 and 0 < x - x0 < x1 - x0 - 2:
                c = seam
            put(px, x, y, c)


def structure(kind, frame):
    S, G, C = STONE[kind], GEMS[kind], CLOTH[kind]
    seam = SEAM[kind]
    px = blank()
    # 계단 두 단
    block(px, S, 3, 92, 83, 91, seam, 12)
    for x in range(3, 93):
        put(px, x, 84, S[0])
    block(px, S, 8, 87, 76, 82, seam, 10)
    # 화로 기둥(좌우)
    for cx in (16, 79):
        block(px, S, cx - 9, cx + 8, 67, 75)                    # 받침
        block(px, S, cx - 6, cx + 5, 36, 66, top=False)          # 기둥
        for y in range(38, 66, 1):                               # 홈(세로 줄)
            put(px, cx - 2, y, S[1]); put(px, cx + 2, y, S[0])
        block(px, S, cx - 8, cx + 7, 48, 50)                      # 체스말 목의 칼라
        for x in range(cx - 8, cx + 8):
            put(px, x, 51, GOLD[1] if x < cx + 4 else GOLD[0])
        for i, y in enumerate(range(26, 36)):                     # 화로 사발
            half = 9 - i // 2
            for x in range(cx - half, cx + half):
                put(px, x, y, shade(S, x, cx - half, cx + half - 1))
        for x in range(cx - 9, cx + 9):
            put(px, x, 26, GOLD[3] if x < cx else GOLD[2])
            put(px, x, 27, INSIDE[kind] if cx - 7 <= x < cx + 7 else GOLD[1])
    # 오른쪽 기둥 금
    for x, y in ((82, 40), (83, 41), (83, 42), (84, 43), (84, 44), (83, 45), (13, 58), (12, 59), (12, 60), (11, 61)):
        put(px, x, y, S[0])
    # 제단 탁자
    block(px, S, 33, 62, 58, 75, top=False)
    block(px, S, 29, 66, 53, 57)
    for x in range(29, 67):
        put(px, x, 57, S[0])
    # 제단보(V자 끝, 금 테두리, 킹 십자 문양)
    for y in range(56, 74):
        x0, x1 = 37, 58
        if y > 68:
            x0, x1 = 37 + (y - 68) * 2, 58 - (y - 68) * 2
        for x in range(x0, x1 + 1):
            c = C[2] if x < 41 else C[1] if x < 55 else C[0]
            if x in (x0, x1) or y == 56:
                c = GOLD[2] if x < 48 else GOLD[1]
            put(px, x, y, c)
    glow = GOLD[3] if frame in (2, 3) else GOLD[2]
    for y in range(59, 68):
        put(px, 47, y, glow); put(px, 48, y, GOLD[1])
    for x in range(44, 52):
        put(px, x, 61, glow)
    # 탁자 위 쓰러진 킹의 왕관
    inside = INSIDE[kind]
    for y in range(38, 42):
        for x in range(38 - (y - 38) // 2, 58 + (y - 38) // 2):
            put(px, x, y, inside)
    for x in range(42, 54):
        put(px, x, 41, G[1])
    for cx, h in ((38, 10), (43, 9), (48, 13), (53, 9), (58, 3)):
        for k in range(h):
            y = 41 - k
            half = 3 if k < 2 else 2 if k < h * 0.55 else 1
            for x in range(cx - half + 1, cx + half + 1):
                put(px, x, y, S[3] if x <= cx else S[1])
        if h >= 6:
            tip = 41 - h
            for dx, dy, c in ((0, -1, GOLD[3]), (1, -1, GOLD[2]), (0, 0, GOLD[2]), (1, 0, GOLD[1])):
                put(px, cx + dx, tip + dy, c)
    for y in range(42, 53):
        x0, x1 = 36, 60
        for x in range(x0, x1 + 1):
            put(px, x, y, shade(S, x, x0, x1))
    for x in range(36, 61):
        put(px, x, 42, GOLD[3] if x < 48 else GOLD[2])
        put(px, x, 50, GOLD[1]); put(px, x, 51, GOLD[0])
    sock = G[[1, 2, 2, 3, 2, 1][frame]]
    for x, y in ((47, 45), (48, 45), (46, 46), (47, 46), (48, 46), (49, 46), (46, 47), (47, 47), (48, 47), (49, 47), (47, 48), (48, 48)):
        put(px, x, y, O if (x, y) in ((46, 46), (49, 46), (46, 47), (49, 47)) else sock)
    # 탁자 양끝 초
    for x0, top in ((30, 46), (63, 48)):
        for y in range(top, 53):
            put(px, x0, y, (239, 224, 196)); put(px, x0 + 1, y, (198, 176, 150))
        put(px, x0, top - 1, O)
    # 계단 위 부스러기
    for x, y in ((24, 79), (25, 79), (25, 78), (70, 80), (71, 80), (88, 86), (89, 86)):
        put(px, x, y, S[2])
    return outline(px, keep=(inside,))


def floor(kind, frame):
    px = blank()
    for y in range(86, 96):
        for x in range(0, 96):
            if ((x - 47.5) / 48) ** 2 + ((y - 91) / 4.5) ** 2 <= 1:
                put(px, x, y, SHADOW)
    return px


def fx(kind, frame):
    G = GEMS[kind]
    px = blank()
    # 화로 불꽃: 낮게 흔들리는 차가운 불(끝만 흔들림)
    for i, cx in enumerate((16, 79)):
        f = (frame + i * 3) % 6
        heights = [[7, 10, 8, 11, 9, 6, 8], [8, 9, 11, 9, 10, 7, 7], [6, 11, 9, 10, 8, 8, 9],
                   [7, 9, 10, 12, 9, 7, 6], [8, 10, 9, 10, 11, 8, 7], [7, 11, 10, 9, 9, 7, 8]][f]
        for j, h in enumerate(heights):
            x = cx - 7 + j * 2
            for k in range(h):
                y = 25 - k
                c = G[4] if k < 2 and 2 <= j <= 4 else G[3] if k < h * 0.5 else G[2] if k < h - 1 else G[1]
                sway = (1 if (f + j) % 3 == 0 else 0) if k > h - 3 else 0
                put(px, x + sway, y, c); put(px, x + 1 + sway, y, c if k < h - 1 else G[1])
        for e, (ex, ey) in enumerate(((cx - 4, 11), (cx + 5, 8), (cx, 4))):
            t = (frame + e * 2 + i) % 6
            if t < 4:
                put(px, ex + (t % 2), ey - t, G[3] if t < 2 else G[1])
    # 왕관 위에 떠 있는 보석
    lift = [0, 1, 2, 2, 1, 0][frame]
    top, mid, bot = 6 - lift, 15 - lift, 29 - lift
    gem = blank()
    for y in range(top, bot + 1):
        half = (y - top + 1) * 6 // (mid - top + 1) if y <= mid else (bot - y + 1) * 6 // (bot - mid + 1)
        half = max(1, half)
        for x in range(48 - half, 48 + half):
            left = x < 48
            c = (G[3] if left else G[2]) if y <= mid else (G[2] if left else G[1])
            if x in (47, 48):
                c = G[4] if y <= mid else G[3] if y < mid + 5 else G[2]
            put(gem, x, y, c)
    put(gem, 45, mid - 3, G[4]); put(gem, 45, mid - 2, G[4]); put(gem, 46, mid - 4, G[4])
    if kind == "risky":
        for x, y in ((50, mid - 2), (51, mid), (50, mid + 1), (51, mid + 3), (50, mid + 5)):
            put(gem, x, y, G[0])
    gem = outline(gem)
    for y in range(H):
        for x in range(W):
            if gem[y][x] is not None:
                px[y][x] = gem[y][x]
    for x, y0, ph in ((36, 30, 0), (60, 28, 2), (33, 20, 4), (63, 16, 1), (40, 12, 3), (57, 8, 5)):
        t = (frame + ph) % 6
        if t < 4:
            put(px, x, y0 - t, G[3] if t == 0 else G[2] if t < 2 else G[1])
    return px


def compose(layers):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for layer in layers:
        for y in range(H):
            for x in range(W):
                c = layer[y][x]
                if c is not None:
                    if len(c) == 4:
                        under = img.getpixel((x, y))
                        if under[3] == 0:
                            img.putpixel((x, y), c)
                    else:
                        img.putpixel((x, y), c + (255,))
    return img


def frames(kind):
    return [compose([floor(kind, f), structure(kind, f), fx(kind, f)]) for f in range(6)]


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    for kind in GEMS:
        fs = frames(kind)
        fs[0].save(f"{out}/relic_shrine_{kind}.png")
        sheet = Image.new("RGBA", (W * len(fs), H))
        for i, f in enumerate(fs):
            sheet.paste(f, (i * W, 0))
        sheet.save(f"{out}/relic_shrine_{kind}_idle.png")
        big = [f.resize((W * 3, H * 3), Image.NEAREST) for f in fs]
        big[0].save(f"{out}/relic_shrine_{kind}.gif", save_all=True, append_images=big[1:],
                    duration=180, loop=0, disposal=2)
