// Визуал предупреждения о выстреле: враг мигает (всё чаще к выстрелу), от ствола — пунктир по траектории снаряда.
// Когда и куда стрелять, решает EnemyShooter — этот компонент только показывает. Добавится к стрелку сам.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public class ShotTelegraph : MonoBehaviour
    {
        [Tooltip("Цвет, которым мигает враг перед выстрелом")]
        public Color blinkColor = new Color(1f, 0.3f, 0.3f);
        [Tooltip("Показывать пунктир, куда полетит снаряд")]
        public bool showLine = true;
        public Color lineColor = new Color(1f, 0.35f, 0.35f, 0.85f);
        [Tooltip("Длина пунктира (юниты). Короче — только направление, длиннее — видно и рикошет")]
        public float lineLength = 5f;
        public float dotSpacing = 0.3f;
        public int sortingOrder = 9;

        Transform _dotsParent;
        SpriteRenderer[] _renderers;
        Color[] _colors;
        readonly List<Vector2> _points = new List<Vector2>();
        readonly List<SpriteRenderer> _dots = new List<SpriteRenderer>();
        bool _showing;

        /// <summary>Куда класть точки пунктира (корень уровня — чтобы не крутились со стрелком и удалялись с уровнем).</summary>
        public void SetDotsParent(Transform parent) { _dotsParent = parent; }

        /// <summary>Каждый кадр зарядки. progress 0…1 — сколько осталось до выстрела.</summary>
        public void Show(Vector2 muzzle, Vector2 dir, float bulletRadius, int bounces, float progress)
        {
            if (!_showing) Begin();
            // мигание ускоряется к выстрелу
            bool on = Mathf.Repeat(progress * Mathf.Lerp(3f, 9f, progress), 1f) < 0.5f;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].color = Color.Lerp(_colors[i], blinkColor, on ? 0.85f : 0.2f);
            if (showLine) DrawLine(muzzle, dir, bulletRadius, bounces, Mathf.Lerp(0.35f, 1f, progress));
        }

        public void Hide()
        {
            if (!_showing) return;
            _showing = false;
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].color = _colors[i];
            foreach (var d in _dots) if (d != null) d.gameObject.SetActive(false);
        }

        void Begin()
        {
            _showing = true;
            if (_renderers == null)
            {
                _renderers = GetComponentsInChildren<SpriteRenderer>();
                _colors = new Color[_renderers.Length];
            }
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _colors[i] = _renderers[i].color;
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            foreach (var d in _dots) if (d != null) Destroy(d.gameObject);
        }

        /// <summary>Пунктир по траектории будущего снаряда (тот же расчёт, что у полёта). Пересчёт каждый кадр — краб ходит.</summary>
        void DrawLine(Vector2 from, Vector2 dir, float radius, int bounces, float alpha)
        {
            Ricochet.Simulate(from, dir, radius, bounces, lineLength, transform, null, _points);
            int used = 0;
            for (int s = 1; s < _points.Count; s++)
            {
                Vector2 a = _points[s - 1], b = _points[s];
                float len = Vector2.Distance(a, b);
                for (float t = 0f; t < len; t += dotSpacing)
                {
                    var dot = TakeDot(used++);
                    dot.transform.position = Vector2.Lerp(a, b, t / Mathf.Max(0.001f, len));
                    var c = lineColor;
                    c.a *= alpha * Mathf.Lerp(1f, 0.25f, used * dotSpacing / lineLength); // к концу тает
                    dot.color = c;
                }
            }
            for (int i = used; i < _dots.Count; i++) _dots[i].gameObject.SetActive(false);
        }

        SpriteRenderer TakeDot(int index)
        {
            if (index < _dots.Count)
            {
                _dots[index].gameObject.SetActive(true);
                return _dots[index];
            }
            var go = new GameObject("ShotWarning");
            go.transform.SetParent(_dotsParent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.AimDot;
            sr.sortingOrder = sortingOrder;
            if (_renderers != null && _renderers.Length > 0 && _renderers[0] != null) sr.sharedMaterial = _renderers[0].sharedMaterial;
            _dots.Add(sr);
            return sr;
        }
    }
}
