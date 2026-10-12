-- Переводит картинку в пиксель-арт: обрезка пустых краёв → уменьшение до нужной высоты →
-- ограничение палитры → чёткая прозрачность (без полупрозрачных краёв) → тёмный контур снаружи.
-- Запуск (Aseprite в пакетном режиме):
--   Aseprite.exe -b --script-param in=<png> --script-param png=<png> --script-param ase=<aseprite>
--                --script-param size=<px по большей стороне> --script-param colors=<N>
--                --script pixelize.lua
-- size=0 — не менять размер (только палитра, прозрачность и контур).

local p = app.params
local inPath, pngPath, asePath = p["in"], p["png"], p["ase"]
local size = tonumber(p["size"] or "0")
local colors = tonumber(p["colors"] or "12")
local outline = Color{ r = 34, g = 20, b = 26, a = 255 }   -- тёмный, как контур танка и тайлов

local spr = app.open(inPath)
if not spr then error("не открылся " .. tostring(inPath)) end
if spr.colorMode ~= ColorMode.RGB then app.command.ChangePixelFormat{ format = "rgb" } end
app.command.FlattenLayers()

-- 1) обрезаем пустые поля
app.command.AutocropSprite()

-- 2) уменьшаем: билинейно — чтобы цвета усреднились, а не «рвались»
if size > 0 then
  local k = size / math.max(spr.width, spr.height)
  if k < 1 then
    app.command.SpriteSize{ ui = false, scale = k, method = "bilinear", lockRatio = true }
  end
end

-- 3) жёсткая прозрачность: полупрозрачные края превращаем либо в пиксель, либо в пустоту
local function hardAlpha()
  local cel = spr.cels[1]
  local img = cel.image:clone()
  for it in img:pixels() do
    local c = it()
    local a = app.pixelColor.rgbaA(c)
    if a < 110 then it(0)
    elseif a < 255 then
      it(app.pixelColor.rgba(app.pixelColor.rgbaR(c), app.pixelColor.rgbaG(c), app.pixelColor.rgbaB(c), 255))
    end
  end
  cel.image = img
end
hardAlpha()

-- 4) ограничиваем палитру: убирает «фотографический» шум и градиенты
app.command.ColorQuantization{ ui = false, withAlpha = false, maxColors = colors }
app.command.ChangePixelFormat{ format = "indexed", dithering = "none" }
app.command.ChangePixelFormat{ format = "rgb" }

-- 5) место под контур и сам контур снаружи силуэта
app.command.CanvasSize{ ui = false, left = 1, top = 1, right = 1, bottom = 1 }
app.command.Outline{ ui = false, place = "outside", matrix = "circle", color = outline, bgColor = Color{ r = 0, g = 0, b = 0, a = 0 } }

spr:saveCopyAs(pngPath)
if asePath and asePath ~= "" then spr:saveAs(asePath) end
print(string.format("%s -> %dx%d, %d colors", inPath, spr.width, spr.height, colors))
spr:close()
