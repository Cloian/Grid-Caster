-- frames/의 PNG를 시안별 .aseprite(32×32, 12프레임×60ms, 태그 center 1–6 / splash 7–12)로 묶는다.
-- 실행: aseprite -b --script-param root=<이 폴더> [--script-param palette=magic] --script build_aseprite.lua
local root = app.params.root
assert(root and #root > 0, 'root parameter is required')
local palette = app.params.palette or 'warm'
local frames = palette == 'warm' and '/frames/' or ('/frames_' .. palette .. '/')
local suffix = palette == 'warm' and '' or ('_' .. palette)
for _, name in ipairs({ 'A_heat_ring', 'B_brand_sigil', 'C_shard_strike' }) do
  local spr = Sprite(32, 32, ColorMode.RGB)
  spr.layers[1].name = 'Effect'
  local index = 0
  for _, tag in ipairs({ 'center', 'splash' }) do
    for i = 1, 6 do
      index = index + 1
      if index > 1 then spr:newEmptyFrame() end
      spr.frames[index].duration = 0.06
      local image = Image{ fromFile = root .. frames .. name .. '_' .. tag .. '_' .. i .. '.png' }
      spr:newCel(spr.layers[1], index, image, Point(0, 0))
    end
  end
  spr:newTag(1, 6).name = 'center'
  spr:newTag(7, 12).name = 'splash'
  spr:saveAs(root .. '/' .. name .. suffix .. '.aseprite')
  print(name .. suffix .. ' frames=' .. #spr.frames .. ' tags=' .. #spr.tags)
  spr:close()
end
