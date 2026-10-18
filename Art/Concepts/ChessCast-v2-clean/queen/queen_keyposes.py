"""여왕 핵심 자세 8장(1254px 생성 시안) → 120×120 게임 여왕 픽셀로 옮긴다.

1. 시안 배경(어두운 보라)과 가까운 픽셀은 배경으로 본다.
2. 나머지 픽셀을 기존 여왕(queen_boss.png)의 16색으로 양자화한다.
3. 모든 자세에 같은 칸 크기(서 있는 1번 자세의 키가 기존 여왕 키 112px가 되는 크기)를 쓰고,
   칸마다 가장 많은 색을 고른다. 외곽선 색이 칸의 35% 이상이면 외곽선을 우선해 선이 끊기지 않게 하고,
   보석 청록 같은 작은 강조색은 칸의 20% 이상이면 살린다.
4. 발(망토 끝)을 기존 여왕과 같은 줄(y=115)에, 가운데를 x=60에 맞춘다. 떠오르는 7번만 생성 시안의 높이 차이를 유지한다.
5. 외톨이 픽셀(같은 색 이웃 0개, 외곽선 제외)은 이웃 다수 색으로 정리한다.
6. 머리 이식: 생성 시안은 머리가 작고 흰 머리·얼굴이 뭉개지므로, 기존 여왕의 왕관·머리·얼굴(턱까지)을
   왕관 보석 위치를 맞춰 그대로 옮겨 심는다(캐릭터 동일성). 8번 탈진은 이식 뒤 왕관을 1~2px 기울인다.

실행: python3 queen_keyposes.py <시안 폴더> <출력 폴더>
"""
import sys
from collections import Counter
from pathlib import Path
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
S = 120
FOOT_Y = 115
STAND_HEIGHT = 112

base = Image.open(HERE.parent / "queen_boss.png").convert("RGBA")
counts = Counter(c[:3] for c in base.getdata() if c[3])
PAL = np.array(sorted(counts))
OUT_I = int(PAL.sum(1).argmin())
ACCENTS = {i for i, c in enumerate(PAL) if counts[tuple(c)] <= 20}


def load(path):
    a = np.asarray(Image.open(path).convert("RGB")).astype(int)
    bgc = np.median(np.concatenate([a[:20, :20].reshape(-1, 3), a[:20, -20:].reshape(-1, 3)]), axis=0)
    bg = np.abs(a - bgc).sum(2) < 45
    lab = ((a[:, :, None, :] - PAL[None, None]) ** 2).sum(3).argmin(2)
    return a, bg, lab


def figure_box(bg):
    ys, xs = np.where(~bg)
    # 흩어진 잡티를 빼기 위해 행/열마다 전경이 3px 넘는 범위만 본다
    rows = np.where((~bg).sum(1) > 3)[0]
    cols = np.where((~bg).sum(0) > 3)[0]
    return cols.min(), cols.max() + 1, rows.min(), rows.max() + 1


def convert(path, cell, keep_float=0):
    a, bg, lab = load(path)
    x0, x1, y0, y1 = figure_box(bg)
    tw, th = int(round((x1 - x0) / cell)), int(round((y1 - y0) / cell))
    grid = np.full((th, tw), -1)
    for j in range(th):
        for i in range(tw):
            sx0, sx1 = int(x0 + i * cell), max(int(x0 + (i + 1) * cell), int(x0 + i * cell) + 1)
            sy0, sy1 = int(y0 + j * cell), max(int(y0 + (j + 1) * cell), int(y0 + j * cell) + 1)
            m = bg[sy0:sy1, sx0:sx1]
            l = lab[sy0:sy1, sx0:sx1][~m]
            if l.size < m.size * 0.45:
                continue
            cnt = Counter(l.tolist())
            k = cnt.most_common(1)[0][0]
            if cnt.get(OUT_I, 0) >= 0.35 * l.size:
                k = OUT_I
            acc = [c for c in ACCENTS if cnt.get(c, 0) >= 0.2 * l.size]
            if acc:
                k = max(acc, key=lambda c: cnt[c])
            grid[j, i] = k
    grid = despeckle(grid)
    grid = close_outline(grid)
    out = Image.new("RGBA", (S, S))
    p = out.load()
    ox = 60 - tw // 2
    oy = FOOT_Y - th - keep_float
    for j in range(th):
        for i in range(tw):
            if grid[j, i] >= 0 and 0 <= ox + i < S and 0 <= oy + j < S:
                p[ox + i, oy + j] = tuple(int(v) for v in PAL[grid[j, i]]) + (255,)
    return out, (tw, th)


def despeckle(g):
    h, w = g.shape
    out = g.copy()
    for y in range(h):
        for x in range(w):
            k = g[y, x]
            if k < 0 or k == OUT_I or k in ACCENTS:
                continue
            nb = [g[y + dy, x + dx] for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1))
                  if 0 <= y + dy < h and 0 <= x + dx < w]
            if k not in nb:
                cnt = Counter(v for v in nb if v >= 0)
                if cnt:
                    out[y, x] = cnt.most_common(1)[0][0]
    return out


def close_outline(g):
    """전경 가장자리(투명과 맞닿은 칸)는 외곽선 색으로 → 기존 여왕처럼 1px 외곽선이 끊기지 않게."""
    h, w = g.shape
    out = g.copy()
    for y in range(h):
        for x in range(w):
            if g[y, x] < 0:
                continue
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if not (0 <= yy < h and 0 <= xx < w) or g[yy, xx] < 0:
                    out[y, x] = OUT_I
                    break
    return out


GEM = (18, 190, 214)
HEAD_BOTTOM = 41          # 기존 여왕에서 이 줄까지(왕관·머리카락·얼굴)를 머리로 본다
COLLAR = (42, 50, 47, 73) # 그 아래 턱·높은 옷깃·브로치(가운데 x47~73, y42~50)도 옮긴다


def gem_center(img):
    pts = [(x, y) for y in range(S) for x in range(S) if img.getpixel((x, y))[:3] == GEM and img.getpixel((x, y))[3]]
    if not pts:
        return None
    return (sum(x for x, _ in pts) / len(pts), sum(y for _, y in pts) / len(pts))


# 표정: 기존 여왕 얼굴(원본 좌표)에 덧칠한다. 공격 자세는 찌푸린 눈썹·빛나는 눈·드러난 이, 탈진은 감은 눈.
OUTLINE, EYE_CORE, EYE_GLOW, TEETH = (7, 19, 27), (255, 255, 255), (130, 236, 255), (250, 233, 204)
FACES = {
    "fierce": [
        # 눈썹: 바깥이 올라가고 안쪽이 눈 위로 내려오는 사선(왼쪽 52~57, 오른쪽 63~68)
        (52, 36, OUTLINE), (53, 36, OUTLINE), (54, 37, OUTLINE), (55, 37, OUTLINE), (56, 37, OUTLINE), (57, 38, OUTLINE),
        (68, 36, OUTLINE), (67, 36, OUTLINE), (66, 37, OUTLINE), (65, 37, OUTLINE), (64, 37, OUTLINE), (63, 38, OUTLINE),
        # 눈: 흰 심지 + 밝은 청록
        (55, 38, EYE_GLOW), (55, 39, EYE_CORE), (56, 39, EYE_GLOW), (55, 40, EYE_GLOW),
        (64, 38, EYE_GLOW), (64, 39, EYE_CORE), (63, 39, EYE_GLOW), (64, 40, EYE_GLOW),
        # 입: 다문 입술 사이로 이
        (59, 42, OUTLINE), (60, 42, OUTLINE), (61, 42, OUTLINE), (62, 42, OUTLINE),
        (60, 43, TEETH), (61, 43, TEETH), (59, 43, OUTLINE), (62, 43, OUTLINE),
    ],
    "tired": [
        # 감은 눈: 눈 자리를 아래로 처진 선으로
        (55, 38, (243, 238, 221)), (55, 39, OUTLINE), (56, 39, OUTLINE), (55, 40, (250, 233, 204)), (54, 39, OUTLINE),
        (64, 38, (243, 238, 221)), (64, 39, OUTLINE), (63, 39, OUTLINE), (64, 40, (250, 233, 204)), (65, 39, OUTLINE),
        (60, 42, OUTLINE), (61, 42, OUTLINE), (59, 43, OUTLINE), (62, 43, OUTLINE),
    ],
}
FACE_OF = {"02": "fierce", "03": "fierce", "04": "fierce", "05": "fierce", "06": "fierce", "07": "fierce", "08": "tired"}


def head_source(face):
    if face is None:
        return base
    img = base.copy()
    for x, y, c in FACES[face]:
        img.putpixel((x, y), c + (255,))
    return img


def transplant_head(img, tilt=0, face=None):
    head = head_source(face)
    src = head.load()
    og = gem_center(head)
    cg = gem_center(img)
    if cg is None:
        # 보석이 사라졌으면 머리 꼭대기(가장 위 불투명 줄)의 가운데로 맞춘다
        ys = [y for y in range(S) for x in range(S) if img.getpixel((x, y))[3]]
        top = min(ys)
        xs = [x for x in range(S) if img.getpixel((x, top))[3]]
        cg = (sum(xs) / len(xs), og[1] + top - 4)
    dx, dy = round(cg[0] - og[0]), round(cg[1] - og[1])
    mask = {(x, y) for y in range(HEAD_BOTTOM + 1) for x in range(S) if src[x, y][3]}
    y0c, y1c, x0c, x1c = COLLAR
    mask |= {(x, y) for y in range(y0c, y1c + 1) for x in range(x0c, x1c + 1) if src[x, y][3]}
    grown = {(x + ex, y + ey) for x, y in mask for ex in (-1, 0, 1) for ey in (-1, 0, 1)}
    out = img.copy()
    p = out.load()
    for x, y in grown:
        X, Y = x + dx, y + dy
        if 0 <= X < S and 0 <= Y < S and (y <= HEAD_BOTTOM - 4 or (y0c <= y <= y1c and x0c < x < x1c)):
            p[X, Y] = (0, 0, 0, 0)       # 시안의 작은 왕관·머리 자리를 비운다(턱 아래 옷깃은 둔다)
    for x, y in mask:
        X, Y = x + dx, y + dy
        if tilt:
            X += 0 if y > 28 else (1 if y > 16 else 2) * tilt
        if 0 <= X < S and 0 <= Y < S:
            p[X, Y] = src[x, y]
    # 위로 잘리면 전체를 내린다
    top = min((y for y in range(S) for x in range(S) if out.getpixel((x, y))[3]), default=0)
    if top < 2:
        moved = Image.new("RGBA", (S, S))
        moved.paste(out, (0, 2 - top), out)
        out = moved
    return out


# 손 보정: 생성 시안의 손이 작아 외곽선만 뭉친 자세는 손을 직접 찍어 덮는다. (왼쪽 위 x, y, 행들)
HAND_FIX = {
    "04": (52, 81, [
        "..#####..",
        ".#ccCCk#.",
        "#cccCCkk#",
        "#cccCCkk#",
        "#ccCCCkk#",
        ".#cCCCk#.",
        ".#ccCkk#.",
        "..#cCk#..",
        "...###...",
    ]),
}
FIX_PAL = {"#": (7, 19, 27), "c": (250, 233, 204), "C": (243, 238, 221), "k": (230, 210, 184)}


def fix_hand(img, code):
    """뭉친 손(크림·외곽선)을 넓게 지우고, 지운 자리는 둘레의 옷 색으로 메운 뒤 손을 새로 찍는다."""
    if code not in HAND_FIX:
        return img
    x0, y0, rows = HAND_FIX[code]
    out = img.copy()
    p = out.load()
    w, h = len(rows[0]), len(rows)
    holes = set()
    for y in range(y0 - 4, y0 + h + 7):
        for x in range(x0 - 6, x0 + w + 5):
            c = p[x, y]
            if c[3] and (c[:3] in (FIX_PAL["#"], FIX_PAL["c"], FIX_PAL["C"], FIX_PAL["k"]) or sum(c[:3]) > 600):
                holes.add((x, y))
    # 드레스 앞판(상아)은 손 상자 오른쪽 바깥에서 이어지므로, 상자 오른쪽 열의 상아는 건드리지 않는다
    holes = {(x, y) for x, y in holes if x < x0 + w - 1 or p[x, y][:3] == FIX_PAL["#"]}
    for x, y in holes:
        p[x, y] = (0, 0, 0, 0)
    # 손이 옮겨 가면서 드러난 옷 사이 틈(좌우가 모두 막힌 투명 칸)도 메울 대상에 넣는다
    for y in range(y0 - 4, y0 + h + 7):
        row = [x for x in range(x0 - 6, x0 + w + 5) if p[x, y][3]]
        if row:
            for x in range(min(row), max(row)):
                if not p[x, y][3]:
                    holes.add((x, y))
    for _ in range(40):                                  # 둘레 옷 색으로 바깥에서부터 메운다
        filled = []
        for x, y in holes:
            if p[x, y][3]:
                continue
            nb = [p[x + dx, y + dy] for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
                  if p[x + dx, y + dy][3] and p[x + dx, y + dy][:3] != FIX_PAL["#"]]   # 외곽선 색으로는 메우지 않는다
            if len(nb) >= (2 if _ < 20 else 1):
                filled.append(((x, y), Counter(c[:3] for c in nb).most_common(1)[0][0]))
        for (x, y), c in filled:
            p[x, y] = c + (255,)
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch != ".":
                p[x0 + i, y0 + j] = FIX_PAL[ch] + (255,)
    return out


# 머리 위로 든 손: 생성 시안은 1px 선으로 끊겨 뭉개 보이므로, 소맷단(손목)에서 손끝까지 4px 팔뚝 + 펼친 손을 새로 찍는다.
# (손목 x, y), (손 아래 끝 x, y), 좌우 뒤집기 — 머리 이식 뒤 최종 좌표.
RAISED_FIX = {
    "03": [((40, 22), (36, 12), False)],
    "07": [((39, 25), (36, 14), False), ((81, 25), (84, 14), True)],
}
RAISED_HAND = [
    ".#.#.#.",
    "#c#c#k#",
    "#c#C#k#",
    "#cCCCk#",
    "#cCCCk##",
    "#ccCCkc#",
    ".#cCCk#.",
]
RAISED_PAL = {"#": (7, 19, 27), "c": (250, 233, 204), "C": (243, 238, 221), "k": (230, 210, 184)}


def fix_raised(img, code):
    if code not in RAISED_FIX:
        return img
    out = img.copy()
    p = out.load()
    skin = {RAISED_PAL["c"], RAISED_PAL["C"], RAISED_PAL["k"], (199, 173, 150)}
    for (wx, wy), (tx, ty), mirror in RAISED_FIX[code]:
        x0, x1 = min(wx, tx) - 5, max(wx, tx) + 5
        y0 = ty - len(RAISED_HAND) - 1
        # 손·팔뚝 자리의 끊긴 선(살색·외곽선)을 지운다. 소맷단(금·청록)은 둔다.
        for y in range(max(0, y0), wy + 1):
            for x in range(x0, x1 + 1):
                c = p[x, y]
                if c[3] and (c[:3] in skin or c[:3] == RAISED_PAL["#"]):
                    p[x, y] = (0, 0, 0, 0)
        # 팔뚝: 손목에서 손 아래 끝까지 행마다 4px(외곽선·살·살·그늘·외곽선)
        for y in range(ty, wy + 1):
            t = (y - ty) / max(1, wy - ty)
            cx = round(tx + (wx - tx) * t)
            row = ["#", "c", "C", "k", "#"]
            if mirror:
                row = ["#", "k", "C", "c", "#"]
            for i, ch in enumerate(row):
                xx = cx - 2 + i
                if not (p[xx, y][3] and ch == "#" and p[xx, y][:3] not in skin):
                    p[xx, y] = RAISED_PAL[ch] + (255,)
        # 손: 아래 끝이 ty-1에 오도록, 가운데가 tx
        rows = [r[::-1].replace("c", "_").replace("k", "c").replace("_", "k") for r in RAISED_HAND] if mirror else RAISED_HAND
        for j, row in enumerate(rows):
            for i, ch in enumerate(row):
                if ch != ".":
                    p[tx - 3 + i, ty - len(rows) + j] = RAISED_PAL[ch] + (255,)
    return out


def main(src, dst):
    src, dst = Path(src), Path(dst)
    dst.mkdir(parents=True, exist_ok=True)
    files = sorted(src.glob("0*_*.png"))
    _, bg1, _ = load(files[0])
    bx0, bx1, by0, by1 = figure_box(bg1)
    cell = (by1 - by0) / STAND_HEIGHT
    print("cell %.2f px" % cell)
    for f in files:
        keep = 0
        if f.name.startswith("07"):
            keep = 3   # 떠오르는 자세: 발이 바닥에서 3px 떠 있다
        img, size = convert(f, cell, keep)
        img.save(dst / f"raw_{f.stem}.png")
        img = fix_hand(img, f.name[:2])
        img = transplant_head(img, tilt=1 if f.name.startswith("08") else 0, face=FACE_OF.get(f.name[:2]))
        img = fix_raised(img, f.name[:2])
        img.save(dst / f"key_{f.stem}.png")
        print(f.name, size)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
