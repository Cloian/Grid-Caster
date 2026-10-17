"""어둡고 미스터리한 유물 제단 64px (안전=창백한 청록, 위험=핏빛) + 6프레임 대기 모션.

낮고 넓은 고대 석단(이가 빠지고 금 간 검푸른 돌), 앞면에 희미하게 빛나는 왕(king)의 십자 문양,
양 끝에 녹아내린 초와 보석 색의 차가운 불꽃, 그 위에 어두운 기운을 두르고 떠 있는 보석 파편.
발밑은 금 간 체스판 바닥 그림자. 사용: python3 relic_altar_dark.py OUT_DIR
"""
import sys
from PIL import Image

W = H = 64
O = (6, 7, 14)
STONE = [(22, 24, 38), (36, 40, 58), (54, 60, 82), (86, 94, 118)]   # 어둠→밝음
CRACK = (10, 11, 20)
WAX = [(92, 84, 80), (150, 138, 120), (196, 184, 160)]
GEMS = {
    "safe": [(8, 40, 52), (14, 92, 110), (34, 160, 176), (120, 226, 230), (220, 255, 250)],
    "risky": [(40, 6, 14), (110, 14, 28), (188, 30, 42), (240, 96, 80), (255, 210, 190)],
}


def blank():
    return [[None] * W for _ in range(H)]


def put(px, x, y, c):
    if 0 <= x < W and 0 <= y < H:
        px[y][x] = c


def outline(px):
    out = [row[:] for row in px]
    for y in range(H):
        for x in range(W):
            if px[y][x] is not None:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < W and 0 <= ny < H and px[ny][nx] not in (None, O):
                    out[y][x] = O
                    break
    return out


def stone_rect(px, x0, x1, y0, y1, lit_top=True):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if lit_top and y == y0:
                c = STONE[3] if x < x1 - 4 else STONE[2]
            elif x - x0 < 2:
                c = STONE[2]
            elif x1 - x < 4:
                c = STONE[0]
            else:
                # 얼룩진 돌결: 좌표 해시로 어두운 점을 흩뿌린다
                c = STONE[1] if (x * 7 + y * 13) % 11 else STONE[0]
            put(px, x, y, c)


def altar(gem, frame):
    px = blank()
    # 상판(넓게), 몸통, 받침
    stone_rect(px, 9, 54, 38, 41)
    stone_rect(px, 13, 50, 42, 53, lit_top=False)
    for x in range(13, 51):
        put(px, x, 42, STONE[0])  # 상판 아래 그림자
    stone_rect(px, 10, 53, 54, 57)
    # 이 빠진 모서리
    for x, y in ((9, 38), (10, 38), (9, 39), (54, 38), (53, 41), (54, 41), (10, 57), (53, 54)):
        px[y][x] = None
    # 금 간 자국
    for x, y in ((20, 38), (21, 39), (21, 40), (22, 41), (44, 43), (45, 44), (45, 45), (46, 46), (46, 47),
                 (16, 48), (17, 49), (17, 50), (38, 54), (39, 55), (40, 55), (41, 56)):
        put(px, x, y, CRACK)
    # 앞면: 원 안의 왕 십자(king) 문양, 빛이 맥박처럼 밝아졌다 어두워진다
    glow = [gem[1], gem[2], gem[3], gem[3], gem[2], gem[1]][frame]
    dim = gem[1] if glow != gem[1] else gem[0]
    cx, cy = 31.5, 47.5
    for y in range(43, 53):
        for x in range(25, 39):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5
            if 4.2 <= d <= 5.0:
                put(px, x, y, dim)
    for y in range(44, 52):
        put(px, 31, y, glow); put(px, 32, y, glow)
    for x in range(29, 35):
        put(px, x, 46, glow)
    # 촛대 초 두 자루(왼쪽은 길고 오른쪽은 짧게 녹음), 촛농
    for (x0, top) in ((12, 30), (48, 33)):
        for y in range(top, 38):
            put(px, x0, y, WAX[2]); put(px, x0 + 1, y, WAX[1]); put(px, x0 + 2, y, WAX[0])
        put(px, x0 + 1, top - 1, O)  # 심지
        put(px, x0 - 1, 37, WAX[1]); put(px, x0 + 3, 37, WAX[0]); put(px, x0 + 3, 36, WAX[0])
        put(px, x0 + 2, 39, WAX[1])  # 상판 위로 흘러내린 촛농
    px = outline(px)
    # 바닥 그림자(금 간 체스판 칸 두 개가 비친다)
    for x in range(8, 56):
        for y in (58, 59):
            if px[y][x] is None and abs(x - 31.5) < 24 - (y - 58) * 4:
                px[y][x] = (14, 16, 26) if ((x - 8) // 8) % 2 else (22, 24, 38)
    return px


def flames(gem, frame):
    px = blank()
    for i, (x, top) in enumerate(((13, 29), (49, 32))):
        sway = [0, 0, 1, 0, 0, -1][(frame + i * 2) % 6]
        tall = [3, 4, 4, 3, 4, 3][(frame + i) % 6]
        for k in range(tall):
            y = top - k
            c = gem[4] if k == 0 else gem[3] if k == 1 else gem[2]
            put(px, x + (sway if k >= 2 else 0), y, c)
        put(px, x - 1, top, gem[2]); put(px, x + 1, top, gem[1])
    return px


def gem_layer(gem, frame):
    px = blank()
    lift = [0, 1, 2, 2, 1, 0][frame]
    top, mid, bot = 10 - lift, 18 - lift, 33 - lift
    for y in range(top, bot + 1):
        half = (y - top + 1) * 6 // (mid - top + 1) if y <= mid else (bot - y + 1) * 6 // (bot - mid + 1)
        half = max(1, half)
        for x in range(32 - half, 32 + half):
            left = x < 32
            c = (gem[3] if left else gem[2]) if y <= mid else (gem[2] if left else gem[1])
            if x in (31, 32) and y > mid + 2:
                c = gem[0]  # 속이 어두운 파편
            put(px, x, y, c)
    put(px, 30, mid - 3, gem[4]); put(px, 30, mid - 2, gem[4]); put(px, 31, mid - 4, gem[4])
    if gem is GEMS["risky"]:
        for x, y in ((33, mid - 2), (34, mid), (33, mid + 1), (34, mid + 3), (35, mid + 4)):
            put(px, x, y, gem[0])
    return outline(px), lift


# 어두운 기운: 보석 주위를 천천히 떠오르는 입자. (x, 시작 y, 위상)
MOTES = [(22, 30, 0), (41, 28, 2), (19, 22, 4), (44, 18, 1), (26, 12, 3), (38, 8, 5), (24, 35, 1), (40, 34, 4)]


def aura(gem, frame):
    px = blank()
    for x, y0, phase in MOTES:
        t = (frame + phase) % 6
        y = y0 - t
        if t < 4:
            put(px, x, y, gem[1] if t < 2 else gem[0])
        if t == 1:
            put(px, x, y - 1, gem[2])
    # 보석 아래 상판 위로 번지는 희미한 빛
    for x in range(26, 38):
        put(px, x, 37, gem[0] if x in (26, 37) else gem[1])
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
    out = []
    for f in range(6):
        g, _ = gem_layer(gem, f)
        out.append(compose([altar(gem, f), aura(gem, f), flames(gem, f), g]))
    return out


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
