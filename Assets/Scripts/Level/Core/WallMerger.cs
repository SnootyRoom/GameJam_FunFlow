// Слияние твёрдых клеток в прямоугольники для коллайдеров без швов.
// Нужно и генератору, и разрушению стен во время игры (ульта). Не зависит от UnityEngine.
using System.Collections.Generic;

namespace CatTanks.Level
{
    public static class WallMerger
    {
        public static List<GridRect> Merge(LevelData d)
        {
            int W = d.width, H = d.height;
            var rects = new List<GridRect>();
            var used = new bool[W, H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (used[x, y] || d.tiles[x, y] == Tile.Floor) continue;
                    var t = d.tiles[x, y];
                    // стены и укрытия не смешиваем — у них разный цвет
                    int w = 1;
                    while (x + w < W && !used[x + w, y] && d.tiles[x + w, y] == t) w++;
                    int h = 1;
                    while (y + h < H && RowSame(d, x, y + h, w, used, t)) h++;
                    for (int yy = y; yy < y + h; yy++)
                        for (int xx = x; xx < x + w; xx++) used[xx, yy] = true;
                    rects.Add(new GridRect(x, y, w, h));
                }
            return rects;
        }

        static bool RowSame(LevelData d, int x, int y, int w, bool[,] used, Tile t)
        {
            for (int xx = x; xx < x + w; xx++)
                if (used[xx, y] || d.tiles[xx, y] != t) return false;
            return true;
        }
    }
}
