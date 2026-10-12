// Примитивы-заглушки: белый квадрат и круг, которые красятся цветом.
// Когда появятся спрайты художника — просто назначь префабы в LevelBuilder.
using UnityEngine;

namespace CatTanks
{
    public static class Prims
    {
        static Sprite _square, _circle;

        /// <summary>Квадрат 1×1 юнит (64 px при PPU 64).</summary>
        public static Sprite Square { get { if (_square == null) _square = Make(false); return _square; } }

        /// <summary>Круг диаметром 1 юнит.</summary>
        public static Sprite Circle { get { if (_circle == null) _circle = Make(true); return _circle; } }

        static Sprite Make(bool circle)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = circle ? "PrimCircle" : "PrimSquare",
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    byte a = 255;
                    if (circle)
                    {
                        float dx = x + 0.5f - n / 2f, dy = y + 0.5f - n / 2f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        a = (byte)(Mathf.Clamp01(n / 2f - d) * 255); // сглаженный край
                    }
                    px[y * n + x] = new Color32(255, 255, 255, a);
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

        /// <summary>Затемнить цвет, не трогая прозрачность.</summary>
        public static Color Dark(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

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
}
