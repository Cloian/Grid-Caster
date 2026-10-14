-- image_gen 재드로잉을 Aseprite에서 정확한 게임 검토 크기로 정리한다.
-- 120×120 실제 캔버스, nearest sampling, 고정 10색, alpha 0/255.
local out = 'Art/Concepts/BossReboot-v2-120px'
local function rgb(r,g,b) return app.pixelColor.rgba(r,g,b,255) end
local pc=app.pixelColor
local colors={
 rgb(11,13,20),rgb(38,33,49),rgb(73,61,81),rgb(107,88,112),
 rgb(25,29,41),rgb(51,57,73),rgb(73,56,39),rgb(128,99,66),
 rgb(176,138,86),rgb(196,111,77)
}
local function nearest(p)
 local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
 local best,distance=colors[1],math.huge
 for _,c in ipairs(colors) do
  local dr,dg,db=r-pc.rgbaR(c),g-pc.rgbaG(c),b-pc.rgbaB(c)
  local d=2*dr*dr+3*dg*dg+db*db
  if d<distance then best,distance=c,d end
 end
 return best
end
local function make(source,quantize)
 local input=Image{fromFile=source}
 local minx,miny,maxx,maxy=input.width,input.height,-1,-1
 for y=0,input.height-1 do for x=0,input.width-1 do
  if pc.rgbaA(input:getPixel(x,y))>=128 then
   minx,miny,maxx,maxy=math.min(minx,x),math.min(miny,y),math.max(maxx,x),math.max(maxy,y)
  end
 end end
 assert(maxx>=minx,'No visible source pixels')
 local bw,bh=maxx-minx+1,maxy-miny+1
 local scale=math.min(112/bw,112/bh)
 local w,h=math.floor(bw*scale+0.5),math.floor(bh*scale+0.5)
 local ox,oy=math.floor((120-w)/2),116-h
 local im=Image(120,120,ColorMode.RGB)
 im:clear(0)
 for y=0,h-1 do for x=0,w-1 do
  local sx=minx+math.min(bw-1,math.floor((x+0.5)*bw/w))
  local sy=miny+math.min(bh-1,math.floor((y+0.5)*bh/h))
  local p=input:getPixel(sx,sy)
  if pc.rgbaA(p)>=128 then
   im:drawPixel(ox+x,oy+y,quantize and nearest(p) or rgb(pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)))
  end
 end end
 return im,w,h
end
local boss,w,h=make(out..'/generated-redraw-source.png',true)
-- 색 하나만 튀는 내부 픽셀만 정리한다. 눈의 연결된 2픽셀 군집과 외곽 형태는 유지.
local clean=Image(boss)
for y=1,118 do for x=1,118 do
 local p=boss:getPixel(x,y)
 if pc.rgbaA(p)==255 then
  local neighbours={boss:getPixel(x-1,y),boss:getPixel(x+1,y),boss:getPixel(x,y-1),boss:getPixel(x,y+1)}
  local same,opaque,counts=0,0,{}
  for _,n in ipairs(neighbours) do
   if n==p then same=same+1 end
   if pc.rgbaA(n)==255 then opaque=opaque+1; counts[n]=(counts[n] or 0)+1 end
  end
  if same==0 and opaque==4 then
   for n,count in pairs(counts) do if count>=3 then clean:drawPixel(x,y,n); break end end
  end
 end
end end
boss=clean
assert(not app.fs.isFile(out..'/black-iron-colossus-120.png'),'Do not overwrite an existing version')
boss:saveAs(out..'/black-iron-colossus-120.png')
local sprite=Sprite(120,120,ColorMode.RGB)
sprite.layers[1].name='120px cleaned boss draft'
sprite:newCel(sprite.layers[1],1,boss,Point(0,0))
local pal=Palette(11)
pal:setColor(0,Color{r=0,g=0,b=0,a=0})
for i,c in ipairs(colors) do pal:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255}) end
sprite:setPalette(pal)
sprite:saveAs(out..'/black-iron-colossus-120.aseprite')
sprite:close()
local function enlarged(im,factor)
 local result=Image(im.width*factor,im.height*factor,ColorMode.RGB)
 for y=0,result.height-1 do for x=0,result.width-1 do
  result:drawPixel(x,y,im:getPixel(math.floor(x/factor),math.floor(y/factor)))
 end end
 return result
end
enlarged(boss,6):saveAs(out..'/black-iron-colossus-120-6x.png')
local queen=make('Art/Concepts/QueenRevision-v7/queen-original-c-face.png',false)
local comparison=Image(264,136,ColorMode.RGB)
comparison:clear(rgb(41,45,57))
comparison:drawImage(queen,Point(4,8))
comparison:drawImage(boss,Point(140,8))
enlarged(comparison,4):saveAs(out..'/queen-boss-same-120px-comparison-4x.png')
print('Native 120x120. Boss drawn bounds fit '..w..'x'..h..', common bottom anchor y=116. Queen/boss same 120 canvas.')
