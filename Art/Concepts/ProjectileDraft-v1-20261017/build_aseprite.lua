-- frames/의 PNG를 시안별 .aseprite(32×32, 8프레임×50ms, 태그 right 1–4 / diag 5–8)로 묶는다.
-- 실행: aseprite -b --script-param root=<이 폴더> --script build_aseprite.lua
local root = app.params.root
assert(root and #root > 0, 'root parameter is required')
for _, name in ipairs({ 'A_comet', 'B_rune_dart', 'C_arcane_orb' }) do
  local spr = Sprite(32, 32, ColorMode.RGB)
  spr.layers[1].name = 'Projectile'
  local index = 0
  for _, tag in ipairs({ 'right', 'diag' }) do
    for i = 1, 4 do
      index = index + 1
      if index > 1 then spr:newEmptyFrame() end
      spr.frames[index].duration = 0.05
      local image = Image{ fromFile = root .. '/frames/' .. name .. '_' .. tag .. '_' .. i .. '.png' }
      spr:newCel(spr.layers[1], index, image, Point(0, 0))
    end
  end
  spr:newTag(1, 4).name = 'right'
  spr:newTag(5, 8).name = 'diag'
  spr:saveAs(root .. '/' .. name .. '.aseprite')
  print(name .. ' frames=' .. #spr.frames .. ' tags=' .. #spr.tags)
  spr:close()
end
