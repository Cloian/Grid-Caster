import sys
from PIL import Image
d=sys.argv[1]
a=Image.open(f'{d}/relic_altar_safe.png'); b=Image.open(f'{d}/relic_altar_risky.png')
bg=Image.new('RGBA',(140,70),(40,44,52,255)); bg.alpha_composite(a,(2,3)); bg.alpha_composite(b,(72,3))
bg.resize((560,280),0).save(f'{d}/preview_4x.png')
k=Image.open(f'{d}/../../knight.png').convert('RGBA')
board=Image.new('RGBA',(128,64))
for ty in range(2):
  for tx in range(4):
    board.paste((58,62,74,255) if (tx+ty)%2 else (70,76,90,255),(tx*32,ty*32,tx*32+32,ty*32+32))
half=lambda im: im.resize((32,32),Image.NEAREST)
board.alpha_composite(half(a),(0,0)); board.alpha_composite(half(k),(32,0)); board.alpha_composite(half(b),(64,32))
board.resize((512,256),0).save(f'{d}/preview_board.png')
