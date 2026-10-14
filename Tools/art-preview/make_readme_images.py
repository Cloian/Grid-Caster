"""README용 아트 미리보기 이미지를 저장소 에셋만으로 다시 만든다.

- docs/images/board-art-preview.png: SampleScene 바닥 배치를 재현한 보드 + 캐릭터 + 발밑 그림자 합성
- docs/images/character-motions.png: 마법사·박쥐·뱀의 동작별 대표 프레임

게임 캡처가 아니라 에셋 합성이다. 실행: python3 Tools/art-preview/make_readme_images.py (Pillow 필요)
"""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
TILES = ROOT / "Assets/TIles"
OUT = ROOT / "docs/images"
T = 32
BG = (30, 22, 34, 255)          # 카메라 배경색 #1E1622
SHADOW_CORE = (30, 22, 34, 128)
SHADOW_RIM = (30, 22, 34, 61)
DARK = (0.8, 0.85, 0.9)         # GridManager 체크무늬 곱

# 바닥 타일 = tileset2.aseprite와 같은 픽셀을 쓰는 미리보기용 PNG가 없으므로 시트에서 직접 읽는다.
SPRITES = {
    0: ("tileset2", 0, 32), 1: ("tileset2", 32, 32), 2: ("tileset2", 64, 32), 3: ("tileset2", 96, 32),
    4: ("wall-Sheet22.png", 64, 64), 10: ("wall-Sheet22.png", 128, 64),
    5: ("wall_sheet123.png", 64, 64), 6: ("wall_sheet123.png", 128, 64),
    7: ("wall_sheet.png", 0, 64), 8: ("wall_sheet.png", 64, 64),
    11: ("Sprite-tile2-Sheet.png", 0, 32), 12: ("Sprite-tile2-Sheet.png", 64, 32),
    13: ("Sprite-tile2-Sheet.png", 96, 32), 14: ("Sprite-tile2-Sheet.png", 32, 32),
}
LAYOUT = """
40 70 80 80 80 80 80 80 80 80 80 80 80 80 100
50 142 111 111 111 111 111 111 111 111 111 111 111 123 60
50 110 00 00 00 31 00 00 00 20 00 00 22 112 60
50 110 20 00 00 00 00 00 00 00 00 00 00 112 60
50 110 00 00 10 00 00 00 00 00 00 20 00 112 60
50 110 00 00 00 00 00 22 00 00 00 00 00 112 60
50 110 22 00 00 00 00 00 00 10 00 00 00 112 60
50 110 00 00 00 00 00 00 00 00 00 00 00 112 60
50 110 00 00 32 00 00 00 00 00 00 00 00 112 60
50 110 00 00 00 00 00 00 00 31 00 00 32 112 60
50 110 00 00 20 00 00 00 00 00 00 00 00 112 60
50 110 31 00 00 00 00 00 00 00 00 00 00 112 60
50 110 00 00 00 10 00 00 00 00 20 00 00 112 60
50 130 113 113 113 113 113 113 113 113 113 113 113 120 60
40 80 80 80 80 80 80 80 80 80 80 80 80 80 100
"""
ROTATE = {0: 0, 1: -90, 2: 180, 3: 90}

# (시트, 프레임 크기, PPU, 그림자 폭, 높이, x, y) — CharacterShadow 프로필과 같은 값
CHARS = {
    "wizard": ("Assets/Player/Lively/wizard_idle.png", 50, 32, 24, 6, 0, -16),
    "bat": ("Assets/Monsters/Bat/Lively/bat_idle.png", 40, 40, 14, 4, 0, -16),
    "snake": ("Assets/Monsters/Snake/Lively/snake_idle.png", 40, 40, 30, 6, 0, -13),
}


def floor_sheet():
    # tileset2는 Aseprite 원본이라 Pillow로 읽지 못한다. 같은 픽셀을 내보낸 PNG를 스크립트 옆에 둔다.
    # 바닥을 다시 그리면 Aseprite로 Assets/TIles/tileset2.aseprite를 이 경로에 다시 내보낸다.
    return Image.open(Path(__file__).with_name("tileset2-export.png")).convert("RGBA")


def tile(index, cache={}):
    if index not in cache:
        name, x, w = SPRITES[index]
        img = floor_sheet() if name == "tileset2" else Image.open(TILES / name).convert("RGBA")
        part = img.crop((x, img.height - w, x + w, img.height))
        cache[index] = part.resize((T, T), Image.NEAREST) if w != T else part
    return cache[index]


def board():
    out = Image.new("RGBA", (15 * T, 15 * T), (0, 0, 0, 0))
    for row, items in enumerate(LAYOUT.strip().splitlines()):
        for col, code in enumerate(items.split()):
            img = tile(int(code[:-1])).rotate(ROTATE[int(code[-1])])
            if 1 <= col <= 13 and 1 <= row <= 13 and ((col - 8) + (6 - row)) % 2:
                px = img.load()
                for y in range(img.height):
                    for x in range(img.width):
                        r, g, b, a = px[x, y]
                        px[x, y] = (int(r * DARK[0]), int(g * DARK[1]), int(b * DARK[2]), a)
            out.alpha_composite(img, (col * T, row * T))
    return out


def shadow(w, h):
    img = Image.new("RGBA", (w, h))
    rx, ry = w / 2, h / 2
    for y in range(h):
        for x in range(w):
            d = ((x + .5 - rx) / rx) ** 2 + ((y + .5 - ry) / ry) ** 2
            if d <= 1:
                img.putpixel((x, y), SHADOW_CORE if d <= .55 else SHADOW_RIM)
    return img


def place(canvas, name, col, row, flip=False):
    sheet, size, ppu, w, h, ox, oy = CHARS[name]
    k = T / ppu
    frame = Image.open(ROOT / sheet).convert("RGBA").crop((0, 0, size, size))
    if flip:
        frame = frame.transpose(Image.FLIP_LEFT_RIGHT)
    cx, cy = col * T + T / 2, row * T + T / 2
    s = shadow(w, h).resize((round(w * k), round(h * k)), Image.NEAREST)
    canvas.alpha_composite(s, (round(cx + ox * k - s.width / 2), round(cy - oy * k - s.height / 2)))
    f = frame.resize((round(size * k), round(size * k)), Image.NEAREST)
    canvas.alpha_composite(f, (round(cx - f.width / 2), round(cy - f.height / 2)))


def board_preview():
    b = board()
    for name, col, row, flip in (("wizard", 7, 8, False), ("bat", 10, 5, True), ("bat", 4, 4, False),
                                 ("snake", 10, 10, True), ("snake", 3, 11, False)):
        place(b, name, col, row, flip)
    canvas = Image.new("RGBA", (b.width + 2 * T, b.height + 2 * T), BG)
    canvas.alpha_composite(b, (T, T))
    return canvas.resize((canvas.width * 2, canvas.height * 2), Image.NEAREST)


MOTIONS = {
    "wizard": ("Assets/Player/Lively", "wizard", 50, ["idle", "walk", "attack", "hit", "death"]),
    "bat": ("Assets/Monsters/Bat/Lively", "bat", 40, ["idle", "move", "attack", "hit", "death"]),
    "snake": ("Assets/Monsters/Snake/Lively", "snake", 40, ["idle", "move", "attack", "hit", "death"]),
}


def motions_preview(scale=3):
    cell = 50
    cols = []
    rows = []
    for folder, prefix, size, names in MOTIONS.values():
        row = []
        for motion in names:
            sheet = Image.open(ROOT / folder / f"{prefix}_{motion}.png").convert("RGBA")
            count = sheet.width // size
            pick = {"idle": 0, "walk": 2, "move": 2, "attack": 2, "hit": 2, "death": count - 1 if prefix == "wizard" else count // 3}[motion]  # 몬스터 마지막 프레임은 소멸 후라 비어 있다
            frame = sheet.crop((pick * size, 0, pick * size + size, size))
            pad = Image.new("RGBA", (cell, cell))
            pad.alpha_composite(frame, ((cell - size) // 2, (cell - size) // 2))
            row.append(pad)
        rows.append(row)
    labels = ["idle", "move", "attack", "hit", "death"]
    w, h = len(labels) * cell, len(rows) * cell
    img = Image.new("RGBA", (w, h), BG)
    for r, row in enumerate(rows):
        for c, frame in enumerate(row):
            img.alpha_composite(frame, (c * cell, r * cell))
    img = img.resize((w * scale, h * scale), Image.NEAREST)
    header = Image.new("RGBA", (img.width, 28), BG)
    d = ImageDraw.Draw(header)
    for c, label in enumerate(labels):
        d.text((c * cell * scale + cell * scale / 2 - len(label) * 3, 8), label, fill=(232, 214, 176, 255))
    out = Image.new("RGBA", (img.width, img.height + header.height), BG)
    out.alpha_composite(header, (0, 0))
    out.alpha_composite(img, (0, header.height))
    return out


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    board_preview().save(OUT / "board-art-preview.png")
    motions_preview().save(OUT / "character-motions.png")
    print("saved", OUT / "board-art-preview.png", OUT / "character-motions.png")
