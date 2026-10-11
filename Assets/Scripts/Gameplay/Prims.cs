// Примитивы-заглушки: белый квадрат и круг, которые красятся цветом.
// Когда появятся спрайты художника — просто назначь префабы в LevelBuilder.
using UnityEngine;

namespace CatTanks
{
    public static class Prims
    {
        enum Shape { Square, Circle, Triangle, Ring }
        static Sprite _square, _circle, _triangle, _ring;

        /// <summary>Квадрат 1×1 юнит (64 px при PPU 64).</summary>
        public static Sprite Square { get { if (_square == null) _square = Make(Shape.Square); return _square; } }

        /// <summary>Круг диаметром 1 юнит.</summary>
        public static Sprite Circle { get { if (_circle == null) _circle = Make(Shape.Circle); return _circle; } }

        /// <summary>Треугольник, смотрит вправо (стрелка-указатель).</summary>
        public static Sprite Triangle { get { if (_triangle == null) _triangle = Make(Shape.Triangle); return _triangle; } }

        /// <summary>Кольцо (прицел).</summary>
        public static Sprite Ring { get { if (_ring == null) _ring = Make(Shape.Ring); return _ring; } }

        static Sprite Make(Shape shape)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "Prim" + shape,
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float dx = fx - n / 2f, dy = fy - n / 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f;
                    switch (shape)
                    {
                        case Shape.Circle: a = Mathf.Clamp01(n / 2f - r); break;
                        case Shape.Ring: a = Mathf.Clamp01(n / 2f - r) * Mathf.Clamp01(r - (n / 2f - 7f)); break;
                        case Shape.Triangle:
                            // вершина справа по центру, основание слева: |dy| <= (n - fx) / 2
                            a = Mathf.Clamp01((n - fx) / 2f - Mathf.Abs(dy) + 0.5f);
                            break;
                    }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
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
