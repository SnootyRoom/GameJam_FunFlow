-- Кнопка SKIP по образцу button_next / button_go: та же плашка 280×104, тот же шрифт 5×7 блоков по 8 px.
-- Запуск: Aseprite.exe -b --script ArtSource/Scripts/draw_skip_button.lua
-- Берёт плашку из Assets/Art/UI/button_next.png, стирает надпись и пишет SKIP.
-- S и I сняты с кнопок pause_settings / pause_tutorial, P — это R без диагонали, K — в той же сетке.

local INK   = Color{ r = 42,  g = 34,  b = 51,  a = 255 }
local PAPER = Color{ r = 242, g = 234, b = 211, a = 255 }
local UNIT  = 8                    -- один пиксель шрифта = 8 px картинки
local TOP   = 20                   -- верх строки текста, как у NEXT
local GAP   = 1                    -- расстояние между буквами в блоках

local glyphs = {
  S = { ".###.", "#...#", "#....", ".###.", "....#", "#...#", ".###." },
  K = { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
  I = { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
  P = { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
}

local base = app.fs.joinPath(app.fs.currentPath, "Assets/Art/UI/button_next.png")
local out  = app.fs.joinPath(app.fs.currentPath, "Assets/Art/UI/button_skip.png")
local src  = app.fs.joinPath(app.fs.currentPath, "ArtSource/UI/button_skip.aseprite")

local sprite = app.open(base)
local img = sprite.cels[1].image

-- стираем старую надпись: вся строка текста лежит на однотонной бумаге
for y = TOP, TOP + 7 * UNIT - 1 do
  for x = 24, 247 do img:drawPixel(x, y, PAPER) end
end

local text = "SKIP"
local width = #text * 5 + (#text - 1) * GAP              -- в блоках
local left = math.floor((16 + 256) / 2 - width * UNIT / 2) -- центр бумаги без тени, как у NEXT

for i = 1, #text do
  local g = glyphs[text:sub(i, i)]
  local gx = left + (i - 1) * (5 + GAP) * UNIT
  for row = 1, 7 do
    for col = 1, 5 do
      if g[row]:sub(col, col) == "#" then
        for dy = 0, UNIT - 1 do
          for dx = 0, UNIT - 1 do
            img:drawPixel(gx + (col - 1) * UNIT + dx, TOP + (row - 1) * UNIT + dy, INK)
          end
        end
      end
    end
  end
end

app.fs.makeAllDirectories(app.fs.filePath(src))
sprite:saveCopyAs(src)
sprite:saveCopyAs(out)
sprite:close()
