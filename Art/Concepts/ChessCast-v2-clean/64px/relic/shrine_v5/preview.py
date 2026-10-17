from PIL import Image
a=Image.open('relic_shrine_safe.png'); b=Image.open('relic_shrine_risky.png')
shot=Image.open('../../ingame_wave3_bombers.png').convert('RGBA')
def place(im,c,r):  # c,r = 왼쪽 칸, 제단이 차지하는 줄
    shot.alpha_composite(im.resize((192,192),Image.NEAREST),(66+64*c-1,70+64*r-128-2))
place(a,1,3); place(b,7,9)
shot.crop((60,40,900,760)).save('preview_ingame.png')
bg=Image.new('RGBA',(200,100),(198,160,121,255))
for i in range(0,200,32):
  for j in range(0,100,32):
    if (i//32+j//32)%2: bg.paste((158,135,109,255),(i,j,i+32,j+32))
bg.alpha_composite(a,(2,2)); bg.alpha_composite(b,(102,2))
bg.resize((800,400),0).save('preview_4x.png')
