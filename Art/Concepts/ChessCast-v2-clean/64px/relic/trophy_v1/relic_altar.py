"""체스 컨셉 유물 제단 64px (안전=청록 보석, 위험=붉은 보석) + 4프레임 대기 모션.

구성(아래→위): 체커보드 띠를 두른 체스판 석판 → 금띠 2단 받침 → 체스말 목(칼라 링) →
여왕 왕관 갈퀴가 달린 잔 → 그 위에 떠 있는 보석. 팔레트는 퀸·향로 사제(청록/금/크림)와 맞춘다.
사용: python3 relic_altar.py OUT_DIR
"""
import sys
from PIL import Image

W = H = 64
O = (7, 19, 27)
TEAL = [(4, 42, 50), (6, 69, 77), (12, 99, 109), (36, 125, 133)]          # 어둠→밝음
GOLD = [(157, 102, 29), (206, 144, 41), (242, 183, 66), (255, 207, 104)]
STONE = [(141, 159, 169), (194, 206, 209), (243, 238, 221), (250, 233, 204)]
GEMS = {
    "safe": [(6, 69, 77), (18, 140, 170), (18, 190, 214), (127, 232, 242), (232, 251, 255)],
    "risky": [(74, 12, 26), (150, 20, 36), (224, 36, 44), (255, 122, 90), (255, 224, 200)],
}


def blank():
    return [[None] * W for _ in range(H)]


def put(px, x, y, c):
    if 0 <= x < W and 0 <= y < H:
        px[y][x] = c


def shaded_rect(px, x0, x1, y0, y1, ramp, top=None):
    """왼쪽 위에서 빛: 왼쪽 2열 밝게, 오른쪽 3열 어둡게, 맨 윗줄은 top 색."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if x - x0 < 2:
                c = ramp[3]
            elif x1 - x < 3:
                c = ramp[0]
            else:
                c = ramp[2] if (x - x0) < (x1 - x0) * 0.45 else ramp[1]
            if top and y == y0:
                c = top
            put(px, x, y, c)


def outline(px):
    out = [row[:] for row in px]
    for y in range(H):
        for x in range(W):
            if px[y][x] is not None:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < W and 0 <= ny < H and px[ny][nx] is not None and px[ny][nx] != O:
                    out[y][x] = O
                    break
    return out


def pedestal(gem):
    px = blank()
    # 1) 체스판 석판: 윗면(크림) + 앞면 체커 띠 2줄 + 그림자 줄
    shaded_rect(px, 13, 50, 51, 53, [STONE[0], STONE[1], STONE[1], STONE[2]], top=STONE[2])
    for y in range(54, 58):
        for x in range(13, 51):
            sq = ((x - 13) // 4 + (y - 54) // 2) % 2
            c = (STONE[2] if sq == 0 else TEAL[1])
            if x < 15:
                c = STONE[3] if sq == 0 else TEAL[2]
            if x > 47:
                c = STONE[0] if sq == 0 else TEAL[0]
            put(px, x, y, c)
    for x in range(13, 51):
        put(px, x, 58, TEAL[0])
    # 2) 2단 받침(금띠)
    shaded_rect(px, 18, 45, 45, 50, TEAL, top=GOLD[2])
    for x in range(18, 46):
        put(px, x, 46, GOLD[0] if x > 42 else GOLD[1])
    # 3) 체스말 목: 칼라 링 + 가는 기둥
    shaded_rect(px, 25, 38, 33, 44, TEAL)
    shaded_rect(px, 22, 41, 39, 40, GOLD, top=GOLD[3])
    shaded_rect(px, 23, 40, 41, 41, TEAL)
    # 기둥 앞 문양: 작은 왕관(점 3개 + 받침)
    for x, y in ((29, 35), (31, 34), (32, 34), (34, 35), (30, 36), (31, 36), (32, 36), (33, 36),
                 (29, 37), (30, 37), (31, 37), (32, 37), (33, 37), (34, 37)):
        put(px, x, y, GOLD[2] if x < 33 else GOLD[1])
    # 4) 잔: 아래로 좁아지는 사발 + 금 테두리
    for i, y in enumerate(range(27, 33)):
        x0, x1 = 19 + i, 44 - i
        shaded_rect(px, x0, x1, y, y, TEAL)
    for x in range(19, 45):
        put(px, x, 27, GOLD[3] if x < 30 else GOLD[2] if x < 40 else GOLD[1])
    # 보석 빛이 잔 안쪽에 비친다
    for x in range(27, 37):
        put(px, x, 28, gem[3] if 29 <= x <= 34 else gem[2])
    # 5) 여왕 왕관 갈퀴: 바깥 둘은 밖으로 휘고 끝에 금 구슬
    prongs = [((19, 26), (18, 24), (17, 22)), ((25, 26), (25, 24)), ((38, 26), (38, 24)), ((44, 26), (45, 24), (46, 22))]
    for prong in prongs:
        for (x, y) in prong:
            for yy in (y, y - 1):
                put(px, x, yy, TEAL[2] if x < 32 else TEAL[1])
                put(px, x + 1, yy, TEAL[1] if x < 32 else TEAL[0])
        tx, ty = prong[-1]
        ty -= 2
        for dx, dy, c in ((0, 0, GOLD[3]), (1, 0, GOLD[2]), (0, -1, GOLD[2]), (1, -1, GOLD[1])):
            put(px, tx + dx, ty + dy, c)
    return outline(px)


def gem_layer(gem, lift):
    px = blank()
    top, mid, bot = 4 - lift, 13 - lift, 25 - lift
    for y in range(top, bot + 1):
        half = (y - top + 1) * 8 // (mid - top + 1) if y <= mid else (bot - y + 1) * 8 // (bot - mid + 1)
        half = max(1, half)
        for x in range(32 - half, 32 + half):
            left = x < 32
            if y <= mid:
                c = gem[3] if left else gem[2]
            else:
                c = gem[2] if left else gem[1]
            if x == 31 or x == 32:  # 가운데 능선
                c = gem[4] if y <= mid else gem[3]
            if y == mid:
                c = gem[3] if left else gem[1]
            put(px, x, y, c)
    for x, y in ((29, mid - 3), (29, mid - 2), (30, mid - 4)):  # 하이라이트
        put(px, x, y, gem[4])
    if gem is GEMS["risky"]:  # 위험 제단: 보석에 금이 간다
        for x, y in ((34, mid - 3), (35, mid - 2), (34, mid - 1), (35, mid), (36, mid + 1), (35, mid + 2), (35, mid + 3)):
            put(px, x, y, gem[0])
    px = outline(px)
    return px


SPARKLES = [(21, 8), (44, 11), (18, 18), (47, 19), (40, 3)]


def sparkle_layer(gem, frame):
    px = blank()
    for i, (x, y) in enumerate(SPARKLES):
        phase = (frame + i) % 4
        if phase == 0:
            for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                put(px, x + dx, y + dy, gem[4] if (dx, dy) == (0, 0) else gem[3])
        elif phase == 1:
            put(px, x, y, gem[3])
        elif phase == 3:
            put(px, x, y + 1, gem[2])
    return px


def compose(*layers):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    for layer in layers:
        for y in range(H):
            for x in range(W):
                if layer[y][x] is not None:
                    img.putpixel((x, y), layer[y][x] + (255,))
    return img


def frames(kind):
    gem = GEMS[kind]
    base = pedestal(gem)
    lifts = [0, 1, 1, 0]
    return [compose(base, sparkle_layer(gem, f), gem_layer(gem, lifts[f])) for f in range(4)]


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
        big[0].save(f"{out}/relic_altar_{kind}.gif", save_all=True, append_images=big[1:], duration=160, loop=0, disposal=2)
