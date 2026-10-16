"""범위 공격(강화 폭발) 이펙트 시안 3종의 프레임을 그린다.

각 시안은 32×32 타일 한 칸 기준이며 `center`(직격 칸) 6프레임과 `splash`(주변 칸) 6프레임으로 구성된다.
알파는 0/255만 쓰고, 주요 형태에는 1px 외곽선(#0E0C1E)을 자동으로 두른다(ArtStyleBible §4·§5).
실행: python3 make_frames.py  → frames/<시안>_<태그>_<번호>.png
"""
from pathlib import Path
from PIL import Image

N = 32
ROOT = Path(__file__).resolve().parent
OUT = ROOT / "frames"

# warm: 강화 투사체(#FFE08C)·꼬리(#FFBA47)에서 이어지는 따뜻한 램프 + 공통 외곽선.
WARM = {
    "o": (0x0E, 0x0C, 0x1E),  # 외곽선
    "d": (0x6A, 0x22, 0x26),  # 짙은 잉걸
    "r": (0xC2, 0x46, 0x1E),  # 적주황
    "a": (0xF0, 0x7A, 0x26),  # 주황
    "y": (0xFF, 0xBA, 0x47),  # 호박(투사체 꼬리)
    "g": (0xFF, 0xE0, 0x8C),  # 옅은 금(투사체)
    "w": (0xFF, 0xF6, 0xD8),  # 백열
    "s": (0x3A, 0x26, 0x30),  # 그을음(바닥 자국)
}
# magic: 기본 마법 투사체(Assets/Projectiles/Attack/attack.aseprite)의 5색을 그대로 쓰고, 식는 색만 짙은 보라 2색을 더한다.
MAGIC = {
    "o": (0x0E, 0x0C, 0x1E),
    "d": (0x3A, 0x1C, 0x6E),  # 식은 보라
    "r": (0x7E, 0x23, 0xC6),  # 투사체 보라
    "a": (0x4E, 0x84, 0xE5),  # 투사체 짙은 파랑
    "y": (0x63, 0x9B, 0xFF),  # 투사체 파랑
    "g": (0x5F, 0xCD, 0xE4),  # 투사체 청록
    "w": (0xFF, 0xFF, 0xFF),  # 투사체 백색
    "s": (0x26, 0x1C, 0x48),  # 자국
}
PALETTES = {"warm": WARM, "magic": MAGIC}
P = WARM


class Frame:
    def __init__(self):
        self.px = {}
        self.no_outline = set()

    def put(self, x, y, c, outline=True):
        if 0 <= x < N and 0 <= y < N:
            self.px[(x, y)] = c
            if not outline:
                self.no_outline.add((x, y))
            else:
                self.no_outline.discard((x, y))

    def rect(self, x0, y0, x1, y1, c, outline=True):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c, outline)

    def line(self, x0, y0, x1, y1, c, outline=True):
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.put(x0, y0, c, outline)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def diamond(self, cx, cy, r, c, fill=True, outline=True):
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r, cx + r + 1):
                d = abs(x - cx) + abs(y - cy)
                if (fill and d <= r) or (not fill and d == r):
                    self.put(x, y, c, outline)

    def disc(self, cx, cy, r, c, outline=True):
        for y in range(cy - r, cy + r + 1):
            for x in range(cx - r, cx + r + 1):
                if (x - cx) ** 2 + (y - cy) ** 2 <= r * r + r:
                    self.put(x, y, c, outline)

    def ring(self, cx, cy, r, thick, c, outline=True, keep=None):
        for y in range(cy - r - 1, cy + r + 2):
            for x in range(cx - r - 1, cx + r + 2):
                d2 = (x - cx) ** 2 + (y - cy) ** 2
                if (r - thick) ** 2 + (r - thick) < d2 <= r * r + r:
                    if keep is None or keep(x - cx, y - cy):
                        self.put(x, y, c, outline)

    def image(self):
        img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        solid = {p for p in self.px if p not in self.no_outline}
        for (x, y) in solid:
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < N and 0 <= ny < N and (nx, ny) not in self.px:
                    img.putpixel((nx, ny), P["o"] + (255,))
        for (x, y), c in self.px.items():
            img.putpixel((x, y), P[c] + (255,))
        return img


C = 15  # 칸 중심(0-index, 32칸에서 15/16 사이를 15로 둔다)


def embers(f, points):
    lift = {"d": "r", "s": "r", "r": "a"}
    for x, y, c in points:
        f.rect(x, y, x + 1, y + 1, lift.get(c, c), outline=False)


# ---------------- A. 열파 고리: 중심 폭발이 둥근 열파로 번지고, 주변 칸 테두리가 달아올랐다 잉걸로 흩어진다.
def heat_center():
    frames = []
    f = Frame(); f.diamond(C, C, 3, "y"); f.diamond(C, C, 1, "w"); frames.append(f)
    f = Frame()
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        f.line(C, C, C + dx * 9, C + dy * 9, "y")
        f.line(C + dx, C + dy, C + dx * 7, C + dy * 7, "g", False)
    f.disc(C, C, 4, "a"); f.disc(C, C, 3, "g"); f.disc(C, C, 1, "w"); frames.append(f)
    f = Frame(); f.ring(C, C, 10, 2, "a"); f.ring(C, C, 9, 1, "y", False); f.disc(C, C, 3, "y"); f.disc(C, C, 1, "w"); frames.append(f)
    f = Frame(); f.ring(C, C, 13, 2, "r", keep=lambda x, y: (abs(x) > 3 and abs(y) > 3) or abs(x) < 7 and abs(y) < 7 or (x + y) % 5 != 0)
    f.ring(C, C, 12, 1, "a", False, keep=lambda x, y: (x * 3 + y) % 4 != 0); f.disc(C, C, 2, "a"); frames.append(f)
    f = Frame(); embers(f, [(4, 5, "a"), (25, 6, "y"), (6, 25, "r"), (24, 24, "a"), (15, 3, "r"), (3, 15, "a"), (27, 15, "r")]); f.disc(C, C, 1, "d"); frames.append(f)
    f = Frame(); embers(f, [(5, 2, "r"), (25, 3, "d"), (23, 21, "r"), (14, 1, "d")]); frames.append(f)
    return frames


def heat_splash():
    frames = []
    f = Frame()
    for i in range(4, 28):
        for (x, y) in ((i, 4), (i, 27), (4, i), (27, i)):
            f.put(x, y, "g")
    for i in range(5, 27):
        for (x, y) in ((i, 5), (i, 26), (5, i), (26, i)):
            f.put(x, y, "y", False)
    frames.append(f)
    f = Frame()
    for i in range(6, 26):
        for (x, y) in ((i, 6), (i, 25), (6, i), (25, i)):
            f.put(x, y, "a")
        for (x, y) in ((i, 7), (i, 24), (7, i), (24, i)):
            if 7 <= i <= 24:
                f.put(x, y, "y", False)
    for x, y in ((6, 6), (25, 6), (6, 25), (25, 25)):
        f.rect(x - 1, y - 1, x + 1, y + 1, "g")
    frames.append(f)
    f = Frame()
    for cx, cy, sx, sy in ((7, 7, 1, 1), (24, 7, -1, 1), (7, 24, 1, -1), (24, 24, -1, -1)):
        f.rect(min(cx, cx + sx * 5), min(cy, cy + sy), max(cx, cx + sx * 5), max(cy, cy + sy), "a")
        f.rect(min(cx, cx + sx), min(cy, cy + sy * 5), max(cx, cx + sx), max(cy, cy + sy * 5), "a")
        f.rect(min(cx, cx + sx), min(cy, cy + sy), max(cx, cx + sx), max(cy, cy + sy), "y", False)
    embers(f, [(14, 10, "y"), (17, 19, "a")])
    frames.append(f)
    f = Frame()
    for cx, cy, sx, sy in ((9, 9, 1, 1), (22, 9, -1, 1), (9, 22, 1, -1), (22, 22, -1, -1)):
        f.rect(min(cx, cx + sx * 2), min(cy, cy + sy), max(cx, cx + sx * 2), max(cy, cy + sy), "a", False)
        f.rect(min(cx, cx + sx), min(cy, cy + sy * 2), max(cx, cx + sx), max(cy, cy + sy * 2), "r", False)
    embers(f, [(13, 6, "a"), (18, 14, "y"), (12, 17, "r")])
    frames.append(f)
    f = Frame(); embers(f, [(13, 3, "r"), (19, 9, "a"), (11, 12, "d")]); frames.append(f)
    f = Frame(); embers(f, [(14, 1, "d"), (20, 5, "r")]); frames.append(f)
    return frames


# ---------------- B. 낙인 문양: 칸마다 마름모 문양이 찍혀 "맞은 칸"이 또렷이 읽히고, 식으면서 눈금만 남는다.
def sigil_center():
    frames = []
    f = Frame(); f.rect(C - 1, 0, C + 2, C + 1, "g"); f.rect(C, 0, C + 1, C, "w", False); f.diamond(C, C + 2, 2, "g"); f.put(C, C + 2, "w", False); frames.append(f)
    f = Frame(); f.rect(C - 1, 0, C + 2, C, "y"); f.diamond(C, C, 7, "a"); f.diamond(C, C, 5, "g"); f.diamond(C, C, 2, "w"); frames.append(f)
    f = Frame(); f.diamond(C, C, 10, "a"); f.diamond(C, C, 8, "y", False); f.diamond(C, C, 6, "a", False); f.diamond(C, C, 3, "g"); f.diamond(C, C, 1, "w")
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        f.line(C + dx * 12, C + dy * 12, C + dx * 14, C + dy * 14, "y")
    frames.append(f)
    f = Frame(); f.diamond(C, C, 11, "a", fill=False); f.diamond(C, C, 10, "r", fill=False, outline=False); f.diamond(C, C, 6, "a", fill=False); f.diamond(C, C, 2, "y")
    for dx, dy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        f.line(C + dx * 8, C + dy * 8, C + dx * 10, C + dy * 10, "y")
    frames.append(f)
    f = Frame(); f.diamond(C, C, 11, "r", fill=False, outline=False); f.diamond(C, C, 6, "d", fill=False, outline=False); f.put(C, C, "r", False); frames.append(f)
    f = Frame()
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        f.line(C + dx * 10, C + dy * 10, C + dx * 11, C + dy * 11, "d", False)
    frames.append(f)
    return frames


def sigil_splash():
    frames = []
    f = Frame(); f.diamond(C, C, 2, "w"); frames.append(f)
    f = Frame(); f.diamond(C, C, 6, "y"); f.diamond(C, C, 4, "g", False); f.diamond(C, C, 1, "w"); frames.append(f)
    f = Frame(); f.diamond(C, C, 8, "a", fill=False); f.diamond(C, C, 7, "y", fill=False, outline=False); f.diamond(C, C, 2, "g")
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        f.line(C + dx * 10, C + dy * 10, C + dx * 12, C + dy * 12, "y")
    frames.append(f)
    f = Frame(); f.diamond(C, C, 8, "r", fill=False); f.diamond(C, C, 7, "a", fill=False, outline=False); f.diamond(C, C, 1, "a")
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        f.line(C + dx * 10, C + dy * 10, C + dx * 12, C + dy * 12, "a")
    frames.append(f)
    f = Frame(); f.diamond(C, C, 8, "r", fill=False, outline=False)
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        f.line(C + dx * 10, C + dy * 10, C + dx * 11, C + dy * 11, "r", False)
    frames.append(f)
    f = Frame()
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        f.line(C + dx * 10, C + dy * 10, C + dx * 11, C + dy * 11, "d", False)
    frames.append(f)
    return frames


# ---------------- C. 파편 착탄: 지금의 잔여 마력 파편이 각 칸에 꽂혀 부서지고 그을음 자국을 남긴다.
def shard_center():
    frames = []
    f = Frame(); f.diamond(C, C, 4, "g"); f.diamond(C, C, 2, "w")
    for dx, dy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        f.line(C + dx * 4, C + dy * 4, C + dx * 7, C + dy * 7, "y")
    frames.append(f)
    f = Frame()
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        f.line(C, C, C + dx * 12, C + dy * 12, "y")
    for dx, dy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        f.line(C, C, C + dx * 8, C + dy * 8, "a")
    f.disc(C, C, 3, "g"); f.disc(C, C, 1, "w"); frames.append(f)
    f = Frame(); f.disc(C, C, 4, "a"); f.disc(C, C, 2, "y")
    for dx, dy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        x, y = C + dx * 9, C + dy * 9
        f.rect(x - 1, y - 1, x + 1, y + 1, "g"); f.line(x - dx * 2, y - dy * 2, x - dx * 4, y - dy * 4, "y")
    frames.append(f)
    f = Frame(); f.disc(C, C, 4, "d"); f.disc(C, C, 2, "r", outline=False)
    for dx, dy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        x, y = C + dx * 13, C + dy * 13
        f.rect(x - 1, y - 1, x, y, "a")
    frames.append(f)
    f = Frame(); f.disc(C, C, 3, "d", outline=False); f.disc(C, C, 1, "r", outline=False)
    embers(f, [(C - 7, C + 5, "d"), (C + 6, C - 6, "s")]); frames.append(f)
    f = Frame(); f.diamond(C, C, 2, "d", outline=False); frames.append(f)
    return frames


def shard_splash():
    frames = []
    f = Frame(); f.rect(6, 6, 8, 8, "g")
    for k in range(0, 2): f.line(1 + k, 0, 5 + k, 4, "y", False); f.line(0, 1 + k, 4, 5 + k, "a", False)
    frames.append(f)
    f = Frame(); f.rect(C - 2, C - 2, C, C, "g"); f.put(C - 1, C - 1, "w")
    for k in range(0, 2): f.line(C - 9 + k, C - 10, C - 4 + k, C - 5, "y", False); f.line(C - 10, C - 9 + k, C - 5, C - 4 + k, "a", False)
    frames.append(f)
    f = Frame()
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        f.line(C, C, C + dx * 6, C + dy * 6, "y")
    for dx, dy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        f.line(C + dx, C + dy, C + dx * 4, C + dy * 4, "a")
    f.diamond(C, C, 2, "g"); f.put(C, C, "w"); frames.append(f)
    f = Frame(); f.diamond(C, C, 2, "r", outline=False); f.put(C, C, "y", False)
    embers(f, [(C - 8, C - 3, "y"), (C + 6, C - 7, "a"), (C + 5, C + 6, "a"), (C - 6, C + 7, "r")]); frames.append(f)
    f = Frame(); f.diamond(C, C, 1, "d", outline=False); embers(f, [(C - 10, C - 4, "r"), (C + 8, C + 8, "d")]); frames.append(f)
    f = Frame(); f.put(C, C, "d", False); frames.append(f)
    return frames


DRAFTS = {
    "A_heat_ring": (heat_center, heat_splash),
    "B_brand_sigil": (sigil_center, sigil_splash),
    "C_shard_strike": (shard_center, shard_splash),
}

def frames_dir(palette):
    return OUT if palette == "warm" else ROOT / f"frames_{palette}"


if __name__ == "__main__":
  for palette, colors in PALETTES.items():
    P = colors
    out = frames_dir(palette)
    out.mkdir(exist_ok=True)
    for name, (center, splash) in DRAFTS.items():
        for tag, make in (("center", center), ("splash", splash)):
            frames = make()
            assert len(frames) == 6, (name, tag, len(frames))
            for i, f in enumerate(frames, 1):
                img = f.image()
                colors = {img.getpixel((x, y))[:3] for x in range(N) for y in range(N) if img.getpixel((x, y))[3]}
                assert len(colors) <= 12, (name, tag, i, len(colors))
                img.save(out / f"{name}_{tag}_{i}.png")
    print("ok")
