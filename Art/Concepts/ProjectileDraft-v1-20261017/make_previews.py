"""현재 투사체와 시안 3종을 같은 배율로 비교하는 이미지·GIF를 만든다.

- 8방향 보드: 3×3 사암 칸 가운데에서 바깥 8칸으로 날아가는 모습.
  현재 투사체는 게임처럼 한 장을 회전(대각선은 45° 최근접 회전)하고,
  시안은 right·diag 두 장을 뒤집기·90° 회전만으로 8방향을 만든다.
실행: python3 make_previews.py → preview/
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

from make_frames import DRAFTS

ROOT = Path(__file__).resolve().parent
FRAMES = ROOT / "frames"
OUT = ROOT / "preview"
CURRENT = ROOT / "reference"
TILE = 32
FLOOR = (0xC6, 0xA0, 0x7A)
DARK = tuple(int(c * m) for c, m in zip(FLOOR, (0.8, 0.85, 0.9)))
FONT = "/System/Library/Fonts/AppleSDGothicNeo.ttc"
TITLES = {
    "current": "현재 — 20px 3프레임(100ms), 대각선은 45° 회전",
    "A_comet": "A. 혜성탄 — 백열 구체와 가늘어지는 꼬리, 뒤로 흐르는 불티",
    "B_rune_dart": "B. 룬 화살 — 마름모 화살촉 주위를 룬 조각 두 개가 나선으로 돎",
    "C_arcane_orb": "C. 비전 구체 — 도는 초승달 광택과 보라 고리, 식어 가는 잔상 방울",
}
DIRECTIONS = [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1), (0, 1), (1, 1)]


def frames_of(name):
    if name == "current":
        return [Image.open(CURRENT / f"attack_{i}.png").convert("RGBA") for i in (1, 2, 3)]
    return [Image.open(FRAMES / f"{name}_right_{k}.png").convert("RGBA") for k in range(1, 5)]


def diag_frames(name):
    return [Image.open(FRAMES / f"{name}_diag_{k}.png").convert("RGBA") for k in range(1, 5)]


def oriented(name, k, dx, dy):
    """dy<0이 화면 위쪽."""
    if name == "current":
        img = frames_of(name)[k % 3]
        angle = {(1, 0): 0, (1, -1): 45, (0, -1): 90, (-1, -1): 135,
                 (-1, 0): 180, (-1, 1): 225, (0, 1): 270, (1, 1): 315}[(dx, dy)]
        return img.rotate(angle, resample=Image.NEAREST, expand=True)
    if dx != 0 and dy != 0:
        img = diag_frames(name)[k % 4]
        if dx < 0:
            img = img.transpose(Image.FLIP_LEFT_RIGHT)
        if dy > 0:
            img = img.transpose(Image.FLIP_TOP_BOTTOM)
        return img
    img = frames_of(name)[k % 4]
    return {(1, 0): img, (-1, 0): img.transpose(Image.FLIP_LEFT_RIGHT),
            (0, -1): img.transpose(Image.ROTATE_90), (0, 1): img.transpose(Image.ROTATE_270)}[(dx, dy)]


def board(name, k, scale=4):
    img = Image.new("RGBA", (TILE * 3, TILE * 3))
    for gy in range(3):
        for gx in range(3):
            img.paste((FLOOR if (gx + gy) % 2 == 0 else DARK) + (255,), (gx * TILE, gy * TILE, (gx + 1) * TILE, (gy + 1) * TILE))
    for dx, dy in DIRECTIONS:
        sprite = oriented(name, k, dx, dy)
        cx, cy = (1 + dx) * TILE + TILE // 2, (1 + dy) * TILE + TILE // 2
        img.alpha_composite(sprite, (cx - sprite.width // 2, cy - sprite.height // 2))
    return img.resize((img.width * scale, img.height * scale), Image.NEAREST)


def strip(name, scale=6):
    frames = frames_of(name) + ([] if name == "current" else diag_frames(name))
    w = TILE * scale
    out = Image.new("RGBA", (len(frames) * (w + 8) - 8, w), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        canvas = Image.new("RGBA", (TILE, TILE))
        canvas.alpha_composite(f, ((TILE - f.width) // 2, (TILE - f.height) // 2))
        out.alpha_composite(canvas.resize((w, w), Image.NEAREST), (i * (w + 8), 0))
    return out


def main():
    OUT.mkdir(exist_ok=True)
    font = ImageFont.truetype(FONT, 30)
    names = ["current"] + list(DRAFTS)
    rows = []
    for name in names:
        b = board(name, 0)
        s = strip(name)
        bg = Image.new("RGBA", (TILE * 6 * 8 + 7 * 8 + 40 + b.width, max(b.height, s.height) + 56), (0x1E, 0x16, 0x22, 255))
        d = ImageDraw.Draw(bg)
        d.text((0, 6), TITLES[name], font=font, fill=(0xF0, 0xD8, 0xAE))
        sb = Image.new("RGBA", s.size, FLOOR + (255,))
        sb.alpha_composite(s)
        bg.alpha_composite(sb, (0, 56))
        bg.alpha_composite(b, (bg.width - b.width, 56))
        rows.append(bg)
        count = 3 if name == "current" else 4
        loop = [board(name, k).convert("P", palette=Image.ADAPTIVE) for k in range(count)]
        loop[0].save(OUT / f"{name}-8dir.gif", save_all=True, append_images=loop[1:],
                     duration=100 if name == "current" else 50, loop=0, disposal=2)
        if name != "current":
            s.save(OUT / f"{name}-6x.png")
    sheet = Image.new("RGBA", (max(r.width for r in rows) + 64, sum(r.height for r in rows) + 32 * (len(rows) + 1)), (0x1E, 0x16, 0x22, 255))
    y = 32
    for r in rows:
        sheet.alpha_composite(r, (32, y))
        y += r.height + 32
    sheet.save(OUT / "overview.png")
    print("ok", sheet.size)


if __name__ == "__main__":
    main()
