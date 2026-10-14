-- image_gen 편집 시안을 실제 120px, 16색, 이진 알파로 정리.
local out='Art/Concepts/QueenRevision-v9-presence-120px'
local pc=app.pixelColor
local function rgb(r,g,b) return pc.rgba(r,g,b,255) end
local colors={
 rgb(7,19,27),
 rgb(4,42,50),rgb(6,69,77),rgb(12,99,109),rgb(36,125,133),
 rgb(157,102,29),rgb(206,144,41),rgb(242,183,66),rgb(255,207,104),
 rgb(141,159,169),rgb(194,206,209),rgb(243,238,221),
 rgb(199,173,150),rgb(230,210,184),rgb(250,233,204),
 rgb(18,190,214)
}
local function nearest(p)
 local r,g,b=pc.rgbaR(p),pc.rgbaG(p),pc.rgbaB(p)
 local best,dist=colors[1],math.huge
 for _,c in ipairs(colors) do
  local dr,dg,db=r-pc.rgbaR(c),g-pc.rgbaG(c),b-pc.rgbaB(c)
  local d=2*dr*dr+3*dg*dg+db*db
  if d<dist then best,dist=c,d end
 end
 return best
end
local function make(path)
 local input=Image{fromFile=path}
 local minx,miny,maxx,maxy=input.width,input.height,-1,-1
 for y=0,input.height-1 do for x=0,input.width-1 do
  if pc.rgbaA(input:getPixel(x,y))>=128 then
   minx,miny,maxx,maxy=math.min(minx,x),math.min(miny,y),math.max(maxx,x),math.max(maxy,y)
  end
 end end
 assert(maxx>=minx,'No visible pixels')
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
  if pc.rgbaA(p)>=128 then im:drawPixel(ox+x,oy+y,nearest(p)) end
 end end
 return im,w,h
end
local function enlarge(im,n)
 local result=Image(im.width*n,im.height*n,ColorMode.RGB)
 for y=0,result.height-1 do for x=0,result.width-1 do
  result:drawPixel(x,y,im:getPixel(math.floor(x/n),math.floor(y/n)))
 end end
 return result
end
local queen,w,h=make(out..'/generated-edit-source.png')
assert(not app.fs.isFile(out..'/queen-presence-120.png'),'Do not overwrite previous output')
queen:saveAs(out..'/queen-presence-120.png')
enlarge(queen,6):saveAs(out..'/queen-presence-120-6x.png')
local sprite=Sprite(120,120,ColorMode.RGB)
sprite.layers[1].name='Imposing queen 120px concept'
sprite:newCel(sprite.layers[1],1,queen,Point(0,0))
local pal=Palette(#colors+1)
pal:setColor(0,Color{r=0,g=0,b=0,a=0})
for i,c in ipairs(colors) do pal:setColor(i,Color{r=pc.rgbaR(c),g=pc.rgbaG(c),b=pc.rgbaB(c),a=255}) end
sprite:setPalette(pal)
sprite:saveAs(out..'/queen-presence-120.aseprite')
sprite:close()
local old=make('Art/Concepts/QueenRevision-v7/queen-original-c-face.png')
local comparison=Image(264,136,ColorMode.RGB)
comparison:clear(rgb(41,45,57))
comparison:drawImage(old,Point(4,8))
comparison:drawImage(queen,Point(140,8))
enlarge(comparison,4):saveAs(out..'/queen-before-after-120-4x.png')
print('Native 120x120; fitted queen '..w..'x'..h..'; fixed 16-color palette, binary alpha.')
