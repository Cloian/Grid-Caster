"""플레이어 마법 투사체 시안 3종의 프레임을 그린다.

각 시안은 32×32 캔버스, 4프레임 반복(프레임당 50ms)이며 두 방향을 따로 그린다.
- right: 오른쪽을 향함(상하좌우는 회전·뒤집기로 쓴다)
- diag : 오른쪽 위 대각선을 향함(대각선 4방향은 뒤집기로 쓴다). 45° 회전으로 생기는 픽셀 깨짐을 피한다.
색은 기본 마법 투사체(Assets/Projectiles/Attack/attack.aseprite) 5색에 외곽선·식는 보라를 더했다.
실행: python3 make_frames.py → frames/<시안>_<방향>_<번호>.png
"""
import math
from pathlib import Path
from PIL import Image

N = 32
ROOT = Path(__file__).resolve().parent
OUT = ROOT / "frames"
P = {
    "o": (0x0E, 0x0C, 0x1E),  # 외곽선
    "d": (0x3A, 0x1C, 0x6E),  # 식는 보라
    "r": (0x7E, 0x23, 0xC6),  # 보라
    "a": (0x4E, 0x84, 0xE5),  # 짙은 파랑
    "y": (0x63, 0x9B, 0xFF),  # 파랑
    "g": (0x5F, 0xCD, 0xE4),  # 청록
    "w": (0xFF, 0xFF, 0xFF),  # 백색
}
CX, CY = 16, 16
DIRS = {"right": (1.0, 0.0), "diag": (1 / math.sqrt(2), -1 / math.sqrt(2))}


class Frame:
    """(u, v) = (진행 방향, 수직) 좌표로 모양을 판정해 픽셀을 칠한다."""

    def __init__(self, direction):
        self.ux, self.uy = DIRS[direction]
        self.px, self.bare = {}, set()

    def uv(self, x, y):
        dx, dy = x - CX + 0.5, y - CY + 0.5
        return dx * self.ux + dy * self.uy, -dx * self.uy + dy * self.ux

    def xy(self, u, v):
        return (math.floor(CX + u * self.ux - v * self.uy), math.floor(CY + u * self.uy + v * self.ux))

    def put(self, x, y, c, outline=True):
        if 0 <= x < N and 0 <= y < N:
            self.px[(x, y)] = c
            (self.bare.discard if outline else self.bare.add)((x, y))

    def shape(self, color_at, outline=True):
        for y in range(N):
            for x in range(N):
                c = color_at(*self.uv(x, y))
                if c:
                    self.put(x, y, c, outline)

    def dot(self, u, v, c, size=2, outline=False):
        x, y = self.xy(u, v)
        for oy in range(size):
            for ox in range(size):
                self.put(x + ox, y + oy, c, outline)

    def image(self):
        img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
        solid = {p for p in self.px if p not in self.bare}
        for (x, y) in solid:
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < N and 0 <= ny < N and (nx, ny) not in self.px:
                    img.putpixel((nx, ny), P["o"] + (255,))
        for (x, y), c in self.px.items():
            img.putpixel((x, y), P[c] + (255,))
        return img


# ---------------- A. 혜성탄: 백열 구체 + 가늘어지는 꼬리, 뒤로 흐르는 2px 불티.
# 다듬기: 핵을 둥근 3×3 백열로(십자·사각형 모양 제거), 꼬리 첫 청록 띠를 없애 이음매를 지우고,
# 꼬리를 부드럽게 가늘게, 불티가 캔버스 밖으로 잘리지 않게 했다.
def comet(direction, k):
    f = Frame(direction)
    length = (11, 13, 10, 12)[k]
    sway = (0.0, 0.5, 0.0, -0.5)[k]

    def tail(u, v):
        t = (-u - 2.0) / length
        if not 0 <= t <= 1:
            return None
        half = 2.3 * (1 - t) ** 1.5 + 0.35
        if abs(v - sway * t) > half:
            return None
        return "y" if t < 0.45 else "a" if t < 0.8 else "r"
    f.shape(tail)

    def orb(u, v):
        r = math.hypot(u - 0.5, v)
        if r > 3.8:
            return None
        if math.hypot(u - 1.0, v) <= 1.6:
            return "w"
        return "g" if r <= 2.9 else "y"
    f.shape(orb)
    for i, (u, v, c) in enumerate(((-10, -4, "g"), (-8, 3, "y"), (-12, 2, "r"))):
        f.dot(u - ((k + i) % 3), v, c)
    if k % 2 == 0:
        f.dot(4.5, 0, "w", 1)
    return f


# ---------------- B. 룬 화살: 마름모 화살촉 + 촉 주위를 나선으로 도는 룬 조각 두 개.
def rune_dart(direction, k):
    f = Frame(direction)
    length = (8, 10, 8, 9)[k]

    def streak(u, v):
        t = (-u - 3) / length
        if 0 <= t <= 1 and abs(abs(v) - 1.3) < 0.55:
            return "y" if t < 0.4 else "a" if t < 0.75 else "r"
        return None
    f.shape(streak, outline=False)

    def head(u, v):
        if u < -4 or u > 7:
            return None
        if abs(v) > min((u + 4) * 0.55, (7 - u) * 0.55):
            return None
        return "w" if abs(v) < 0.8 and u > -1 else "g" if abs(v) < 1.8 else "a"
    f.shape(head)
    phase = k * math.pi / 2
    for sign, c in ((1, "r"), (-1, "g")):
        v = sign * 4.2 * math.cos(phase)
        u = -2 - 1.5 * sign * math.sin(phase)
        f.dot(u, v, c, 2, outline=True)
    return f


# ---------------- C. 비전 구체: 크게 도는 초승달 광택과 보라 고리, 뒤로 작아지며 식는 잔상 방울 세 개.
def arcane_orb(direction, k):
    f = Frame(direction)
    drift = (0.0, 1.0, 2.0, 1.0)[k]
    for u, radius, c in ((-8.5 - drift, 2.4, "y"), (-12.5 - drift, 1.8, "r"), (-15.5 - drift, 1.2, "d")):
        def ghost(uu, vv, u=u, radius=radius, c=c):
            return c if math.hypot(uu - u, vv) <= radius else None
        f.shape(ghost, outline=False)
    angle = k * math.pi / 2 + math.pi / 4

    def orb(u, v):
        r = math.hypot(u, v)
        if r > 5.3:
            return None
        if r > 4.4:
            return "r"
        a = math.atan2(v, u)
        diff = abs((a - angle + math.pi) % (2 * math.pi) - math.pi)
        if 2.4 < r <= 4.4 and diff < 0.75:
            return "w" if diff < 0.35 else "g"
        return "g" if r <= 1.6 else "y" if r <= 3.0 else "a"
    f.shape(orb)
    return f


DRAFTS = {"A_comet": comet, "B_rune_dart": rune_dart, "C_arcane_orb": arcane_orb}

if __name__ == "__main__":
    OUT.mkdir(exist_ok=True)
    for name, draw in DRAFTS.items():
        for direction in DIRS:
            for k in range(4):
                img = draw(direction, k).image()
                colors = {p[:3] for p in img.getdata() if p[3]}
                assert len(colors) <= 12, (name, direction, k, len(colors))
                img.save(OUT / f"{name}_{direction}_{k + 1}.png")
    print("ok")
