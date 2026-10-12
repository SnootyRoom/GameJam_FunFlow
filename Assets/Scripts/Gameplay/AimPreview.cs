// Предпросмотр траектории в пиксельном стиле: пунктир из точек с контуром, точки «бегут» от ствола,
// к концу тают; в точке рикошета — ромбик, там, где снаряд во что-то врежется, — крестик.
// Траекторию считает Ricochet.Simulate — тот же код, что у снаряда и ИИ, поэтому прицел всегда честный.
// Вешается рядом с PlayerTank. Спрайты можно заменить своими (пусто = встроенные из PixelArt).
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public class AimPreview : MonoBehaviour
    {
        public PlayerTank tank;

        [Header("Траектория")]
        [Tooltip("Длина в юнитах (по всем отрезкам)")]
        public float maxDistance = 12f;
        public float bulletRadius = 0.12f;
        [Tooltip("Расстояние между точками")]
        public float dotSpacing = 0.32f;
        [Tooltip("Скорость «бега» точек от ствола, юнитов в секунду")]
        public float scrollSpeed = 1.2f;

        [Header("Вид")]
        public Color color = new Color(1f, 0.95f, 0.6f, 1f);   // как снаряд
        [Tooltip("Прозрачность в конце траектории")]
        [Range(0f, 1f)] public float endAlpha = 0.15f;
        [Tooltip("Прозрачность крестика в точке попадания")]
        [Range(0f, 1f)] public float endMarkerAlpha = 0.85f;
        public Sprite dotSprite, bounceSprite, endSprite;
        public int sortingOrder = 9;   // под танком, над полом и стенами

        readonly List<Vector2> _points = new List<Vector2>();
        readonly List<SpriteRenderer> _dots = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _bounces = new List<SpriteRenderer>();
        SpriteRenderer _end;
        Transform _root;
        int _usedDots, _usedBounces;

        void Awake()
        {
            if (tank == null) tank = GetComponent<PlayerTank>();
            if (dotSprite == null) dotSprite = PixelArt.AimDot;
            if (bounceSprite == null) bounceSprite = PixelArt.AimBounce;
            if (endSprite == null) endSprite = PixelArt.AimEnd;
            _root = new GameObject("AimPreview").transform;
            _root.SetParent(transform, false);
            _end = Make(endSprite);
        }

        void LateUpdate()
        {
            _usedDots = _usedBounces = 0;
            bool show = tank != null && tank.isActiveAndEnabled && Time.timeScale > 0f;
            if (show)
            {
                Ricochet.Simulate(tank.MuzzlePosition, tank.AimDirection, bulletRadius, tank.maxBounces, maxDistance,
                                  tank.transform, null, _points);
                show = _points.Count > 1;
            }
            if (show) Layout();
            HideUnused();
        }

        void Layout()
        {
            float total = 0f;
            for (int i = 1; i < _points.Count; i++) total += Vector2.Distance(_points[i - 1], _points[i]);
            if (total < 0.01f) return;

            // точки: каждые dotSpacing, со сдвигом по времени — пунктир «бежит» вперёд
            float phase = Mathf.Repeat(Time.unscaledTime * scrollSpeed, dotSpacing);
            float walked = 0f;
            for (int i = 1; i < _points.Count; i++)
            {
                Vector2 a = _points[i - 1], b = _points[i];
                float len = Vector2.Distance(a, b);
                float first = Mathf.Repeat(phase - walked, dotSpacing);
                for (float s = first; s < len; s += dotSpacing)
                {
                    float along = (walked + s) / total;
                    var dot = Take(_dots, ref _usedDots, dotSprite);
                    dot.transform.position = Vector2.Lerp(a, b, s / len);
                    dot.color = Fade(along);
                }
                walked += len;

                // промежуточные точки ломаной — рикошеты
                if (i < _points.Count - 1)
                {
                    var bounce = Take(_bounces, ref _usedBounces, bounceSprite);
                    bounce.transform.position = b;
                    bounce.color = Fade(walked / total);
                }
            }

            // если траектория кончилась раньше дальности — снаряд во что-то врежется: ставим крестик
            bool hits = total < maxDistance - 0.05f;
            _end.gameObject.SetActive(hits);
            if (hits)
            {
                _end.transform.position = _points[_points.Count - 1];
                var c = color;
                c.a *= endMarkerAlpha; // крестик — важная подсказка, он не тает вместе с пунктиром
                _end.color = c;
            }
        }

        Color Fade(float along)
        {
            var c = color;
            c.a *= Mathf.Lerp(1f, endAlpha, along);
            return c;
        }

        SpriteRenderer Take(List<SpriteRenderer> pool, ref int used, Sprite sprite)
        {
            if (used == pool.Count) pool.Add(Make(sprite));
            var sr = pool[used++];
            sr.gameObject.SetActive(true);
            return sr;
        }

        SpriteRenderer Make(Sprite sprite)
        {
            var go = new GameObject("Aim");
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            // материал как у спрайтов танка — совместим с текущим рендером (URP 2D)
            var body = tank != null ? tank.GetComponentInChildren<SpriteRenderer>() : null;
            if (body != null) sr.sharedMaterial = body.sharedMaterial;
            return sr;
        }

        void HideUnused()
        {
            for (int i = _usedDots; i < _dots.Count; i++) _dots[i].gameObject.SetActive(false);
            for (int i = _usedBounces; i < _bounces.Count; i++) _bounces[i].gameObject.SetActive(false);
            if (_usedDots == 0 && _end != null) _end.gameObject.SetActive(false);
        }
    }
}
