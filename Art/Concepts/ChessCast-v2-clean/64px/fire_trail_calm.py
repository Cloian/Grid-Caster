"""비숍 불길 칸(32px × 4프레임) — 눈이 덜 피로하게 낮고 차분한 불.

바닥에 깔린 숯불(어두운 빨강·주황) 위로 낮은 불꽃 3개(5~9px)만 올린다.
프레임마다 불꽃 끝만 1px 흔들고 몸통 모양은 같게 둬 깜빡임(번쩍임)을 줄인다.

실행: python3 fire_trail_calm.py <out.png>
"""
import sys
from PIL import Image

T, N = 32, 4
OUT = (40, 18, 22)
RED, ORANGE, YELLOW = (178, 40, 30), (232, 104, 24), (246, 196, 64)
COAL_D, COAL = (92, 30, 28), (150, 52, 32)


def frame(i):
    img = Image.new("RGBA", (T, T))
    p = img.load()

    def put(x, y, c):
        if 0 <= x < T and 0 <= y < T:
            p[x, y] = c + (255,)

    # 숯불 바닥(납작한 타원, 가장자리는 어둡게)
    for y in range(24, 29):
        for x in range(4, 28):
            d = ((x + 0.5 - 16) / 12) ** 2 + ((y + 0.5 - 26.5) / 2.6) ** 2
            if d <= 1:
                c = COAL_D if d > 0.6 else COAL
                if d < 0.35 and (x + y + i) % 5 == 0:
                    c = ORANGE           # 숯 사이 불씨가 천천히 옮겨 다님
                put(x, y, c)
    # 낮은 불꽃 3개: 몸통은 고정, 끝만 프레임마다 1px 좌우
    for cx, height, phase in ((10, 6, 0), (16, 9, 1), (22, 7, 2)):
        tip = (0, 1, 0, -1)[(i + phase) % 4]
        for h in range(height):
            y = 24 - h
            t = h / max(1, height - 1)
            half = 2 if t < 0.3 else 1 if t < 0.7 else 0
            sx = tip if t >= 0.6 else 0
            for dx in range(-half, half + 1):
                edge = abs(dx) == half and half > 0
                c = RED if edge or t >= 0.85 else YELLOW if dx == 0 and t < 0.5 else ORANGE
                put(cx + dx + sx, y, c)
    # 1px 외곽선(바깥만)
    src = img.copy().load()
    for y in range(T):
        for x in range(T):
            if src[x, y][3]:
                continue
            if any(0 <= x + dx < T and 0 <= y + dy < T and src[x + dx, y + dy][3]
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                p[x, y] = OUT + (255,)
    return img


if __name__ == "__main__":
    sheet = Image.new("RGBA", (T * N, T))
    for i in range(N):
        sheet.alpha_composite(frame(i), (i * T, 0))
    sheet.save(sys.argv[1])
    print("saved", sys.argv[1])
