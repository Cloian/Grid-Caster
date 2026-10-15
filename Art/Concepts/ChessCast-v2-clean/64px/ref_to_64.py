"""고해상도 생성 시안을 64×64 캔버스 도트로 옮긴다(축소 대신 칸 다수결).

1. 원본의 모든 픽셀을 팔레트(정리된 초안의 12색 이하)로 양자화한다.
2. 64px 격자의 각 칸에서 가장 많은 색을 고른다. 외곽선 색이 38% 이상이면 외곽선을 우선해 선이 끊기지 않게 한다.
3. 작은 강조색(눈 등)은 칸의 20% 이상이면 그 색을 쓴다.
4. 그림의 긴 변을 62px에 맞추고 바닥에서 2px 위에 세운다.

실행: python3 ref_to_64.py <reference.png> <palette.png> <out.png> [max_side=62] [accent_min_share=0.2]
"""
import sys
from collections import Counter
import numpy as np
from PIL import Image

ref, palsrc, dst = sys.argv[1:4]
SIDE = int(sys.argv[4]) if len(sys.argv) > 4 else 62
ACCENT = float(sys.argv[5]) if len(sys.argv) > 5 else 0.2
img = Image.open(ref).convert('RGBA')
a = np.asarray(img).astype(int)
rgb, bg = a[:, :, :3], a[:, :, 3] < 128
pal_img = Image.open(palsrc).convert('RGBA')
counts = Counter(c[:3] for c in pal_img.getdata() if c[3])
pal = np.array(sorted(counts))
outi = int(pal.sum(1).argmin())
accents = {i for i, c in enumerate(pal) if counts[tuple(c)] <= 30}
lab = ((rgb[:, :, None, :] - pal[None, None]) ** 2).sum(3).argmin(2)
ys, xs = np.where(~bg)
y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
cell = max(x1 - x0, y1 - y0) / SIDE
TW, TH = int(round((x1 - x0) / cell)), int(round((y1 - y0) / cell))
out = Image.new('RGBA', (64, 64)); p = out.load()
ox, oy = (64 - TW) // 2, 62 - TH
for j in range(TH):
    for i in range(TW):
        sx0, sx1 = int(x0 + i * cell), max(int(x0 + (i + 1) * cell), int(x0 + i * cell) + 1)
        sy0, sy1 = int(y0 + j * cell), max(int(y0 + (j + 1) * cell), int(y0 + j * cell) + 1)
        m = bg[sy0:sy1, sx0:sx1]; l = lab[sy0:sy1, sx0:sx1][~m]
        if l.size < m.size * 0.5:
            continue
        cnt = Counter(l.tolist())
        k = cnt.most_common(1)[0][0]
        if cnt.get(outi, 0) >= 0.38 * l.size:
            k = outi
        acc = [c for c in accents if cnt.get(c, 0) >= ACCENT * l.size]
        if acc:
            k = max(acc, key=lambda c: cnt[c])
        p[ox + i, oy + j] = tuple(int(v) for v in pal[k]) + (255,)
out.save(dst)
print(dst, (TW, TH), 'cell %.2f' % cell)
