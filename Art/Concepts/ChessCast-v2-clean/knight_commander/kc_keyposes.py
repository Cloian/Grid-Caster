"""기사단장 핵심 자세 8장(1254px 생성 시안) → 게임 도트(칸당 32px 밀도, 3×3 점유 기마상).

1. 기준 디자인(KnightCommander-v1 컨셉)에서 배경을 뺀 픽셀로 14색 팔레트를 만든다(모든 자세가 같은 팔레트).
2. 모든 자세에 같은 칸 크기를 쓰고(시안 한 변 1254px → 100px, 3×3 칸 폭을 채움), 칸마다 가장 많은 색을 고른다.
   외곽선(가장 어두운 색)이 칸의 18% 이상이면 외곽선을 우선하고, 청록 깃·보석 같은 작은 강조색은 20% 이상이면 살린다.
3. 말발굽의 가장 아래 줄을 y=CANVAS-6에, 몸통(창을 뺀 굵은 덩어리) 가운데를 캔버스 가운데에 맞춘다.
4. 외톨이 픽셀 정리, 전경 가장자리에 1px 외곽선.
5. 판 바닥과 비슷한 흐린 색을 진한 색으로, 작은 구멍 채우기, 실루엣 외곽선 닫기(solidify).

실행: python3 kc_keyposes.py <시안 폴더> <기준 컨셉 png> <출력 폴더>
"""
import sys
from collections import Counter
from pathlib import Path
import numpy as np
from PIL import Image

CANVAS = 128
CELL = 1254 / 100.0    # 창·꼬리까지 3×3 칸(96px) 폭을 채우는 크기
FOOT_Y = CANVAS - 6


def background_mask(a, tol=20):
    """가장자리에서 이어진 배경만 뺀다. 외곽선·말 그늘의 진한 갈색은 배경 보라와 가까워서
    색 거리만으로 빼면 몸 안에 구멍이 난다. 가장자리부터 이어진 칸만 배경으로 본다."""
    corners = np.concatenate([a[:24, :24].reshape(-1, 3), a[:24, -24:].reshape(-1, 3), a[-24:, :24].reshape(-1, 3)])
    bg = np.median(corners, axis=0)
    near = np.abs(a - bg).sum(2) < tol
    h, w = near.shape
    seen = np.zeros_like(near)
    stack = [(y, x) for y in (0, h - 1) for x in range(w)] + [(y, x) for x in (0, w - 1) for y in range(h)]
    while stack:
        y, x = stack.pop()
        if seen[y, x] or not near[y, x]:
            continue
        seen[y, x] = True
        if y > 0: stack.append((y - 1, x))
        if y < h - 1: stack.append((y + 1, x))
        if x > 0: stack.append((y, x - 1))
        if x < w - 1: stack.append((y, x + 1))
    return seen


def build_palette(concept, colors=14):
    a = np.asarray(Image.open(concept).convert("RGB")).astype(int)
    fg = a[~background_mask(a)]
    img = Image.fromarray(fg.reshape(1, -1, 3).astype(np.uint8))
    q = img.quantize(colors=colors, method=Image.Quantize.MEDIANCUT)
    pal = np.array(q.getpalette()[:colors * 3]).reshape(-1, 3)
    counts = Counter(np.asarray(q).ravel().tolist())
    return pal, counts


def convert(path, pal, counts):
    a = np.asarray(Image.open(path).convert("RGB")).astype(int)
    bg = background_mask(a)
    lab = ((a[:, :, None, :] - pal[None, None]) ** 2).sum(3).argmin(2)
    out_i = int(pal.sum(1).argmin())
    total = sum(counts.values())
    accents = {i for i in range(len(pal)) if counts.get(i, 0) < total * 0.012}
    n = int(round(1254 / CELL))
    grid = np.full((n, n), -1)
    for j in range(n):
        for i in range(n):
            # 칸 가장자리 22%는 이웃 도트와 섞이므로 가운데 부분만 본다(경계 잡티 감소)
            inset = CELL * 0.22
            x0, x1 = int(i * CELL + inset), int((i + 1) * CELL - inset)
            y0, y1 = int(j * CELL + inset), int((j + 1) * CELL - inset)
            m = bg[y0:y1, x0:x1]
            l = lab[y0:y1, x0:x1][~m]
            if l.size < m.size * 0.45:
                continue
            cnt = Counter(l.tolist())
            k = cnt.most_common(1)[0][0]
            if cnt.get(out_i, 0) >= 0.18 * l.size:
                k = out_i
            acc = [c for c in accents if cnt.get(c, 0) >= 0.2 * l.size]
            if acc:
                k = max(acc, key=lambda c: cnt[c])
            grid[j, i] = k
    grid = despeckle(grid, out_i, accents)
    grid = despeckle(grid, out_i, accents, strict=True)   # 같은 색 이웃이 1개뿐인 가는 꼬리도 정리
    grid = close_outline(grid, out_i)
    # 기준점: 가장 아래 전경 줄 = 발굽, 몸통 가운데 = 전경이 10칸 넘는 줄들의 가로 중심
    rows = np.where((grid >= 0).sum(1) > 0)[0]
    bottom = rows.max()
    wide = [j for j in range(n) if (grid[j] >= 0).sum() > 15]
    cols = np.where((grid[wide] >= 0).any(0))[0] if wide else np.where((grid >= 0).any(0))[0]
    cx = (cols.min() + cols.max()) / 2
    out = Image.new("RGBA", (CANVAS, CANVAS))
    p = out.load()
    dx = int(round(CANVAS / 2 - cx))
    dy = FOOT_Y - bottom
    for j in range(n):
        for i in range(n):
            if grid[j, i] >= 0 and 0 <= i + dx < CANVAS and 0 <= j + dy < CANVAS:
                p[i + dx, j + dy] = tuple(int(v) for v in pal[grid[j, i]]) + (255,)
    return out


def despeckle(g, out_i, accents, strict=False):
    h, w = g.shape
    out = g.copy()
    for y in range(h):
        for x in range(w):
            k = g[y, x]
            if k < 0 or k == out_i or k in accents:
                continue
            nb = [g[y + dy, x + dx] for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)) if 0 <= y + dy < h and 0 <= x + dx < w]
            if nb.count(k) < (2 if strict else 1) and not (strict and nb.count(k) == 1 and nb.count(out_i) >= 2):
                cnt = Counter(v for v in nb if v >= 0)
                if cnt:
                    out[y, x] = cnt.most_common(1)[0][0]
    return out


def close_outline(g, out_i):
    h, w = g.shape
    out = g.copy()
    for y in range(h):
        for x in range(w):
            if g[y, x] < 0:
                continue
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if not (0 <= yy < h and 0 <= xx < w) or g[yy, xx] < 0:
                    out[y, x] = out_i
                    break
    return out


# ---------------- 도트 정리 ----------------
# 비슷한 금색을 3단계(밝음·중간·그늘)로 합치고 드문 색을 가까운 색으로 보낸다.
MERGE = {
    (0xe3, 0xb1, 0x53): (0xf0, 0xbe, 0x59),
    (0xe0, 0xba, 0x70): (0xf0, 0xbe, 0x59),
    (0x3d, 0x50, 0x4a): (0x16, 0x37, 0x3c),
}


def tidy(img, passes=2):
    """색 합치기 + 다수결 정리: 외곽선이 아닌 칸이 주변 8칸 중 5칸 이상과 다른 같은 색에 둘러싸이면 그 색으로 바꾼다."""
    out = img.copy()
    p = out.load()
    W, H = out.size
    for y in range(H):
        for x in range(W):
            c = p[x, y]
            if c[3] and c[:3] in MERGE:
                p[x, y] = MERGE[c[:3]] + (255,)
    outline = min((c[:3] for c in out.getdata() if c[3]), key=sum)
    for _ in range(passes):
        src = out.copy().load()
        for y in range(1, H - 1):
            for x in range(1, W - 1):
                c = src[x, y]
                if not c[3]:
                    continue
                nb = [src[x + dx, y + dy] for dx in (-1, 0, 1) for dy in (-1, 0, 1) if (dx, dy) != (0, 0)]
                if any(n[3] == 0 for n in nb):
                    continue                                   # 실루엣 가장자리는 건드리지 않는다
                if c[:3] == outline:
                    # 안쪽에 혼자 떨어진 외곽선 점(이웃 외곽선 1개 이하)은 갑옷 면을 쪼개므로 주변 색으로
                    if sum(1 for n in nb if n[:3] == outline) <= 1:
                        cnt = Counter(n[:3] for n in nb if n[:3] != outline)
                        p[x, y] = cnt.most_common(1)[0][0] + (255,)
                    continue
                cnt = Counter(n[:3] for n in nb if n[:3] != outline)
                if not cnt:
                    continue
                best, k = cnt.most_common(1)[0]
                if best != c[:3] and k >= 5:
                    p[x, y] = best + (255,)
    return out


# ---------------- 판 위에서 꽉 차 보이게 ----------------
# 판 바닥(밝은 황토 #c4a076 · 어두운 #96805f)과 비슷한 흐린 금색·회갈색은 몸이 비쳐 보이게 만든다.
# 흐린 금색은 진한 금색과 짙은 금 그늘로, 회갈색은 말의 어두운 갈색으로 보낸다.
SOLID = {
    (0xd9, 0xb3, 0x69): (0xec, 0xb8, 0x52),
    (0xf1, 0xca, 0x78): (0xec, 0xb8, 0x52),
    (0xd6, 0xa9, 0x57): (0xa8, 0x69, 0x2a),
    (0xc0, 0x8e, 0x46): (0xa8, 0x69, 0x2a),
    (0x81, 0x72, 0x5d): (0x4b, 0x31, 0x2e),
}
HOLE_FILL = (0x2d, 0x20, 0x27)


def solidify(img, max_hole=10):
    """색 정리 + 작은 빈 구멍 채우기 + 실루엣 가장자리 외곽선 닫기(1px 가는 선의 끝은 그대로)."""
    out = img.copy()
    p = out.load()
    W, H = out.size
    for y in range(H):
        for x in range(W):
            c = p[x, y]
            if c[3] and c[:3] in SOLID:
                p[x, y] = SOLID[c[:3]] + (255,)
    # 바깥과 이어지지 않은 작은 투명 구멍(다리 사이 같은 큰 틈은 남긴다)
    outside = set()
    stack = [(0, 0)]
    while stack:
        x, y = stack.pop()
        if not (0 <= x < W and 0 <= y < H) or (x, y) in outside or p[x, y][3]:
            continue
        outside.add((x, y))
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    seen = set()
    for y in range(H):
        for x in range(W):
            if p[x, y][3] or (x, y) in outside or (x, y) in seen:
                continue
            region, stack = [], [(x, y)]
            while stack:
                cx, cy = stack.pop()
                if (cx, cy) in seen or p[cx, cy][3]:
                    continue
                seen.add((cx, cy))
                region.append((cx, cy))
                stack += [(cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)]
            if len(region) <= max_hole:
                for cx, cy in region:
                    p[cx, cy] = HOLE_FILL + (255,)
    outline = min((c[:3] for c in out.getdata() if c[3]), key=sum)
    src = out.copy().load()
    for y in range(H):
        for x in range(W):
            if not src[x, y][3] or src[x, y][:3] == outline:
                continue
            nb = [src[x + dx, y + dy] if 0 <= x + dx < W and 0 <= y + dy < H else (0, 0, 0, 0)
                  for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            if any(n[3] == 0 for n in nb) and sum(1 for n in nb if n[3]) >= 2:
                p[x, y] = outline + (255,)
    return out


def main(src, concept, dst):
    dst = Path(dst)
    dst.mkdir(parents=True, exist_ok=True)
    pal, counts = build_palette(concept)
    Image.fromarray(np.repeat(pal.reshape(1, -1, 3), 8, 0).astype(np.uint8)).resize((len(pal) * 16, 16), Image.NEAREST).save(dst / "palette.png")
    for f in sorted(Path(src).glob("0*_*.png")):
        img = convert(f, pal, counts)
        img.save(dst / f"raw_{f.stem}.png")
        solidify(tidy(img)).save(dst / f"kc_{f.stem}.png")
        print(f.name)


if __name__ == "__main__":
    main(*sys.argv[1:4])
