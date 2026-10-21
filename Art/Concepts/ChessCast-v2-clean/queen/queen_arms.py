"""여왕 팔(소매) 손 찍기 템플릿 + 정수 기울이기.

기본은 오른쪽(화면 오른쪽)으로 뻗은 수평 소매 한 장을 직접 찍어 두고,
- 기울기: 열마다 정수 칸만큼 위/아래로 민다(열 단위 shear) → 1:1, 2:1 계단 대각선
- 수직(위로 든 팔): 템플릿을 90° 돌린 뒤(픽셀 그대로) 행 단위로 밀어 바깥으로 기울인다
- 왼팔: 좌우 뒤집기
원본 여왕 소매와 같은 색 규칙: 위(빛) l → t → T → d(아래 그늘), 금 소맷단 y/g/G/b, 손 c/C/k.
"""
from PIL import Image

PAL = {
    "#": (7, 19, 27), "l": (36, 125, 133), "t": (12, 99, 109), "T": (6, 69, 77), "d": (4, 42, 50),
    "y": (255, 207, 104), "g": (242, 183, 66), "G": (206, 144, 41), "b": (157, 102, 29),
    "c": (250, 233, 204), "C": (243, 238, 221), "k": (214, 194, 170),
}

# 오른쪽으로 뻗은 소매. 어깨(붙는 점)는 A, 손 중심은 H 로 표시(그 칸은 각각 # / c 로 칠한다).
OUT = [
    # 원래 늘어뜨린 팔과 같은 길이(어깨→손 약 22px). 넓은 소매는 팔 아래로 처져 금 소맷단이 세로로 열린다.
    # 손은 엄지가 위로 선 펼친 손(손가락이 바깥을 향함).
    "A###.....................",
    "#lll#####................",
    "#tllllllll####...###.....",
    "#ttttttttllll#y##cc#.....",
    "#Tttttttttttt#yg#ccC####.",
    "#TTtttttttttt#yg#cHCCCc#.",
    "#TTTttttttttt#gG#cCCCkk#.",
    "#dTTTTttttttT#gG#ckkkk#..",
    "#ddTTTTTttTTT#gG######...",
    ".#ddTTTTTTTTT#GG#........",
    "..#dddTTTTTTT#Gb#........",
    "...##ddddTTTT#bb#........",
    ".....###ddddd#b#.........",
    "........######.#.........",
]

# 손을 뺀 수평 소매. W = 손목(소맷단 바깥 외곽선, 손 템플릿의 W와 겹친다). 기울일 때는 소매만 밀고 손은 똑바로 다시 붙인다.
OUT_SLEEVE = [
    "A###.............",
    "#lll#####........",
    "#tllllllll####...",
    "#ttttttttllll#y#.",
    "#Tttttttttttt#yg#",
    "#TTtttttttttt#ygW",
    "#TTTttttttttt#gG#",
    "#dTTTTttttttT#gG#",
    "#ddTTTTTttTTT#gG#",
    ".#ddTTTTTTTTT#GG#",
    "..#dddTTTTTTT#Gb#",
    "...##ddddTTTT#bb#",
    ".....###ddddd#b#.",
    "........######.#.",
]

# 손 템플릿(W = 손목 외곽선 칸, H = 손 중심)
HAND_OUT = [
    ".###.....",
    "#cc#.....",
    "#ccC####.",
    "WcHCCCc#.",
    "#cCCCkk#.",
    "#ckkkk#..",
    "######...",
]
HAND_UP45 = [
    "....##.",
    "...#cc#",
    "..#cCc#",
    ".#cHCk#",
    "#cCCk#.",
    "Wckk#..",
    "###....",
]
HAND_DOWN27 = [
    "W#####...",
    "#ccCCk#..",
    "#cHCCkk#.",
    ".#cCCkk#.",
    "..#ck#k#.",
    "...#.#.#.",
]

# 아래 바깥으로 내리꽂는 오른팔(약 60°): 소매가 팔을 따라 아래로 처지고, 소맷단 아래로 손이 아래-바깥을 가리킨다.
DOWN = [
    "A##..................",
    "#ll##................",
    "#tlll##..............",
    "#ttllll##............",
    "#Tttttlll##..........",
    "#TTttttttll##........",
    "#TTTtttttttll#.......",
    ".#TTTTttttttl#.......",
    ".#dTTTTtttttt#.......",
    ".#ddTTTTtttttl#......",
    "..#ddTTTTttttt#......",
    "..#dddTTTTtttt#......",
    "...#dddTTTTTtt#......",
    "...#ddddTTTTTTl#.....",
    "....#dddddTTTTT#.....",
    "....#yyggggGGGb#.....",
    ".....#yggGGGbb##.....",
    "......###########....",
    ".......#ccCCk#.......",
    ".......#cHCCk##......",
    "........#cCCkk#......",
    "........#cc#ck#......",
    ".........#k##k#......",
    "..........#..#.......",
]


# 머리 위로 든 오른팔: 넓은 소매가 어깨 쪽으로 흘러내려 팔꿈치에 모이고, 크림색 팔뚝과 손이 위로 드러난다.
# A = 어깨(아래 왼쪽), H = 손 중심.
UP = [
    "...#.#.#......",
    "..#c#c#c#.....",
    "..#c#C#C##....",
    "..#cccCCk#....",
    "..#ccHCCk##...",
    "...#ccCCkc#...",
    "...#cccCk#....",
    "....#cCCk#....",
    "....#ccCk#....",
    "....#ccCk#....",
    "....#cCCk#....",
    "....#ccCk#....",
    "....#ccCk#....",
    "....#cCCk#....",
    "....#yyggG#...",
    "...#yyggGGb#..",
    "...#lltttTd#..",
    "...#lttttTd#..",
    "..#llttttTTd#.",
    "..#ltttttTTd#.",
    ".#lltttttTTdd#",
    ".#ltttttTTTdd#",
    ".#ltttttTTTdd#",
    "#lltttttTTTdd#",
    "#ltttttTTTTdd#",
    "#lttttTTTTddd#",
    "#ltttTTTTTddd#",
    "#lttTTTTTdddd#",
    ".#ltTTTTddddd#",
    ".#lTTTTdddd##.",
    "A#TTTTddd##...",
    "##TTddd##.....",
    ".####.........",
]


# 가슴 앞으로 모으는 오른팔: 소매는 아래로 처진 채 팔꿈치를 굽혀 손이 몸 가운데로 온다.
# 어깨점 A가 오른쪽 위, 손 H가 왼쪽 아래(몸 가운데). 왼팔은 뒤집으면 두 손이 가운데서 만난다.
GATHER = [
    ".........###A",
    "........#lll#",
    ".......#llltt#",
    "......#llttttT#",
    "......#lttttTT#",
    ".....#lltttTTTd#",
    ".....#ltttTTTTd#",
    "....#lltttTTTdd#",
    "....#ltttTTTTdd#",
    "...#yyggGGGbbb#.",
    "..#yygggGGbb##..",
    ".#ccC####bb#....",
    "#ccHCCk#.##.....",
    "#ccCCkk#........",
    ".#kkk##.........",
    "..###...........",
]


def thicken(rows, extra=3):
    """소매 몸통 줄(5~8행)을 정수 배로 한 번 더 넣어 두껍게 한다(픽셀 그대로 복제)."""
    out = []
    for i, r in enumerate(rows):
        out.append(r)
        if 5 <= i < 5 + extra:
            out.append(r.replace("A", "#").replace("H", "c"))
    return out


WRIST = [None]


def parse(rows):
    WRIST[0] = None
    w = max(len(r) for r in rows)
    pix, anchor, hand = {}, None, None
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == "A":
                anchor, ch = (x, y), "#"
            if ch == "H":
                hand, ch = (x, y), "c"
            if ch == "W":
                WRIST[0], ch = (x, y), "#"
            if ch != ".":
                pix[(x, y)] = PAL[ch]
    return pix, anchor, hand


def shear_cols(pix, k):
    """열 x를 round(x*k)만큼 위로(+k) 민다."""
    return {(x, y - round(x * k)): c for (x, y), c in pix.items()}


def rot_up(pix):
    """오른쪽을 향한 팔을 위를 향하게 90° 돌린다(x→-y, y→x). 빛(위쪽 l)이 왼쪽 면이 된다."""
    return {(y, -x): c for (x, y), c in pix.items()}


def shear_rows(pix, k):
    """행 y를 round(-y*k)만큼 오른쪽으로(위로 갈수록 바깥) 민다."""
    return {(x + round(-y * k), y): c for (x, y), c in pix.items()}


SWAP = {}
for a_, b_ in (("l", "d"), ("t", "T"), ("y", "b"), ("g", "G"), ("c", "k")):
    SWAP[PAL[a_]], SWAP[PAL[b_]] = PAL[b_], PAL[a_]
VERTICAL = {"up", "up70", "gather", "down45"}


def arm(kind):
    """kind → (픽셀 dict, 어깨점, 손점) — 오른팔 기준 좌표."""
    if kind in ("up", "up70"):
        pix, a, h = parse(UP)
    elif kind == "down45":
        pix, a, h = parse(DOWN)
    elif kind == "gather":
        pix, a, h = parse(GATHER)
    else:
        # 소매(손목 W까지)만 먼저 만들고, 기울인 뒤 손을 똑바로 붙인다
        pix, a, _ = parse(OUT_SLEEVE)
        w = WRIST[0]
        k = {"out": 0.0, "up27": 0.5, "up45": 1.0, "down27": -0.5}[kind]
        pix = shear_cols(pix, k)
        a = (a[0], a[1] - round(a[0] * k))
        w = (w[0], w[1] - round(w[0] * k))
        hand_rows = {"out": HAND_OUT, "up27": HAND_OUT, "up45": HAND_UP45, "down27": HAND_DOWN27}[kind]
        hp, _, hh = parse(hand_rows)
        hw = WRIST[0]
        for (x, y), c in hp.items():
            pix[(x - hw[0] + w[0], y - hw[1] + w[1])] = c
        hand = (hh[0] - hw[0] + w[0], hh[1] - hw[1] + w[1])
        return pix, a, hand
    if kind == "up70":
        pix = shear_rows(pix, 0.3)
        a = (a[0] + round(-a[1] * 0.3), a[1])
        h = (h[0] + round(-h[1] * 0.3), h[1])
    return pix, a, h


def paste_arm(img, kind, shoulder, side, drop=0):
    """img(RGBA 120px)에 팔을 붙인다. side=+1 오른팔, -1 왼팔(좌우 뒤집기). 손 위치를 돌려준다."""
    pix, a, h = arm(kind)
    if side < 0 and kind in VERTICAL:
        # 좌우로 뒤집으면 빛 받는 면이 바깥(오른쪽)으로 가므로 밝은/어두운 색을 맞바꾼다(빛: 왼쪽 위)
        pix = {xy: SWAP.get(c, c) for xy, c in pix.items()}
    p = img.load()
    W, H = img.size
    sx, sy = shoulder[0], shoulder[1] + drop
    for (x, y), c in pix.items():
        X = sx + (x - a[0]) * side
        Y = sy + (y - a[1])
        if 0 <= X < W and 0 <= Y < H:
            p[X, Y] = c + (255,)
    return (sx + (h[0] - a[0]) * side, sy + (h[1] - a[1]))


if __name__ == "__main__":
    kinds = ["out", "up27", "up45", "up70", "up", "down27", "down45"]
    sheet = Image.new("RGBA", (60 * len(kinds), 60), (18, 30, 42, 255))
    for i, k in enumerate(kinds):
        paste_arm(sheet, k, (i * 60 + 12, 30), +1)
    sheet.resize((sheet.width * 6, sheet.height * 6), Image.NEAREST).save("arm_templates.png")
