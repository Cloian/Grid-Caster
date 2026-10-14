"""성장(강화·유물) 계열 아이콘 20×20. 기존 KnightMove 아이콘의 나무 테두리·돌 바탕을 그대로 쓰고
가운데 12×12 상징만 새로 찍는다. 상징 바깥 1px는 자동으로 검은 외곽선이 된다.

실행: python3 scripts/progression_icons.py <KnightMove.png> <out_dir>
"""
import sys
from pathlib import Path
from PIL import Image

BG = (0x75, 0x6F, 0x78, 255)
OUTLINE = (0x01, 0x01, 0x02, 255)
PAL = {
    "Y": (0xFB, 0xBE, 0x52), "O": (0xDF, 0x71, 0x26), "W": (0xF5, 0xF8, 0xFF),
    "C": (0x63, 0xD5, 0xEF), "B": (0x53, 0x8F, 0xE0), "P": (0x7E, 0x23, 0xC6),
    "p": (0xB0, 0x6A, 0xE8), "R": (0xC8, 0x2E, 0x3C), "r": (0xF0, 0x6A, 0x6A),
    "S": (0xC6, 0xD0, 0xDA), "s": (0x7F, 0x8C, 0x99), "K": (0x8A, 0x5A, 0x30),
    "k": (0xC9, 0x9A, 0x3E), "G": (0x6A, 0xBE, 0x30),
}

SYMBOLS = {
    # 빠른 시전·충전: 번개
    "Bolt": """
......YYYY..
.....YYYY...
....YYYY....
...YYYY.....
..YYYYYYYY..
.....YYYY...
....YYYY....
...YYYO.....
..YYYO......
..YYO.......
.YO.........
.O..........""",
    # 관통: 긴 바늘
    "Needle": """
..........WW
.........WCC
........WCC.
.......WCC..
......WCC...
.....WCC....
....WCC.....
...WCC......
..BCC.......
.BBC........
BBB.........
BB..........""",
    # 마나·충전량: 보석
    "ManaGem": """
....pppp....
...pWppPp...
..pWpppPPp..
.pWpppppPPp.
pppppppPPPPp
PPPPPPPPPPPP
.PPPPPPPPPP.
..PPPPPPPP..
...PPPPPP...
....PPPP....
.....PP.....
............""",
    # 넉백·밀치기: 충격 화살
    "Ram": """
......SS....
......SSS...
SSSSSSSSSS..
SWWWWWWWSSS.
SWWWWWWWWSSS
SWWWWWWWWSSS
SWWWWWWWSSS.
SSSSSSSSSS..
......SSS...
......SS....
............
............""",
    # 체력·회복: 하트
    "Heart": """
............
.rrr...rrr..
rWrrr.rrrrr.
rWrrrrrrrrR.
rrrrrrrrrrR.
rrrrrrrrrRR.
.rrrrrrrRR..
..rrrrrRR...
...rrrRR....
....rRR.....
.....R......
............""",
    # 방어·피해 경감: 방패
    "Shield": """
SSSSSSSSSSSS
SBBBBBBBBBBS
SBCCBBBBBBBS
SBCBBBYYBBBS
SBBBBYYYYBBS
SBBBBBYYBBBS
.SBBBBBBBBS.
.SBBBBBBBBS.
..SBBBBBBS..
...SBBBBS...
....SBBS....
.....SS.....""",
    # 이동 강화: 장화
    "Boot": """
...KKKKK....
...KkkkK....
...KKKKK....
...KKKKK....
...KKKKK....
...KKKKK....
...KKKKKK...
...KKKKKKKK.
..KKKKKKKKKK
..KKKKKKKKKK
..kkkkkkkkkk
............""",
    # 추가 행동·왕의 턴: 왕관
    "Crown": """
............
Y....Y....Y.
YY..YYY..YY.
YYY.YYY.YYY.
YYYYYYYYYYY.
YYYRYYYCYYY.
YYYYYYYYYYY.
OOOOOOOOOOO.
OYOYOYOYOYO.
OOOOOOOOOOO.
............
............""",
    # 표식·기습: 표적
    "Target": """
....RRRR....
..RRWWWWRR..
.RWW....WWR.
.RW..RR..WR.
RW..RWWR..WR
RW.RW..WR.WR
RW.RW..WR.WR
RW..RWWR..WR
.RW..RR..WR.
.RWW....WWR.
..RRWWWWRR..
....RRRR....""",
    # 특성 숙련: 별
    "Star": """
.....YY.....
.....YY.....
....YWYY....
....YWYY....
YYYYYYYYYYYY
.YYYYYYYYYO.
..YYYYYYYO..
...YYYYYO...
..YYYOYYYO..
..YYO..OYY..
.YO......OY.
.O........O.""",
}


def make(frame, art):
    icon = frame.copy()
    # 가운데를 돌 바탕으로 비운다 (테두리 2px는 유지)
    for y in range(2, 18):
        for x in range(3, 17):
            icon.putpixel((x, y), BG)
    rows = [r for r in art.strip("\n").splitlines()]
    ox, oy = 4, 4
    mask = set()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                icon.putpixel((ox + x, oy + y), PAL[ch] + (255,))
                mask.add((ox + x, oy + y))
    for (x, y) in list(mask):
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (x + dx, y + dy)
            if n not in mask and 3 <= n[0] <= 16 and 2 <= n[1] <= 17:
                icon.putpixel(n, OUTLINE)
    return icon


if __name__ == "__main__":
    frame = Image.open(sys.argv[1]).convert("RGBA")
    out = Path(sys.argv[2])
    out.mkdir(parents=True, exist_ok=True)
    for name, art in SYMBOLS.items():
        make(frame, art).save(out / f"{name}.png")
    print("saved", sorted(SYMBOLS))
