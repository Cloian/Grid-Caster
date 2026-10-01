-- 기존 Teleport 아이콘의 테두리를 그대로 보존하고 체스 말 실루엣만 직접 찍는다.
local output = 'Assets/UI/Icons/Movement'
app.fs.makeAllDirectories(output)

local source = Image{fromFile='Assets/UI/Icons/Traits/Teleport.png'}
local function rgba(r, g, b) return app.pixelColor.rgba(r, g, b, 255) end
local colors = {
  K=rgba(1, 1, 2),
  D=rgba(52, 42, 82),
  V=rgba(112, 70, 145),
  B=rgba(83, 143, 224),
  C=rgba(99, 213, 239),
  W=rgba(245, 248, 255),
  O=rgba(226, 112, 42),
  Y=rgba(251, 196, 79),
  P=rgba(231, 72, 159)
}

local base = Image(source)
for y=3,16 do
  for x=3,16 do base:drawPixel(x, y, source:getPixel(8, 2)) end
end

local pieces = {
  { id='KnightMove', rows={
    '....KKK.......',
    '...KCCCK......',
    '..KCCWCK......',
    '..KCBCK.......',
    '.KBBBK........',
    '.KBBBK........',
    '..KBBKK.......',
    '...KBBBK......',
    '...KBBBBK.....',
    '..KBBBBBBK....',
    '..KKKKKKKK....',
    '.KCCCCCCCCK...',
    '.KKKKKKKKKK...',
    '..............'
  }},
  { id='BishopMove', rows={
    '......KK......',
    '.....KYYK.....',
    '....KYYOK.....',
    '...KYYOK......',
    '...KYOYYK.....',
    '...KYYYYK.....',
    '....KYYK......',
    '....KYYK......',
    '...KYYYYK.....',
    '..KYYYYYYK....',
    '..KKKKKKKK....',
    '.KOOOOOOOOOK..',
    '.KKKKKKKKKKK..',
    '..............'
  }},
  { id='RookMove', rows={
    '..KK..KK..KK..',
    '..KPKKPPKKPK..',
    '..KPPPPPPPPK..',
    '...KPPPPPPK...',
    '...KPPPPPPK...',
    '...KPVVPPPK...',
    '...KPPPPPPK...',
    '...KPPPPPPK...',
    '...KPPPPPPK...',
    '..KPPPPPPPPK..',
    '..KKKKKKKKKK..',
    '.KBBBBBBBBBBK.',
    '.KKKKKKKKKKKK.',
    '..............'
  }}
}

local function drawPattern(image, rows)
  for y,row in ipairs(rows) do
    for x=1,#row do
      local color = colors[row:sub(x,x)]
      if color then image:drawPixel(x + 2, y + 2, color) end
    end
  end
end

for _,piece in ipairs(pieces) do
  local sprite = Sprite(20, 20, ColorMode.RGB)
  sprite.layers[1].name = 'Original user icon frame'
  sprite:newCel(sprite.layers[1], 1, base, Point(0, 0))
  sprite.layers[1].isEditable = false

  local pixels = Image(20, 20)
  drawPattern(pixels, piece.rows)
  local layer = sprite:newLayer()
  layer.name = 'Hand-pixel chess piece'
  sprite:newCel(layer, 1, pixels, Point(0, 0))

  local flat = Image(sprite)
  for y=0,19 do
    for x=0,19 do
      if x < 3 or x > 16 or y < 3 or y > 16 then
        assert(flat:getPixel(x,y) == source:getPixel(x,y), piece.id .. ' changed frame')
      end
    end
  end

  sprite:saveAs(output .. '/' .. piece.id .. '.aseprite')
  flat:saveAs(output .. '/' .. piece.id .. '.png')
  sprite:close()
  print(piece.id .. ': 20x20, source frame preserved')
end
