// Эффекты-заглушки: свой лёгкий пул частиц на SpriteRenderer (без материалов и ассетов).
// Всё визуальное «сочное» — здесь. Когда появятся партиклы художника, меняем реализацию, а не вызовы.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public static class Fx
    {
        /// <summary>HUD подписывается и рисует всплывающий текст над точкой мира.</summary>
        public static event Action<Vector2, string, Color> TextRequested;

        public static void Text(Vector2 at, string text, Color color) => TextRequested?.Invoke(at, text, color);

        /// <summary>Искры рикошета/попадания.</summary>
        public static void Spark(Vector2 at, Color? color = null, int count = 5)
        {
            var c = color ?? new Color(1f, 0.9f, 0.45f);
            FxParticles.Burst(at, count, c, speed: 4f, size: 0.12f, life: 0.18f, sprite: Prims.Square);
            FxParticles.Spawn(at, Vector2.zero, Prims.WithAlpha(c, 0.7f), 0.5f, 0.1f, Prims.Glow, growTo: 0.9f);
        }

        /// <summary>Вспышка у дула.</summary>
        public static void MuzzleFlash(Vector2 at, Vector2 dir, Color color)
        {
            FxParticles.Spawn(at, Vector2.zero, Prims.WithAlpha(Prims.Light(color, 0.5f), 0.9f), 0.45f, 0.07f, Prims.Glow, growTo: 0.8f);
            FxParticles.Burst(at, 3, color, speed: 3f, size: 0.08f, life: 0.12f, dir: dir, spreadDeg: 35f);
        }

        /// <summary>Взрыв: вспышка, кольцо, обломки цвета объекта, дым.</summary>
        public static void Explosion(Vector2 at, Color color, float scale = 1f)
        {
            FxParticles.Spawn(at, Vector2.zero, new Color(1f, 0.95f, 0.75f, 0.9f), 1.4f * scale, 0.12f, Prims.Glow, growTo: 2.6f * scale);
            FxParticles.Spawn(at, Vector2.zero, new Color(1f, 0.8f, 0.4f, 0.8f), 0.3f, 0.3f, Prims.Ring, growTo: 2.2f * scale);
            FxParticles.Burst(at, Mathf.RoundToInt(10 * scale), color, speed: 6f * scale, size: 0.2f, life: 0.5f, sprite: Prims.Square, spin: true);
            FxParticles.Burst(at, Mathf.RoundToInt(6 * scale), new Color(0.35f, 0.35f, 0.38f, 0.6f), speed: 1.2f, size: 0.45f * scale, life: 0.9f, sprite: Prims.Circle, growTo: 0.9f * scale);
        }

        /// <summary>Удар ореха: вспышка, ударные волны, скорлупа, пыль столбом, камни.</summary>
        public static void NutImpact(Vector2 at, float radius)
        {
            FxParticles.Spawn(at, Vector2.zero, new Color(1f, 0.95f, 0.8f, 0.95f), radius, 0.15f, Prims.Glow, growTo: radius * 3.2f, sortingOrder: 30);
            FxParticles.Spawn(at, Vector2.zero, new Color(1f, 0.85f, 0.55f, 0.9f), 0.4f, 0.35f, Prims.Ring, growTo: radius * 2.6f, sortingOrder: 30);
            FxParticles.Spawn(at, Vector2.zero, new Color(0.9f, 0.8f, 0.65f, 0.6f), 0.4f, 0.6f, Prims.Ring, growTo: radius * 4f, sortingOrder: 29);
            // скорлупа ореха
            FxParticles.Burst(at, 14, new Color(0.55f, 0.36f, 0.18f), speed: 9f, size: 0.35f, life: 0.9f, sprite: Prims.Triangle, spin: true, growTo: 0.25f);
            FxParticles.Burst(at, 10, new Color(0.78f, 0.6f, 0.38f), speed: 7f, size: 0.22f, life: 0.8f, sprite: Prims.Square, spin: true, growTo: 0.15f);
            // пыль: кольцо облаков по краю + столб в центре
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                FxParticles.Spawn(at + dir * radius * 0.6f, dir * UnityEngine.Random.Range(2f, 4f), new Color(0.72f, 0.66f, 0.56f, 0.55f),
                                  0.6f, UnityEngine.Random.Range(0.9f, 1.4f), Prims.Circle, growTo: 1.6f, sortingOrder: 26);
            }
            FxParticles.Burst(at, 8, new Color(0.6f, 0.55f, 0.48f, 0.5f), speed: 1.5f, size: 1.2f, life: 1.6f, sprite: Prims.Circle, growTo: 2.4f);
            Spark(at, new Color(1f, 0.85f, 0.4f), 16);
        }

        /// <summary>Расходящееся кольцо: ульта, подбор.</summary>
        public static void Pulse(Vector2 at, float radius, Color color, float duration = 0.35f)
        {
            FxParticles.Spawn(at, Vector2.zero, color, 0.2f, duration, Prims.Ring, growTo: radius * 2f);
            FxParticles.Spawn(at, Vector2.zero, Prims.WithAlpha(color, color.a * 0.5f), 0.2f, duration * 0.8f, Prims.Glow, growTo: radius * 1.6f);
        }

        /// <summary>Конфетти (победа).</summary>
        public static void Confetti(Vector2 at)
        {
            Color[] palette = { new Color(1f, 0.8f, 0.2f), new Color(0.4f, 0.85f, 1f), new Color(1f, 0.45f, 0.6f), new Color(0.5f, 1f, 0.5f) };
            for (int i = 0; i < 36; i++)
                FxParticles.Burst(at, 1, palette[i % palette.Length], speed: UnityEngine.Random.Range(3f, 8f), size: 0.14f, life: 1.2f, sprite: Prims.Square, spin: true);
        }

        /// <summary>След гусениц (лежит на полу и медленно гаснет).</summary>
        public static void TrackMark(Vector2 at, float angleDeg)
        {
            FxParticles.Spawn(at, Vector2.zero, new Color(0.2f, 0.17f, 0.12f, 0.16f), 0.16f, 3.5f, Prims.Square, growTo: 0.16f,
                              rotation: angleDeg, sortingOrder: -9, scaleY: 0.32f);
        }

        public static void Dust(Vector2 at)
        {
            FxParticles.Spawn(at, UnityEngine.Random.insideUnitCircle * 0.4f, new Color(0.75f, 0.7f, 0.6f, 0.35f),
                              0.18f, 0.5f, Prims.Circle, growTo: 0.4f, sortingOrder: -6);
        }

        public static void Smoke(Vector2 at)
        {
            FxParticles.Spawn(at, new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f), 0.8f), new Color(0.25f, 0.25f, 0.28f, 0.45f),
                              0.2f, 0.8f, Prims.Circle, growTo: 0.6f, sortingOrder: 16);
        }

        public static void Trail(Vector2 at, Color color)
        {
            FxParticles.Spawn(at, Vector2.zero, Prims.WithAlpha(color, 0.45f), 0.16f, 0.18f, Prims.Circle, growTo: 0.04f, sortingOrder: 19);
        }
    }

    /// <summary>Пул частиц. Один объект на всю игру, частицы обновляются в одном Update.</summary>
    public class FxParticles : MonoBehaviour
    {
        struct P
        {
            public SpriteRenderer sr;
            public Vector2 vel;
            public Color color;
            public float age, life, size0, size1, spin, scaleY;
        }

        const int Max = 700;
        static FxParticles _instance;
        readonly List<P> _alive = new List<P>(256);
        readonly Stack<SpriteRenderer> _free = new Stack<SpriteRenderer>();

        static FxParticles Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("FxParticles");
                _instance = go.AddComponent<FxParticles>();
                return _instance;
            }
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        public static void Spawn(Vector2 pos, Vector2 vel, Color color, float size, float life, Sprite sprite,
                                 float growTo = -1f, float rotation = 0f, int sortingOrder = 22, float spin = 0f, float scaleY = 1f)
        {
            var inst = Instance;
            if (inst._alive.Count >= Max) return;
            var sr = inst._free.Count > 0 ? inst._free.Pop() : inst.NewRenderer();
            sr.gameObject.SetActive(true);
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            sr.color = color;
            sr.transform.position = pos;
            sr.transform.rotation = Quaternion.Euler(0, 0, rotation);
            sr.transform.localScale = new Vector3(size, size * scaleY, 1f);
            inst._alive.Add(new P { sr = sr, vel = vel, color = color, life = life, size0 = size, size1 = growTo < 0 ? size * 0.2f : growTo, spin = spin, scaleY = scaleY });
        }

        public static void Burst(Vector2 pos, int count, Color color, float speed, float size, float life,
                                 Sprite sprite = null, Vector2 dir = default, float spreadDeg = 360f, bool spin = false, float growTo = -1f)
        {
            float baseAngle = dir == Vector2.zero ? 0f : Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                float a = (baseAngle + UnityEngine.Random.Range(-spreadDeg, spreadDeg) * 0.5f) * Mathf.Deg2Rad;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (speed * UnityEngine.Random.Range(0.4f, 1f));
                Spawn(pos, v, color, size * UnityEngine.Random.Range(0.7f, 1.2f), life * UnityEngine.Random.Range(0.7f, 1.2f),
                      sprite ?? Prims.Square, growTo, UnityEngine.Random.Range(0f, 360f), spin: spin ? UnityEngine.Random.Range(-720f, 720f) : 0f);
            }
        }

        public static void ClearAll()
        {
            if (_instance == null) return;
            foreach (var p in _instance._alive) _instance.Release(p.sr);
            _instance._alive.Clear();
        }

        SpriteRenderer NewRenderer()
        {
            var go = new GameObject("p");
            go.transform.SetParent(transform, false);
            return go.AddComponent<SpriteRenderer>();
        }

        void Release(SpriteRenderer sr)
        {
            if (sr == null) return;
            sr.gameObject.SetActive(false);
            _free.Push(sr);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var p = _alive[i];
                p.age += dt;
                if (p.age >= p.life || p.sr == null)
                {
                    Release(p.sr);
                    _alive[i] = _alive[_alive.Count - 1];
                    _alive.RemoveAt(_alive.Count - 1);
                    continue;
                }
                float t = p.age / p.life;
                p.vel *= Mathf.Exp(-4f * dt); // сопротивление: частицы «оседают»
                var tr = p.sr.transform;
                tr.position += (Vector3)(p.vel * dt);
                if (p.spin != 0f) tr.Rotate(0, 0, p.spin * dt);
                float s = Mathf.Lerp(p.size0, p.size1, 1f - (1f - t) * (1f - t));
                tr.localScale = new Vector3(s, s * p.scaleY, 1f);
                p.sr.color = new Color(p.color.r, p.color.g, p.color.b, p.color.a * (1f - t * t));
                _alive[i] = p;
            }
        }
    }
}
