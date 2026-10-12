// Маленькие пиксельные спрайты, нарисованные прямо в коде строками — для эффектов, которым не нужен художник
// (точки прицела, следы гусениц, пыль). Каждый символ — пиксель:
//   '.' — прозрачный,  'o' — белый (окрашивается цветом SpriteRenderer),  '#' — тёмный контур,  '-' — полупрозрачный белый.
// Фильтр Point: пиксели остаются чёткими при любом масштабе.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public static class PixelArt
    {
        /// <summary>Сколько пикселей таких спрайтов на юнит. 32 — пиксель чуть крупнее, чем у танка (≈43 на юнит).</summary>
        public const float PixelsPerUnit = 32f;

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static readonly Color32 Outline = new Color32(34, 20, 26, 255); // как контур танка и тайлов

        public static Sprite AimDot => Get("AimDot",
            ".##.",
            "#oo#",
            "#oo#",
            ".##.");

        public static Sprite AimBounce => Get("AimBounce",
            "...#...",
            "..#o#..",
            ".#ooo#.",
            "#ooooo#",
            ".#ooo#.",
            "..#o#..",
            "...#...");

        public static Sprite AimEnd => Get("AimEnd",
            "##...##",
            "#o#.#o#",
            ".#o#o#.",
            "..#o#..",
            ".#o#o#.",
            "#o#.#o#",
            "##...##");

        // отпечаток протектора: полосы поперёк гусеницы (вдоль движения — по X)
        public static Sprite TreadMark => Get("TreadMark",
            "oo..",
            "oo..",
            "oo..",
            "oo..",
            "oo..",
            "oo..",
            "oo..",
            "oo..");

        public static Sprite Dust => Get("Dust",
            "..-..",
            ".-o-.",
            "-ooo-",
            ".-o-.",
            "..-..");

        /// <summary>Спрайт из строк (первая строка — верх). Создаётся один раз и кэшируется.</summary>
        public static Sprite Get(string name, params string[] rows)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null) return cached;
            int h = rows.Length, w = rows[0].Length;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = rows[h - 1 - y][x]; // в текстуре y растёт вверх
                    px[y * w + x] = c == 'o' ? new Color32(255, 255, 255, 255)
                                  : c == '-' ? new Color32(255, 255, 255, 120)
                                  : c == '#' ? Outline
                                  : new Color32(0, 0, 0, 0);
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Pixel" + name,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            Cache[name] = sprite;
            return sprite;
        }
    }
}
