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

        // ------------------------------------------------------------------ Процедурные (под нужный размер)

        /// <summary>Пиксельное кольцо диаметром diameter юнитов: белая линия thickness пикселей с тёмным контуром.
        /// dashed — пунктир (для границы дальности).</summary>
        public static Sprite Ring(float diameter, int thickness = 2, bool dashed = false)
        {
            int n = Mathf.Max(8, Mathf.RoundToInt(diameter * PixelsPerUnit));
            return Procedural($"Ring{n}_{thickness}_{dashed}", n, (x, y, r, a) =>
            {
                float R = n / 2f;
                if (dashed && Mathf.Repeat(a * R / 6f, 2f) > 1f) return Clear;     // штрихи по ~6 px
                if (r <= R - 1 && r > R - 1 - thickness) return White;
                if (r <= R && r > R - 2 - thickness) return Outline;                // контур с обеих сторон линии
                return Clear;
            });
        }

        /// <summary>Круг, залитый «шахматкой» (пиксельная полупрозрачность, как в старых играх).</summary>
        public static Sprite DitherDisc(float diameter)
        {
            int n = Mathf.Max(4, Mathf.RoundToInt(diameter * PixelsPerUnit));
            return Procedural($"Disc{n}", n, (x, y, r, a) => r <= n / 2f && ((x + y) & 1) == 0 ? White : Clear);
        }

        /// <summary>Воронка: тёмная яма с рваным краем, валом земли и трещинами. Рисуется белым цветом SpriteRenderer.</summary>
        public static Sprite Crater(float diameter)
        {
            int n = Mathf.Max(16, Mathf.RoundToInt(diameter * PixelsPerUnit));
            var pit = new Color32(36, 28, 24, 230);
            var pitLight = new Color32(58, 45, 36, 220);
            var rim = new Color32(112, 94, 72, 200);
            var crack = new Color32(30, 22, 20, 200);
            return Procedural($"Crater{n}", n, (x, y, r, a) =>
            {
                float R = n / 2f;
                // рваный край: радиус ямы «гуляет» по углу
                float edge = R * (0.62f + 0.06f * Mathf.Sin(a * 5f + 1.3f) + 0.04f * Mathf.Sin(a * 11f + 0.7f));
                if (r < edge) return r > edge - 3f || ((x + y) % 3 == 0) ? pitLight : pit;
                if (r < edge + 3f) return rim;
                // трещины-лучи наружу, шириной в пиксель
                float ray = Mathf.Abs(Mathf.Sin(a * 4f + Mathf.Sin(r * 0.15f) * 0.8f));
                if (r < R * 0.98f && ray < 0.06f * (1f - r / R) + 0.015f) return crack;
                return Clear;
            });
        }

        static readonly Color32 White = new Color32(255, 255, 255, 255);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        delegate Color32 PixelFunc(int x, int y, float r, float angle);

        static Sprite Procedural(string key, int n, PixelFunc f)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var px = new Color32[n * n];
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - c, dy = y - c;
                    px[y * n + x] = f(x, y, Mathf.Sqrt(dx * dx + dy * dy) + 0.5f, Mathf.Atan2(dy, dx));
                }
            return Cache[key] = MakeSprite(key, n, n, px);
        }

        static Sprite MakeSprite(string name, int w, int h, Color32[] px)
        {
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
            return sprite;
        }

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
                                  : Clear;
                }
            return Cache[name] = MakeSprite(name, w, h, px);
        }
    }
}
