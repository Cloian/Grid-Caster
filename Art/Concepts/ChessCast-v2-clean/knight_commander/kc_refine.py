"""기사단장 키포즈 다듬기 — 자동 변환(kc_keyposes.py) 결과를 재질별로 다시 칠한다.

자동 변환은 칸마다 다수결 색을 골라 금색·상아·청록이 한 픽셀씩 뒤섞이고 명암이 읽히지 않는다.
1. 색을 재질(금 갑옷 · 청록 천 · 말 갈색 · 외곽선)로 묶고, 재질 경계를 다수결로 정리한다(작은 섬 제거).
2. 재질마다 4단(밝음·빛·중간·그늘) 램프로 다시 칠한다. 원래 밝기를 3×3 평균으로 매끈하게 한 뒤
   왼쪽 위 빛을 기준으로 가장자리를 한 단계 밝게/어둡게 민다.
3. 1px 틈(꼬리·갈기 가닥 사이)을 메우고 실루엣 외곽선을 닫는다.
4. 투구와 방패는 자세마다 정한 자리에 손으로 그린 도장을 찍는다(STAMPS).
   기수 영역 밖(말 몸통)의 작은 재질 조각은 둘레 재질로 합쳐 얼룩을 없앤다.

실행: python3 kc_refine.py <keyposes 폴더>   (kc_*.png를 덮어쓴다)
"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image

OUTLINE = (15, 13, 26)
# 재질: 0 외곽선 1 금 장식 2 청록 천 3 말 갈색 4 상아 판금(기수 갑옷)
RAMPS = {
    1: [(0x8a, 0x4e, 0x22), (0xd0, 0x8c, 0x2c), (0xf2, 0xc2, 0x52), (0xfb, 0xf0, 0xcf)],
    2: [(0x10, 0x2c, 0x33), (0x1c, 0x4f, 0x53), (0x2c, 0x7c, 0x74), (0x52, 0xa8, 0x96)],
    3: [(0x3a, 0x22, 0x22), (0x5e, 0x39, 0x2c), (0x84, 0x55, 0x3a), (0xa8, 0x76, 0x4c)],
    4: [(0x8c, 0x86, 0x8e), (0xc4, 0xc0, 0xbc), (0xe8, 0xe4, 0xd8), (0xff, 0xfc, 0xf0)],
}
MATERIAL = {
    (0x0f, 0x0d, 0x1a): (0, 0), (0x16, 0x11, 0x1e): (0, 0), (0x2d, 0x20, 0x27): (3, 1),
    (0xec, 0xb8, 0x52): (1, 2), (0xa8, 0x69, 0x2a): (1, 1), (0xf8, 0xef, 0xd4): (4, 2),
    (0xd9, 0xb3, 0x69): (1, 2), (0xf1, 0xca, 0x78): (1, 2), (0xd6, 0xa9, 0x57): (1, 1), (0xc0, 0x8e, 0x46): (1, 1),
    (0x1d, 0x4d, 0x51): (2, 2), (0x19, 0x35, 0x39): (2, 1), (0x16, 0x37, 0x3c): (2, 1), (0x3d, 0x50, 0x4a): (2, 1),
    (0x74, 0x4a, 0x37): (3, 2), (0x4b, 0x31, 0x2e): (3, 1), (0x81, 0x72, 0x5d): (3, 1),
}
N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))
N8 = [(dx, dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if (dx, dy) != (0, 0)]


def nearest_material(c):
    best = min(MATERIAL, key=lambda k: sum((a - b) ** 2 for a, b in zip(k, c)))
    return MATERIAL[best]


def refine(img, key=None):
    a = np.asarray(img.convert("RGBA"))
    H, W = a.shape[:2]
    mat = np.full((H, W), -1)
    tone = np.zeros((H, W), float)
    for y in range(H):
        for x in range(W):
            if a[y, x, 3]:
                m, t = nearest_material(tuple(int(v) for v in a[y, x, :3]))
                mat[y, x], tone[y, x] = m, t

    def inside(x, y):
        return 0 <= x < W and 0 <= y < H

    # 3. 1px 틈 메우기(좌우 또는 위아래가 막힌 빈칸 → 말 갈색 그늘), 두 번
    for _ in range(2):
        fill = []
        for y in range(1, H - 1):
            for x in range(1, W - 1):
                if mat[y, x] >= 0:
                    continue
                if (mat[y, x - 1] >= 0 and mat[y, x + 1] >= 0) or (mat[y - 1, x] >= 0 and mat[y + 1, x] >= 0):
                    fill.append((x, y))
        for x, y in fill:
            mat[y, x], tone[y, x] = 3, 1
    # 꼬리 가닥 사이 2px 틈: 양옆(또는 위아래) 2칸 안에 말 갈색·외곽선이 있으면 갈색 그늘로, 두 번.
    # 꼬리는 모든 자세에서 왼쪽 아래(x<48, y>55)에 있다 — 창 자루 같은 가는 선은 메우지 않는다.
    def brownish(x, y):
        return inside(x, y) and mat[y, x] in (0, 3)
    for _ in range(2):
        fill = []
        for y in range(2, H - 2):
            for x in range(2, W - 2):
                if mat[y, x] >= 0 or x >= 48 or y <= 55:
                    continue
                horizontal = any(brownish(x - d, y) for d in (1, 2)) and any(brownish(x + d, y) for d in (1, 2))
                vertical = any(brownish(x, y - d) for d in (1, 2)) and any(brownish(x, y + d) for d in (1, 2))
                near_brown = sum(1 for dx in (-2, -1, 0, 1, 2) for dy in (-2, -1, 0, 1, 2) if inside(x + dx, y + dy) and mat[y + dy, x + dx] == 3)
                if (horizontal or vertical) and near_brown >= 4:
                    fill.append((x, y))
        for x, y in fill:
            mat[y, x], tone[y, x] = 3, 1
    # 갈색(꼬리·갈기) 덩어리 안의 외곽선 가닥은 갈색 그늘로 — 가닥이 체크무늬처럼 보이지 않게 한 덩어리로 묶는다
    src = mat.copy()
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            if src[y, x] != 0:
                continue
            nb = [src[y + dy, x + dx] for dx, dy in N8]
            if -1 not in nb and nb.count(3) >= 4:
                mat[y, x], tone[y, x] = 3, 0

    # 1. 재질 다수결(외곽선이 아닌 칸): 주변 8칸 중 5칸 이상이 다른 같은 재질이면 그 재질로, 두 번
    for _ in range(2):
        src = mat.copy()
        for y in range(1, H - 1):
            for x in range(1, W - 1):
                m = src[y, x]
                if m <= 0:
                    continue
                nb = [src[y + dy, x + dx] for dx, dy in N8]
                if -1 in nb:
                    continue
                vals = [v for v in nb if v > 0]
                if not vals:
                    continue
                best = max(set(vals), key=vals.count)
                if best != m and vals.count(best) >= 5:
                    mat[y, x] = best
                    tone[y, x] = 1.5
    # 외곽선 외톨이(이웃 외곽선 0개이고 안쪽)는 주변 재질로
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            if mat[y, x] == 0:
                nb = [mat[y + dy, x + dx] for dx, dy in N8]
                if -1 not in nb and nb.count(0) == 0:
                    vals = [v for v in nb if v > 0]
                    mat[y, x] = max(set(vals), key=vals.count)
                    tone[y, x] = 1.5

    # 상아 판금 조각이 3px 미만이면 금 장식의 밝은 면으로(흩뿌린 점처럼 보이지 않게)
    seen = np.zeros((H, W), bool)
    for y in range(H):
        for x in range(W):
            if mat[y, x] != 4 or seen[y, x]:
                continue
            comp, stack = [], [(x, y)]
            while stack:
                cx, cy = stack.pop()
                if not inside(cx, cy) or seen[cy, cx] or mat[cy, cx] != 4:
                    continue
                seen[cy, cx] = True
                comp.append((cx, cy))
                stack += [(cx + dx, cy + dy) for dx, dy in N4]
            if len(comp) < 3:
                for cx, cy in comp:
                    mat[cy, cx], tone[cy, cx] = 1, 2.5

    # 말 몸통 얼룩 정리: 기수 영역(방패 왼쪽 ~ 투구 오른쪽, 투구 위 ~ 방패 아래) 밖에서
    # 작은 재질 조각을 둘레에서 가장 많은 재질로 합친다. 큰 마갑(상아·금 판)은 남긴다.
    if key in STAMPS:
        (hx, hy), (sx0, sy0, sx1, sy1) = STAMPS[key]
        rider = (min(sx0, hx) - 1, hy - 2, max(sx1, hx + 7) + 3, sy1 + 4)
        limit = {1: 14, 2: 0, 3: 5, 4: 12}   # 청록(창 자루·마갑 천)은 합치지 않는다
        for _ in range(3):
            seen = np.zeros((H, W), bool)
            changed = False
            for y in range(H):
                for x in range(W):
                    m = mat[y, x]
                    if m <= 0 or seen[y, x]:
                        continue
                    comp, stack = [], [(x, y)]
                    while stack:
                        cx, cy = stack.pop()
                        if not inside(cx, cy) or seen[cy, cx] or mat[cy, cx] != m:
                            continue
                        seen[cy, cx] = True
                        comp.append((cx, cy))
                        stack += [(cx + dx, cy + dy) for dx, dy in N4]
                    if len(comp) >= limit[m]:
                        continue
                    if all(rider[0] <= cx <= rider[2] and rider[1] <= cy <= rider[3] for cx, cy in comp):
                        continue
                    border = [mat[cy + dy, cx + dx] for cx, cy in comp for dx, dy in N4
                              if inside(cx + dx, cy + dy) and mat[cy + dy, cx + dx] > 0 and mat[cy + dy, cx + dx] != m]
                    if not border:
                        continue
                    best = max(set(border), key=border.count)
                    for cx, cy in comp:
                        mat[cy, cx], tone[cy, cx] = best, 1.5
                    changed = True
            if not changed:
                break
        # 합친 뒤 경계를 한 번 더 다수결로 둥글게(기수 영역 밖만)
        src = mat.copy()
        for y in range(1, H - 1):
            for x in range(1, W - 1):
                m = src[y, x]
                if m <= 0 or (rider[0] <= x <= rider[2] and rider[1] <= y <= rider[3]):
                    continue
                vals = [src[y + dy, x + dx] for dx, dy in N8]
                if -1 in vals:
                    continue
                vals = [v for v in vals if v > 0]
                best = max(set(vals), key=vals.count) if vals else m
                if best != m and vals.count(best) >= 5:
                    mat[y, x], tone[y, x] = best, 1.5

    # 2. 명암: 같은 재질 이웃끼리 원래 밝기 평균 → 3단 양자화 → 빛 방향 가장자리 보정
    out = Image.new("RGBA", (W, H))
    p = out.load()
    level = np.zeros((H, W), int)
    for y in range(H):
        for x in range(W):
            m = mat[y, x]
            if m < 0:
                continue
            if m == 0:
                p[x, y] = OUTLINE + (255,)
                continue
            vals = [tone[y + dy, x + dx] for dx in (-1, 0, 1) for dy in (-1, 0, 1)
                    if inside(x + dx, y + dy) and mat[y + dy, x + dx] == m]
            t = sum(vals) / len(vals)
            lv = 0 if t < 1.0 else 1 if t < 1.8 else 2
            up_left = inside(x - 1, y - 1) and mat[y - 1, x - 1] == m
            up = inside(x, y - 1) and mat[y - 1, x] == m
            left = inside(x - 1, y) and mat[y, x - 1] == m
            down_right = inside(x + 1, y + 1) and mat[y + 1, x + 1] == m
            down = inside(x, y + 1) and mat[y + 1, x] == m
            if not up and not up_left and lv >= 1:
                lv += 1                                      # 빛 받는 윗면(위·왼쪽 위가 모두 다른 재질)
            elif not down and not down_right:
                lv -= 1                                      # 아래 그늘
            level[y, x] = max(0, min(3, lv))
    # 한 픽셀짜리 명암 섬 정리
    for y in range(1, H - 1):
        for x in range(1, W - 1):
            m = mat[y, x]
            if m <= 0:
                continue
            nb = [level[y + dy, x + dx] for dx, dy in N4 if mat[y + dy, x + dx] == m]
            if len(nb) >= 3 and level[y, x] not in nb:
                level[y, x] = max(set(nb), key=nb.count)
    for y in range(H):
        for x in range(W):
            m = mat[y, x]
            if m > 0:
                p[x, y] = RAMPS[m][level[y, x]] + (255,)
    # 실루엣 외곽선 닫기(1px 가는 선 끝은 그대로)
    src = out.copy().load()
    for y in range(H):
        for x in range(W):
            if not src[x, y][3]:
                continue
            nb = [src[x + dx, y + dy] if inside(x + dx, y + dy) else (0, 0, 0, 0) for dx, dy in N4]
            if any(n[3] == 0 for n in nb) and sum(1 for n in nb if n[3]) >= 2:
                p[x, y] = OUTLINE + (255,)
    return out


# ---------------- 손으로 그린 투구·방패 ----------------
# 자동 변환으로는 얼굴(투구 눈구멍)과 방패 문장이 깨진다. 자세마다 위치를 정해 손으로 그린 도장을 찍는다.
# 투구(오른쪽을 봄, 7×8): O 외곽선 · H/L/M/D 상아 4단 · G 금 띠 · . 그대로 둠
HELM = [
    "..OOOO.",
    ".OHHLLO",
    "OHHLLMO",
    "OLLGGGO",
    "OLLOOOO",
    "OLLMOMO",
    ".OMMMDO",
    "..OOOO.",
]
HELM_COLORS = {"O": OUTLINE, "H": RAMPS[4][3], "L": RAMPS[4][2], "M": RAMPS[4][1], "D": RAMPS[4][0], "G": RAMPS[1][2]}
# 자세별 투구 왼쪽 위, 방패 테두리 상자(x0, y0, x1, y1)
STAMPS = {
    "01": ((62, 55), (40, 64, 54, 84)),
    "02": ((67, 57), (49, 64, 62, 82)),
    "03": ((69, 42), (72, 58, 85, 79)),
    "04": ((55, 41), (32, 53, 46, 75)),
    "05": ((59, 51), (47, 66, 59, 84)),
    "06": ((57, 41), (41, 56, 57, 77)),
    "07": ((49, 51), (38, 64, 51, 85)),
    "08": ((63, 47), (44, 63, 58, 84)),
}


def shield_mask(w, h):
    """연 모양 방패: 위 55%는 폭 그대로(위 모서리만 깎음), 아래는 뾰족하게 좁아진다."""
    inside = set()
    for r in range(h):
        t = r / (h - 1)
        hw = w / 2 if t < 0.55 else max(0.6, w / 2 * (1 - (t - 0.55) / 0.45) ** 0.8)
        if r == 0:
            hw -= 1.5
        for x in range(w):
            if abs(x + 0.5 - w / 2) <= hw:
                inside.add((x, r))
    return inside


def stamp_shield(p, box):
    x0, y0, x1, y1 = box
    w, h = x1 - x0 + 1, y1 - y0 + 1
    inside = shield_mask(w, h)
    edge = {c for c in inside if any((c[0] + dx, c[1] + dy) not in inside for dx, dy in N4)}
    rim = {c for c in inside - edge if any((c[0] + dx, c[1] + dy) in edge for dx, dy in N4)}
    mid = w // 2
    cross_y = round(h * 0.33)
    for (x, r) in inside:
        if (x, r) in edge:
            c = OUTLINE
        elif (x, r) in rim:
            c = RAMPS[1][3] if (x < mid and r < h * 0.5) else RAMPS[1][2] if x < mid or r < h * 0.3 else RAMPS[1][1]
        else:
            # 문장: 세로 막대 + 가로 막대 + 위쪽 마름모 끝
            emblem = (x in (mid - 1, mid) and 4 <= r <= h - 7) or (r == cross_y and abs(x + 0.5 - w / 2) <= 3)
            if emblem:
                c = RAMPS[1][2] if x < mid else RAMPS[1][1]
            else:
                c = RAMPS[2][2] if x < mid - 1 else RAMPS[2][1] if r < h * 0.7 else RAMPS[2][0]
        p[x0 + x, y0 + r] = c + (255,)


def stamp_helm(p, top_left):
    hx, hy = top_left
    # 투구 바로 아래 남은 상아 덩어리(옛 얼굴)는 청록 목도리로 — 머리가 길쭉해 보이지 않게
    for r in range(8, 11):
        for x in range(-1, 8):
            c = p[hx + x, hy + r]
            if c[3] and c[:3] in RAMPS[4]:
                p[hx + x, hy + r] = (RAMPS[2][2] if x < 3 else RAMPS[2][1]) + (255,)
    for r, row in enumerate(HELM):
        for x, ch in enumerate(row):
            if ch != ".":
                p[hx + x, hy + r] = HELM_COLORS[ch] + (255,)


# 창 자루가 말 갈색으로 칠해진 자세: 자루 선(두 끝점) 1.8px 안의 갈색을 같은 단계의 청록으로 — 자루를 한 색으로.
SPEAR_FIX = {"06": ((66, 54.3), (104, 37.2))}


def recolor_spear(p, line):
    (ax, ay), (bx, by) = line
    lx, ly = bx - ax, by - ay
    length2 = lx * lx + ly * ly
    for y in range(int(min(ay, by)) - 2, int(max(ay, by)) + 3):
        for x in range(int(ax) - 2, int(bx) + 3):
            c = p[x, y]
            if not c[3] or c[:3] not in RAMPS[3]:
                continue
            t = max(0.0, min(1.0, ((x - ax) * lx + (y - ay) * ly) / length2))
            if ((ax + lx * t - x) ** 2 + (ay + ly * t - y) ** 2) ** 0.5 <= 1.8:
                p[x, y] = RAMPS[2][RAMPS[3].index(c[:3])] + (255,)


def hand_fix(img, key):
    out = img.copy()
    p = out.load()
    helm, shield = STAMPS[key]
    stamp_shield(p, shield)
    stamp_helm(p, helm)
    if key in SPEAR_FIX:
        recolor_spear(p, SPEAR_FIX[key])
    return out


if __name__ == "__main__":
    for f in sorted(Path(sys.argv[1]).glob("kc_0*.png")):
        img = hand_fix(refine(Image.open(f), f.stem[3:5]), f.stem[3:5])
        img.save(Path(sys.argv[2]) / f.name if len(sys.argv) > 2 else f)
        print(f.name)
