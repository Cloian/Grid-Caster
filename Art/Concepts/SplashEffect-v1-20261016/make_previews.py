"""frames/의 시안을 5×5 사암 바닥(폭발 증폭 2단계, 16칸)에 올린 비교 이미지와 GIF를 만든다.

주변 칸은 실제 게임처럼 파편이 도착하는 만큼(약 80ms ≈ 1프레임) 늦게 시작한다.
실행: python3 make_previews.py  → preview/
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

import sys
from make_frames import DRAFTS, frames_dir

PALETTE = sys.argv[1] if len(sys.argv) > 1 else "warm"

ROOT = Path(__file__).resolve().parent
FRAMES = frames_dir(PALETTE)
OUT = ROOT / ("preview" if PALETTE == "warm" else f"preview_{PALETTE}")
TILE, GRID, SCALE = 32, 5, 3
FLOOR = (0xC6, 0xA0, 0x7A)
DARK = tuple(int(c * m) for c, m in zip(FLOOR, (0.8, 0.85, 0.9)))  # GridManager 체크무늬와 같은 곱
FONT = "/System/Library/Fonts/AppleSDGothicNeo.ttc"
TITLES = {
    "A_heat_ring": "A. 열파 고리 — 둥근 열파가 번지고 주변 칸 테두리가 달아올랐다 잉걸로 흩어짐",
    "B_brand_sigil": "B. 낙인 문양 — 칸마다 마름모 문양이 찍혀 맞은 칸이 또렷하고, 식으면 눈금만 남음",
    "C_shard_strike": "C. 파편 착탄 — 지금의 잔여 마력 파편이 칸마다 꽂혀 부서지고 그을음을 남김",
}


def in_area(dx, dy):
    x, y = abs(dx), abs(dy)
    r = max(x, y)
    return r == 1 or (r == 2 and (x == 0 or y == 0 or (x == 2 and y == 2)))


def frame(name, tag, i):
    if not 1 <= i <= 6:
        return None
    return Image.open(FRAMES / f"{name}_{tag}_{i}.png").convert("RGBA")


def board(name, step):
    img = Image.new("RGBA", (TILE * GRID, TILE * GRID))
    for gy in range(GRID):
        for gx in range(GRID):
            color = FLOOR if (gx + gy) % 2 == 0 else DARK
            img.paste(color + (255,), (gx * TILE, gy * TILE, (gx + 1) * TILE, (gy + 1) * TILE))
    for gy in range(GRID):
        for gx in range(GRID):
            dx, dy = gx - 2, gy - 2
            f = frame(name, "center", step) if (dx, dy) == (0, 0) else (
                frame(name, "splash", step - 1) if in_area(dx, dy) else None)
            if f is not None:
                img.alpha_composite(f, (gx * TILE, gy * TILE))
    return img.resize((img.width * SCALE, img.height * SCALE), Image.NEAREST)


def main():
    OUT.mkdir(exist_ok=True)
    font = ImageFont.truetype(FONT, 30)
    small = ImageFont.truetype(FONT, 22)
    steps = range(1, 8)
    cell = TILE * GRID * SCALE
    gap, head = 16, 56
    rows = []
    for name in DRAFTS:
        panels = [board(name, s) for s in steps]
        frames = [p.convert("P", palette=Image.ADAPTIVE) for p in panels] + [board(name, 99).convert("P", palette=Image.ADAPTIVE)]
        frames[0].save(OUT / f"{name}-board.gif", save_all=True, append_images=frames[1:],
                       duration=[60] * 7 + [500], loop=0, disposal=2)
        row = Image.new("RGBA", (len(panels) * (cell + gap) - gap, cell + head + 34), (0x1E, 0x16, 0x22, 255))
        d = ImageDraw.Draw(row)
        d.text((0, 8), TITLES[name], font=font, fill=(0xF0, 0xD8, 0xAE))
        for i, p in enumerate(panels):
            x = i * (cell + gap)
            row.alpha_composite(p, (x, head))
            d.text((x, head + cell + 4), f"{(steps[i] - 1) * 60}ms", font=small, fill=(0xB0, 0xAA, 0xAC))
        rows.append(row)
        # 칸 하나 기준 6배 스트립(center 위, splash 아래)
        strip = Image.new("RGBA", (6 * TILE * 6 + 5 * 12, 2 * TILE * 6 + 12), (0, 0, 0, 0))
        for r, tag in enumerate(("center", "splash")):
            for i in range(1, 7):
                f = frame(name, tag, i).resize((TILE * 6, TILE * 6), Image.NEAREST)
                strip.alpha_composite(f, ((i - 1) * (TILE * 6 + 12), r * (TILE * 6 + 12)))
        strip.save(OUT / f"{name}-6x.png")
    sheet = Image.new("RGBA", (rows[0].width + 64, sum(r.height for r in rows) + 32 * (len(rows) + 1)), (0x1E, 0x16, 0x22, 255))
    y = 32
    for r in rows:
        sheet.alpha_composite(r, (32, y))
        y += r.height + 32
    sheet.save(OUT / "overview.png")
    print("ok", sheet.size)


if __name__ == "__main__":
    main()
