// Жадное слияние клеток сетки в прямоугольники.
// Используется для коллайдеров стен (все твёрдые клетки) и для визуала (отдельно стены и укрытия).
using System;
using System.Collections.Generic;

namespace CatTanks.Level
{
    public static class GridMerge
    {
        /// <summary>Покрывает все клетки, где include(x, y) == true, минимальным набором прямоугольников без пересечений.</summary>
        public static List<GridRect> Merge(int width, int height, Func<int, int, bool> include)
        {
            var result = new List<GridRect>();
            var used = new bool[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (used[x, y] || !include(x, y)) continue;
                    int w = 1;
                    while (x + w < width && !used[x + w, y] && include(x + w, y)) w++;
                    int h = 1;
                    while (y + h < height && RowFree(x, y + h, w, used, include)) h++;
                    for (int yy = y; yy < y + h; yy++)
                        for (int xx = x; xx < x + w; xx++) used[xx, yy] = true;
                    result.Add(new GridRect(x, y, w, h));
                }
            return result;
        }

        static bool RowFree(int x, int y, int w, bool[,] used, Func<int, int, bool> include)
        {
            for (int xx = x; xx < x + w; xx++)
                if (used[xx, y] || !include(xx, y)) return false;
            return true;
        }
    }
}
