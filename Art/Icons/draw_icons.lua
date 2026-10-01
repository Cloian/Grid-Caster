-- Aseprite에서 20×20 좌표에 직접 찍는 아이콘 시안. 원본 파일은 수정하지 않는다.
local out = 'Art/Icons/HandPixel_v1'
app.fs.makeAllDirectories(out)
local original = Image{fromFile='Assets/UI/Icons/Actions/Move.png'}
local function rgba(r,g,b) return app.pixelColor.rgba(r,g,b,255) end
local palette = {
 K=rgba(1,1,2), V=rgba(118,66,138), P=rgba(158,92,186),
 B=rgba(99,155,255), C=rgba(95,205,228), W=rgba(255,255,255),
 d=rgba(64,35,5), b=rgba(96,57,19), t=rgba(145,102,63),
 h=rgba(203,174,142), O=rgba(223,113,38), Y=rgba(251,190,82)
}
local base = Image(original)
-- 외곽 세 줄은 모서리까지 원본과 완전히 동일하게 유지한다.
for y=3,16 do for x=3,16 do base:drawPixel(x,y,original:getPixel(8,2)) end end
local function pattern(im, rows, ox, oy)
 for y,row in ipairs(rows) do
  for x=1,#row do
   local c = palette[row:sub(x,x)]
   if c then
    local px,py=ox+x-1,oy+y-1
    assert(px>=3 and px<=16 and py>=3 and py<=16, 'Frame overlap')
    im:drawPixel(px,py,c)
   end
  end
 end
end
local boot={
 'KKKKK...', 'KhhhK...', 'KtbK....', '.KbK....',
 '.KbtKK..', '.KhtbbK.', '.KKKKKK.'
}
local specs={
 {id='KnightLeap', rows={
  '..........C...', '.........CWC..', '..........C...',
  '..VP..........', '.VBCV.........', '.VCWV.........',
  '..VV..........', '..............', '..P...........',
  '..C...........', '..C...........', '..CCCC...P....',
  '......P.CWC...', '.........C....'
 }, boot=true},
 {id='BishopPhase', rows={
  '..........C...', '.........CWC..', '........BCC...',
  '.......BP.....', '......BP......', '.....VP.......',
  '....VP........', '...VP.........', '..VP..........',
  '.BP...........', 'BCC...........', 'CWC...........',
  '.C............', '..............'
 }, boot=true},
 {id='RookRush', rows={
  '..............', '..........C...', '.........CWC..',
  '..........C...', '..............', 'PPB.......V...',
  '...C......BC..', 'PBC.......BWC.', '...C......BC..',
  'PPB.......V...', '..............', '...PCCCCCP....',
  '....VBBBV.....', '..............'
 }, boot=true},
 {id='ChainBurst', rows={
  '......K.......', '..K..KOK..K...', '..Y...OY..Y...',
  '.....KYYK.....', '.KK..KYWK..KK.', '.OYYKYWYYKYYO.',
  '..KYYWWWYYK...', '...KWWWWWK....', '..KYYWWWYYK...',
  '.OYYKYWYYKYYO.', '.KK..KYYK..KK.', '.....KOK......',
  '..Y...O...Y...', '......K.......'
 }},
 {id='BindingLanding', rows={
  '..VP......PV..', '.VCWV....VWCV.', '..VB......BV..',
  '...B.KKKK.B...', '....KKPPKK....', '....KPWWPK....',
  '....KPPP PK...', '....KKPPKK....', '...B.KKKK.B...',
  '..VB......BV..', '.VCWV....VWCV.', '..VP.CCCC.PV..',
  '....CWWWWC....', '.....CCCC.....'
 }},
 {id='EchoPressure', rows={
  '..............', '.......KK.....', '......KPWK....',
  '.....KPWPK....', '....KPWPK.....', '.....KPK......',
  '..P...K...KK..', '.V.......KPWK.', '........KPWPK.',
  '.......KPWPK..', '........KPK...', '.....P...K....',
  '....V.........', '..............'
 }}
}
local contact = Image(80,80)
contact:clear(rgba(30,27,35))
local refs={'Actions/Move','Traits/Teleport','Traits/DoubleCast'}
for i,p in ipairs(refs) do
 contact:drawImage(Image{fromFile='Assets/UI/Icons/'..p..'.png'},Point(4+(i-1)*26,2))
end
for i,spec in ipairs(specs) do
 local sprite=Sprite(20,20,ColorMode.RGB)
 sprite.layers[1].name='Original frame and background'
 sprite:newCel(sprite.layers[1],1,base,Point(0,0))
 sprite.layers[1].isEditable=false
 local marks=Image(20,20)
 pattern(marks,spec.rows,3,3)
 local effect=sprite:newLayer()
 effect.name='Hand-pixel ability motif'
 sprite:newCel(effect,1,marks,Point(0,0))
 if spec.boot then
  local im=Image(20,20)
  pattern(im,boot,7,6)
  local layer=sprite:newLayer()
  layer.name='Shared enchanted boot'
  sprite:newCel(layer,1,im,Point(0,0))
 end
 local flat=Image(sprite)
 for y=0,19 do for x=0,19 do
  if x<3 or x>16 or y<3 or y>16 then
   assert(flat:getPixel(x,y)==original:getPixel(x,y),spec.id..' changed border')
  end
 end end
 assert(not app.fs.isFile(out..'/'..spec.id..'.aseprite'),'Output already exists')
 sprite:saveAs(out..'/'..spec.id..'.aseprite')
 flat:saveAs(out..'/'..spec.id..'.png')
 contact:drawImage(flat,Point(4+((i-1)%3)*26,28+math.floor((i-1)/3)*26))
 sprite:close()
 print(spec.id..': 20x20, exact source frame verified')
end
contact:saveAs(out..'/Comparison.png')
print('Top: original Move / Teleport / DoubleCast. Middle: Knight / Bishop / Rook. Bottom: Burst / Binding / Echo.')
