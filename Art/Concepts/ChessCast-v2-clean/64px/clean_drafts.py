"""생성 이미지를 축소한 도트 초안(ChessCastDraft-v1)을 바이블 기준으로 정리한다.

디자인(형태·팔레트·구성)은 유지하고 축소 흔적만 없앤다.
1. 외톨이 픽셀: 4방향 이웃과 모두 다른 색이면 8방향 이웃 다수색으로 합친다(외곽선 경계는 보존).
2. 2px 미만 조각: 같은 색 연결 덩어리가 min_cluster 미만이면 주변 다수색으로 흡수한다.
3. 외곽선: 투명과 닿는 바깥 픽셀은 외곽선 색, 안쪽에 고립된 외곽선 점은 주변색으로.
4. 투명 구멍(1px) 메우기, 이진 알파 유지.

실행: python3 clean_drafts.py <in.png> <out.png> [min_cluster]
"""
import sys
from collections import Counter, deque
from PIL import Image

OUT = (0x0E, 0x0C, 0x1E)
N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))
PROTECTED = set()  # 눈 같은 적은 강조색과 가장 어두운 형태선 색은 정리하지 않는다
N8 = N4 + ((1, 1), (1, -1), (-1, 1), (-1, -1))


def load(path):
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    px = img.load()
    grid = [[px[x, y][:3] if px[x, y][3] >= 128 else None for x in range(w)] for y in range(h)]
    return grid, w, h


def save(grid, w, h, path):
    img = Image.new("RGBA", (w, h))
    px = img.load()
    for y in range(h):
        for x in range(w):
            if grid[y][x] is not None:
                px[x, y] = grid[y][x] + (255,)
    img.save(path)


def neighbors(grid, w, h, x, y, dirs):
    for dx, dy in dirs:
        nx, ny = x + dx, y + dy
        if 0 <= nx < w and 0 <= ny < h:
            yield grid[ny][nx]
        else:
            yield None


def is_edge(grid, w, h, x, y):
    return any(n is None for n in neighbors(grid, w, h, x, y, N4))


def fill_holes(grid, w, h):
    for y in range(h):
        for x in range(w):
            if grid[y][x] is None:
                nb = [n for n in neighbors(grid, w, h, x, y, N4)]
                if all(n is not None for n in nb):
                    grid[y][x] = Counter(nb).most_common(1)[0][0]


def remove_isolated(grid, w, h):
    changed = 0
    snap = [row[:] for row in grid]
    for y in range(h):
        for x in range(w):
            c = snap[y][x]
            if c is None or c == OUT or c in PROTECTED:
                continue
            n4 = [n for n in neighbors(snap, w, h, x, y, N4) if n is not None]
            if n4 and all(n != c for n in n4):
                n8 = [n for n in neighbors(snap, w, h, x, y, N8) if n is not None and n != OUT]
                if n8:
                    grid[y][x] = Counter(n8).most_common(1)[0][0]
                    changed += 1
    return changed


def absorb_small_clusters(grid, w, h, min_cluster):
    seen = [[False] * w for _ in range(h)]
    changed = 0
    for y in range(h):
        for x in range(w):
            c = grid[y][x]
            if c is None or c == OUT or c in PROTECTED or seen[y][x]:
                continue
            cells, q = [], deque([(x, y)])
            seen[y][x] = True
            while q:
                cx, cy = q.popleft()
                cells.append((cx, cy))
                for dx, dy in N4:
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and grid[ny][nx] == c:
                        seen[ny][nx] = True
                        q.append((nx, ny))
            if len(cells) >= min_cluster:
                continue
            around = Counter()
            for cx, cy in cells:
                for n in neighbors(grid, w, h, cx, cy, N4):
                    if n is not None and n != c and n != OUT:
                        around[n] += 1
            if around:
                target = around.most_common(1)[0][0]
                for cx, cy in cells:
                    grid[cy][cx] = target
                    changed += 1
    return changed


def fix_outline(grid, w, h):
    snap = [row[:] for row in grid]
    for y in range(h):
        for x in range(w):
            c = snap[y][x]
            if c is None:
                continue
            # 바깥 가장자리만 외곽선으로 맞춘다. 안쪽 어두운 선은 형태 구분선이라 보존한다.
            if is_edge(snap, w, h, x, y):
                grid[y][x] = OUT


def clean(src, dst, min_cluster=3):
    global OUT
    grid, w, h = load(src)
    # 외곽선 색 = 투명과 닿는 픽셀 중 가장 많은 색(초안마다 다르다)
    edge = Counter(grid[y][x] for y in range(h) for x in range(w)
                   if grid[y][x] is not None and is_edge(grid, w, h, x, y))
    OUT = edge.most_common(1)[0][0]
    counts = Counter(c for row in grid for c in row if c is not None)
    PROTECTED.clear()
    PROTECTED.update(c for c, n in counts.items() if n <= 30)            # 강조색(눈·보석 등)
    PROTECTED.update(sorted(counts, key=lambda c: sum(c))[:2])         # 가장 어두운 형태선 색
    fill_holes(grid, w, h)
    for _ in range(3):
        remove_isolated(grid, w, h)
    absorb_small_clusters(grid, w, h, min_cluster)
    remove_isolated(grid, w, h)
    fix_outline(grid, w, h)
    save(grid, w, h, dst)


if __name__ == "__main__":
    clean(sys.argv[1], sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 3)
