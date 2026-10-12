-- Пиксельный грецкий орех для ульты (16×16). Рисуется по символьной карте, палитра — тёплые коричневые
-- с тёмным контуром как у танка и тайлов. Запуск:
--   Aseprite.exe -b --script-param png=<png> --script-param ase=<aseprite> --script draw_walnut.lua
local map = {
  "......####......",
  "....##LLLL##....",
  "...#LHHLSLLM#...",
  "..#LHLLMSLMMD#..",
  "..#LLMLLSMLMD#..",
  ".#LMLLMDSLMMMD#.",
  ".#LLMLLMSMLMDD#.",
  ".#MLLMLMSLMMDD#.",
  ".#LMLMLDSMLMDD#.",
  ".#MLMLMMSLMDDD#.",
  "..#MLMLDSMMDD#..",
  "..#DMMMMSMDDD#..",
  "...#DMMDSMDD#...",
  "....##DDSD##....",
  "......####......",
  "................",
}
local pal = {
  ["#"] = Color{ r = 34,  g = 20,  b = 26  },   -- контур
  ["D"] = Color{ r = 107, g = 69,  b = 33  },   -- тень
  ["M"] = Color{ r = 160, g = 110, b = 60  },   -- основной
  ["L"] = Color{ r = 209, g = 158, b = 97  },   -- светлый
  ["H"] = Color{ r = 240, g = 220, b = 180 },   -- блик
  ["S"] = Color{ r = 70,  g = 44,  b = 22  },   -- шов
}

local spr = Sprite(16, 16)
spr.filename = "walnut"
local img = spr.cels[1].image
for y = 1, #map do
  local row = map[y]
  assert(#row == 16, "строка " .. y .. " длиной " .. #row)
  for x = 1, 16 do
    local c = pal[row:sub(x, x)]
    if c then img:drawPixel(x - 1, y - 1, c) end
  end
end
spr:saveCopyAs(app.params.png)
spr:saveAs(app.params.ase)
print("walnut 16x16 ok")
