"""쓰러진 킹의 왕관 제단 64px (안전=백색 말·청록 빛, 위험=흑색 말·핏빛) + 6프레임 대기 모션.

금 간 체스판 석판 위에 거대한 킹의 부서진 왕관이 놓여 있다. 왕관의 보석 홈은 비어 있고
(유물이 거기서 나왔다), 부러진 십자와 왕관 조각이 석판에 굴러다닌다. 보석은 왕관 위에 떠 있고
석판 금 사이로 같은 색 빛이 샌다. 장식은 퀸·향로 사제와 같은 낡은 금색.
사용: python3 relic_altar_crown.py OUT_DIR
"""
import sys
from PIL import Image

W = H = 64
O = (6, 7, 14)
GOLD = [(96, 66, 28), (150, 104, 40), (200, 150, 62), (236, 196, 104)]
PIECE = {  # 왕관 돌(어둠→밝음)
    "safe": [(112, 102, 96), (168, 158, 142), (214, 202, 180), (244, 234, 212)],
    "risky": [(20, 22, 34), (36, 40, 58), (58, 64, 86), (92, 100, 124)],
}
INSIDE = {"safe": (70, 64, 66), "risky": (10, 11, 20)}
GEMS = {
    "safe": [(8, 40, 52), (14, 92, 110), (34, 160, 176), (120, 226, 230), (220, 255, 250)],
    "risky": [(40, 6, 14), (110, 14, 28), (188, 30, 42), (240, 96, 80), (255, 210, 190)],
}
CHECK = [(26, 28, 42), (66, 68, 84), (96, 98, 114)]  # 석판: 어두운 칸, 밝은 칸, 윗모서리


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
                if 0 <= nx < W and 0 <= ny < H and px[ny][nx] not in (None, O) and px[ny][nx] not in keep:
                    out[y][x] = O
                    break
    return out


def shade(ramp, x, x0, x1):
    if x - x0 < 2:
        return ramp[3]
    if x1 - x < 4:
        return ramp[0]
    return ramp[2] if x - x0 < (x1 - x0) * 0.45 else ramp[1]


# 석판 금(빛이 새는 길): 왕관 밑에서 바깥으로 뻗는다
CRACKS = [(14, 49), (15, 50), (16, 50), (17, 51), (18, 52), (19, 52), (20, 53),
          (44, 49), (45, 50), (46, 51), (46, 52), (47, 53), (48, 53),
          (28, 53), (29, 54), (29, 55), (36, 52), (37, 53), (37, 54)]


def slab(gem, frame):
    px = blank()
    # 윗면: 위에서 내려다본 체스판(칸 6×3), 앞모서리, 앞면
    for y in range(47, 56):
        for x in range(7, 57):
            sq = ((x - 7) // 6 + (y - 47) // 3) % 2
            c = CHECK[1] if sq else CHECK[0]
            if y == 47:
                c = CHECK[2]
            put(px, x, y, c)
    for y in range(56, 59):
        for x in range(7, 57):
            put(px, x, y, CHECK[0] if x < 52 else (20, 22, 32))
    for x in range(7, 57):
        put(px, x, 56, CHECK[1])
    for x, y in ((7, 47), (56, 47), (56, 58), (7, 58), (8, 58)):
        px[y][x] = None  # 깨진 모서리
    pulse = [1, 2, 3, 3, 2, 1][frame]
    for i, (x, y) in enumerate(CRACKS):
        put(px, x, y, gem[pulse] if i % 3 else gem[max(1, pulse - 1)])
    return px


def crown(kind, gem, frame):
    px = blank()
    P = PIECE[kind]
    inside = INSIDE[kind]
    # 왕관 안쪽(뒷벽): 갈퀴 사이로 보이는 어두운 속, 보석 빛이 아래에서 비친다
    for y in range(33, 37):
        for x in range(21 - (y - 33) // 2, 43 + (y - 33) // 2):
            put(px, x, y, inside)
    for x in range(26, 38):
        put(px, x, 36, gem[1]);
        if 28 <= x <= 35:
            put(px, x, 35, gem[0])
    # 갈퀴(뾰족한 끝 5개). 맨 오른쪽은 부러져 짧다
    points = [(19, 10), (25, 9), (31, 13), (38, 9), (44, 3)]
    for cx, h in points:
        for k in range(h):
            y = 36 - k
            half = 3 if k < 2 else 2 if k < h * 0.55 else 1
            for x in range(cx - half + 1, cx + half + 1):
                put(px, x, y, P[3] if x <= cx else P[1])
        if h >= 6:  # 끝의 금 구슬
            tip = 36 - h
            for dx, dy, c in ((0, 0, GOLD[3]), (1, 0, GOLD[2]), (0, 1, GOLD[2]), (1, 1, GOLD[1])):
                put(px, cx + dx, tip + dy - 1, c)
    put(px, 44, 34, O); put(px, 45, 35, P[0])  # 부러진 단면
    # 띠(본체)
    for y in range(37, 47):
        x0, x1 = 18 + (y - 37) // 5, 45 - (y - 37) // 5
        for x in range(x0, x1 + 1):
            put(px, x, y, shade(P, x, x0, x1))
    for x in range(18, 46):
        put(px, x, 37, GOLD[3] if x < 32 else GOLD[2] if x < 42 else GOLD[1])
    for x in range(19, 45):
        put(px, x, 44, GOLD[2] if x < 32 else GOLD[1])
        put(px, x, 45, GOLD[0])
    # 비어 있는 보석 홈 3개(가운데 홈에서 빛이 조금 샌다)
    glow = gem[[1, 2, 2, 3, 2, 1][frame]]
    for x, y in ((31, 39), (32, 39), (30, 40), (31, 40), (32, 40), (33, 40), (30, 41), (31, 41), (32, 41), (33, 41), (31, 42), (32, 42)):
        put(px, x, y, O if (x, y) in ((30, 40), (33, 40), (30, 41), (33, 41)) else glow)
    for x in (29, 34):
        put(px, x, 39, GOLD[2]); put(px, x, 42, GOLD[1])
    for sx in (22, 41):
        put(px, sx, 40, GOLD[3] if sx < 32 else GOLD[2]); put(px, sx, 41, GOLD[1])
    # 왕관 몸에 금 하나
    for x, y in ((35, 38), (35, 39), (36, 40), (36, 41), (37, 42), (37, 43)):
        put(px, x, y, P[0])
    # 석판 위에 굴러다니는 부러진 십자(왼쪽, 누워 있음)와 갈퀴 조각(오른쪽)
    for x in range(8, 17):
        for y in (50, 51):
            put(px, x, y, P[3] if y == 50 else P[1])
    for y in range(48, 54):
        put(px, 11, y, P[3]); put(px, 12, y, P[2])
    put(px, 16, 50, P[0]); put(px, 16, 51, O)  # 부러진 끝
    for k, (x, y) in enumerate(((49, 52), (50, 52), (51, 52), (52, 52), (50, 51), (51, 51), (51, 50))):
        put(px, x, y, P[2] if k < 4 else P[3])
    put(px, 52, 51, GOLD[2])
    return outline(px, keep=(inside,))


def gem_layer(kind, gem, frame):
    px = blank()
    lift = [0, 1, 2, 2, 1, 0][frame]
    top, mid, bot = 3 - lift, 11 - lift, 23 - lift
    for y in range(top, bot + 1):
        half = (y - top + 1) * 6 // (mid - top + 1) if y <= mid else (bot - y + 1) * 6 // (bot - mid + 1)
        half = max(1, half)
        for x in range(32 - half, 32 + half):
            left = x < 32
            c = (gem[3] if left else gem[2]) if y <= mid else (gem[2] if left else gem[1])
            if x in (31, 32):
                c = gem[4] if y <= mid else gem[3] if y < mid + 5 else gem[2]
            put(px, x, y, c)
    put(px, 29, mid - 3, gem[4]); put(px, 29, mid - 2, gem[4]); put(px, 30, mid - 4, gem[4])
    if kind == "risky":
        for x, y in ((34, mid - 2), (35, mid), (34, mid + 1), (35, mid + 3), (34, mid + 5)):
            put(px, x, y, gem[0])
    return outline(px)


MOTES = [(21, 26, 0), (42, 24, 2), (17, 18, 4), (46, 14, 1), (24, 10, 3), (40, 6, 5), (27, 30, 1), (37, 29, 4)]


def motes(gem, frame):
    px = blank()
    for x, y0, phase in MOTES:
        t = (frame + phase) % 6
        y = y0 - t
        if t < 4:
            put(px, x, y, gem[3] if t == 0 else gem[2] if t < 2 else gem[1])
    return px


def compose(layers):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for layer in layers:
        for y in range(H):
            for x in range(W):
                if layer[y][x] is not None:
                    img.putpixel((x, y), layer[y][x] + (255,))
    return img


def frames(kind):
    gem = GEMS[kind]
    return [compose([slab(gem, f), crown(kind, gem, f), motes(gem, f), gem_layer(kind, gem, f)]) for f in range(6)]


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    for kind in GEMS:
        fs = frames(kind)
        fs[0].save(f"{out}/relic_altar_{kind}.png")
        sheet = Image.new("RGBA", (W * len(fs), H))
        for i, f in enumerate(fs):
            sheet.paste(f, (i * W, 0))
        sheet.save(f"{out}/relic_altar_{kind}_idle.png")
        big = [f.resize((W * 4, H * 4), Image.NEAREST) for f in fs]
        big[0].save(f"{out}/relic_altar_{kind}.gif", save_all=True, append_images=big[1:],
                    duration=180, loop=0, disposal=2)
