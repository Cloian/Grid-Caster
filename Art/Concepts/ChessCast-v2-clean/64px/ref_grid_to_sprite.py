"""확대된 도트 시안(검은 배경)을 원래 도트 격자로 되돌려 64×64 캔버스에 세운다.

1. 열·행 경계 강도에서 period 근처 간격의 경계선을 동적계획법으로 고른다(격자가 조금씩 어긋나도 따라간다).
2. 칸마다 안쪽 픽셀의 중앙값 색을 고른다.
3. 순수 검정 칸은 둘러싸여 있어도 배경(투명). 외곽선·얼굴은 아주 어두운 남색이라 남는다.
4. 12색 이하로 양자화한다(붉은 눈 같은 강조색은 군집에서 빼고 고정).
5. 그림을 64×64 캔버스 가운데, 바닥에서 2px 위에 세운다. 62px를 넘으면 칸 다수결로 줄인다.

실행: python3 ref_grid_to_sprite.py <in> <out.png> <period> [colors=12]
"""
import sys
from collections import Counter, deque
import numpy as np
from PIL import Image


def cuts(g, period):
    lo, hi = period * 0.72, period * 1.3
    n = len(g); g = g / g.max()
    best = np.full(n, -1e9); prev = np.full(n, -1, int)
    for i in range(0, int(hi) + 1): best[i] = g[i]
    for i in range(n):
        for d in range(int(np.ceil(lo)), int(hi) + 1):
            j = i - d
            if j >= 0:
                v = best[j] + g[i] - 0.08 * abs(d - period)
                if v > best[i]: best[i], prev[i] = v, j
    i = int(np.argmax(best[n - int(hi) - 1:]) + n - int(hi) - 1); out = []
    while i >= 0: out.append(i); i = prev[i]
    return np.array(out[::-1]) + 0.5


src, dst, period = sys.argv[1], sys.argv[2], float(sys.argv[3])
ncol = int(sys.argv[4]) if len(sys.argv) > 4 else 12
a = np.asarray(Image.open(src).convert('RGB')).astype(float)
xs = cuts(np.abs(np.diff(a, axis=1)).sum(2).sum(0), period)
ys = cuts(np.abs(np.diff(a, axis=0)).sum(2).sum(1), period)
gw, gh = len(xs) - 1, len(ys) - 1
cells = np.zeros((gh, gw, 3))
for j in range(gh):
    for i in range(gw):
        x0, x1 = int(np.ceil(xs[i] + 2)), int(np.floor(xs[i + 1] - 2))
        y0, y1 = int(np.ceil(ys[j] + 2)), int(np.floor(ys[j + 1] - 2))
        cells[j, i] = np.median(a[y0:max(y1, y0 + 1), x0:max(x1, x0 + 1)].reshape(-1, 3), axis=0)
black = cells.max(2) <= 6
bg = np.zeros((gh, gw), bool); q = deque()
for j in range(gh):
    for i in range(gw):
        if (i in (0, gw - 1) or j in (0, gh - 1)) and black[j, i]: bg[j, i] = True; q.append((i, j))
while q:
    i, j = q.popleft()
    for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        ni, nj = i + di, j + dj
        if 0 <= ni < gw and 0 <= nj < gh and not bg[nj, ni] and black[nj, ni]:
            bg[nj, ni] = True; q.append((ni, nj))
# 사슬·팔에 둘러싸인 배경도 순수 검정이다. 외곽선·얼굴은 어두운 남색이라 남는다.
bg |= black
red = (cells[..., 0] > 170) & (cells[..., 1] < 90) & ~bg
fg = cells[~bg & ~red]
rng = np.random.default_rng(1)
k = ncol - (1 if red.any() else 0)
cent = fg[rng.choice(len(fg), k, replace=False)]
for _ in range(80):
    lab = ((fg[:, None] - cent[None]) ** 2).sum(2).argmin(1)
    for c in range(k):
        if (lab == c).any(): cent[c] = fg[lab == c].mean(0)
cent = np.round(cent).astype(int)
img = Image.new('RGBA', (gw, gh)); p = img.load()
for j in range(gh):
    for i in range(gw):
        if red[j, i]: p[i, j] = (224, 36, 44, 255)
        elif not bg[j, i]:
            c = cent[((cells[j, i] - cent) ** 2).sum(1).argmin()]
            p[i, j] = (int(c[0]), int(c[1]), int(c[2]), 255)
img = img.crop(img.getbbox())
w, h = img.size
if max(w, h) > 62:
    s = 62 / max(w, h); img = img.resize((max(1, round(w * s)), max(1, round(h * s))), Image.NEAREST); w, h = img.size
out = Image.new('RGBA', (64, 64))
out.alpha_composite(img, ((64 - w) // 2, 62 - h))
out.save(dst)
print(dst, 'grid', (gw, gh), 'sprite', (w, h), 'colors', len({c for c in out.getdata() if c[3]}))
