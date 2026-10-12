// Примитивы-заглушки, нарисованные кодом: квадрат, круг, кольцо, сердце, рыбка, мягкое свечение,
// скруглённая панель для UI и шахматка для пола. Когда появятся спрайты художника — назначь префабы/спрайты.
using System;
using UnityEngine;

namespace CatTanks
{
    public static class Prims
    {
        static Sprite _square, _circle, _ring, _glow, _heart, _fish, _rounded, _checker, _triangle;

        /// <summary>Квадрат 1×1 юнит.</summary>
        public static Sprite Square => _square != null ? _square : (_square = Make("PrimSquare", 64, (x, y) => true));

        /// <summary>Круг диаметром 1 юнит.</summary>
        public static Sprite Circle => _circle != null ? _circle : (_circle = Make("PrimCircle", 64, (x, y) => x * x + y * y <= 1f));

        /// <summary>Кольцо (для прицела, ульты, волн).</summary>
        public static Sprite Ring => _ring != null ? _ring : (_ring = Make("PrimRing", 128, (x, y) =>
        {
            float r = x * x + y * y;
            return r <= 1f && r >= 0.78f * 0.78f;
        }));

        /// <summary>Мягкое радиальное свечение (альфа спадает от центра).</summary>
        public static Sprite Glow
        {
            get
            {
                if (_glow != null) return _glow;
                const int n = 64;
                var tex = NewTex("PrimGlow", n);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(t * t * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                return _glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            }
        }

        public static Sprite Heart => _heart != null ? _heart : (_heart = Make("PrimHeart", 96, (x, y) =>
        {
            x *= 1.2f; y = y * 1.25f + 0.2f;
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }));

        /// <summary>Рыбка мордой вправо.</summary>
        public static Sprite Fish => _fish != null ? _fish : (_fish = Make("PrimFish", 128, (x, y) =>
        {
            float bx = (x - 0.15f) / 0.72f, by = y / 0.5f;
            bool body = bx * bx + by * by <= 1f;
            bool tail = x < -0.45f && x > -1f && Mathf.Abs(y) < (-0.45f - x) * 1.1f;
            float ex = x - 0.5f, ey = y - 0.12f;
            bool eye = ex * ex + ey * ey < 0.012f;
            return (body || tail) && !eye;
        }));

        static Sprite _walnut, _crater, _star;

        /// <summary>Пятиконечная звезда (оценка уровня).</summary>
        public static Sprite Star => _star != null ? _star : (_star = Make("PrimStar", 96, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(x, y); // 0 — вверх
            float k = Mathf.Repeat(a, Mathf.PI * 2f / 5f) / (Mathf.PI * 2f / 5f); // 0..1 внутри луча
            return r <= Mathf.Lerp(0.42f, 0.95f, Mathf.Abs(k - 0.5f) * 2f) && r <= 1f;
        }));

        /// <summary>Грецкий орех: цвет, морщинки, шов и блик уже в текстуре (рисовать с белым цветом).</summary>
        public static Sprite Walnut
        {
            get
            {
                if (_walnut != null) return _walnut;
                const int n = 128;
                var tex = NewTex("PrimWalnut", n);
                var px = new Color32[n * n];
                var light = new Color(0.82f, 0.62f, 0.38f);
                var dark = new Color(0.42f, 0.27f, 0.13f);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        // чуть вытянутый овал с острым носиком сверху
                        float vx = u / 0.86f, vy = v / (0.96f - 0.06f * Mathf.Max(0f, v));
                        float r = Mathf.Sqrt(vx * vx + vy * vy);
                        float a = Mathf.Clamp01((1f - r) * n * 0.5f);
                        if (a <= 0f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }

                        // морщинки — «извилины» из шума, сильнее к краям
                        float wr = Mathf.PerlinNoise(u * 5.5f + 3.1f, v * 4.2f + 7.7f);
                        float wr2 = Mathf.PerlinNoise(u * 11f + 1.3f, v * 9f + 2.2f);
                        float ridge = 1f - Mathf.Abs(wr * 2f - 1f);
                        float k = Mathf.Lerp(0.55f, 1f, ridge) * Mathf.Lerp(0.85f, 1f, wr2);
                        // объём: свет сверху-слева, тень снизу-справа
                        float shade = Mathf.Clamp01(0.75f + 0.35f * (-u * 0.5f + v * 0.7f) - 0.35f * r * r);
                        // шов посередине
                        float seam = Mathf.Clamp01(1f - Mathf.Abs(u) * 26f);
                        var c = Color.Lerp(dark, light, k * shade);
                        c = Color.Lerp(c, dark * 0.7f, seam * 0.85f);
                        // мягкий блик
                        float hx = u + 0.35f, hy = v - 0.45f;
                        c = Color.Lerp(c, new Color(1f, 0.9f, 0.75f), Mathf.Clamp01(0.35f - (hx * hx + hy * hy) * 2.2f));
                        // тёмный ободок
                        c = Color.Lerp(c, dark * 0.6f, Mathf.Clamp01((r - 0.86f) * 7f));
                        c.a = a;
                        px[y * n + x] = c;
                    }
                tex.SetPixels32(px);
                tex.Apply();
                return _walnut = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            }
        }

        /// <summary>Воронка: тёмное пятно с рваным краем и трещинами (рисовать с белым цветом, альфа в текстуре).</summary>
        public static Sprite Crater
        {
            get
            {
                if (_crater != null) return _crater;
                const int n = 128;
                var tex = NewTex("PrimCrater", n);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        float ang = Mathf.Atan2(v, u);
                        float edge = 0.62f + 0.1f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2f + 5f, Mathf.Sin(ang) * 2f + 5f);
                        // чёткий край ямы, внутри — темнее к центру
                        float hole = Mathf.Clamp01((edge - r) * 40f);
                        float g = Mathf.Lerp(0.04f, 0.2f, r / edge) * Mathf.Lerp(0.8f, 1.2f, Mathf.PerlinNoise(u * 7f, v * 7f));
                        var c = new Color(g, g * 0.85f, g * 0.7f, hole * 0.85f);
                        // вал выброшенной земли вокруг ямы
                        float rimK = Mathf.Clamp01(1f - Mathf.Abs(r - edge - 0.06f) * 14f) * (1f - hole);
                        if (rimK > 0f) c = new Color(0.45f, 0.38f, 0.3f, rimK * 0.55f);
                        // трещины-лучи наружу
                        float rays = Mathf.Pow(Mathf.Abs(Mathf.Sin(ang * 6f + Mathf.PerlinNoise(r * 4f, 1f) * 2.5f)), 60f);
                        float crack = rays * Mathf.Clamp01((1f - r) * 3f) * (1f - hole) * (r > edge ? 1f : 0f);
                        if (crack > c.a) c = new Color(0.1f, 0.08f, 0.07f, crack * 0.8f);
                        px[y * n + x] = c;
                    }
                tex.SetPixels32(px);
                tex.Apply();
                return _crater = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            }
        }

        /// <summary>Треугольник остриём вправо (стрелки-указатели).</summary>
        public static Sprite Triangle => _triangle != null ? _triangle : (_triangle = Make("PrimTriangle", 64, (x, y) =>
            x >= -0.8f && Mathf.Abs(y) <= (1f - x) * 0.5f && x <= 1f));

        /// <summary>Скруглённый прямоугольник для UI (9-slice: Image.type = Sliced).</summary>
        public static Sprite RoundedRect
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int n = 64, r = 20;
                var tex = NewTex("PrimRounded", n);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                return _rounded = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0,
                                                SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            }
        }

        /// <summary>Шахматка 2×2 клетки, 1 клетка = 1 юнит. Для SpriteRenderer с drawMode = Tiled.</summary>
        public static Sprite Checker
        {
            get
            {
                if (_checker != null) return _checker;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "PrimChecker",
                };
                var a = new Color32(255, 255, 255, 255);
                var b = new Color32(243, 241, 236, 255);
                tex.SetPixels32(new[] { a, b, b, a });
                tex.Apply();
                return _checker = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            }
        }

        static Texture2D NewTex(string name, int n) => new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = name,
        };

        /// <summary>Спрайт по функции «точка внутри?» в координатах [-1, 1]. Сглаживание — суперсэмплинг 4×4.</summary>
        static Sprite Make(string name, int n, Func<float, float, bool> inside)
        {
            var tex = NewTex(name, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float u = (x + (sx + 0.5f) / 4f) / n * 2f - 1f;
                            float v = (y + (sy + 0.5f) / 4f) / n * 2f - 1f;
                            if (inside(u, v)) hits++;
                        }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        /// <summary>Создаёт объект со SpriteRenderer.</summary>
        public static GameObject Create(string name, Transform parent, Sprite sprite, Color color,
                                        Vector3 position, Vector2 scale, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        /// <summary>Дочерний спрайт без коллайдера (ствол, глаза и т.п.).</summary>
        public static GameObject Child(string name, Transform parent, Sprite sprite, Color color,
                                       Vector2 localPos, Vector2 localScale, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        /// <summary>Мягкая тень под объектом. Не вращается вместе с ним (компонент KeepWorldOffset).</summary>
        public static GameObject Shadow(Transform parent, float diameter)
        {
            var go = Child("Shadow", parent, Circle, new Color(0f, 0f, 0f, 0.22f), Vector2.zero, Vector2.one, -7);
            go.AddComponent<NoFlash>();
            var keep = go.AddComponent<KeepWorldOffset>();
            keep.offset = new Vector2(0.08f, -0.12f);
            keep.worldScale = new Vector2(diameter, diameter * 0.85f);
            return go;
        }

        /// <summary>Затемнить цвет, не трогая прозрачность.</summary>
        public static Color Dark(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        /// <summary>Осветлить цвет к белому.</summary>
        public static Color Light(Color c, float k) => new Color(c.r + (1 - c.r) * k, c.g + (1 - c.g) * k, c.b + (1 - c.b) * k, c.a);

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static Rigidbody2D AddTopDownBody(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            return rb;
        }
    }

    /// <summary>Держит объект со смещением в мировых координатах и без поворота родителя (тени, полоски HP).</summary>
    public class KeepWorldOffset : MonoBehaviour
    {
        public Vector2 offset;
        public Vector2 worldScale = Vector2.one;

        void LateUpdate()
        {
            var p = transform.parent;
            if (p == null) return;
            transform.position = (Vector2)p.position + offset;
            transform.rotation = Quaternion.identity;
            var ls = p.lossyScale;
            transform.localScale = new Vector3(worldScale.x / Mathf.Max(0.0001f, ls.x), worldScale.y / Mathf.Max(0.0001f, ls.y), 1f);
        }
    }
}
