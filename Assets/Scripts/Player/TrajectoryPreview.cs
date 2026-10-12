// Предпросмотр траектории с рикошетами: пунктир от дула, кольца в точках отскока, метка в конце.
// Красный — траектория вернётся в самого кота, жёлтый — попадёт во врага. Пунктир «бежит» и гаснет к концу.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public enum AimResult { None, Enemy, Self }

    public class TrajectoryPreview : MonoBehaviour
    {
        public float maxLength = 30f;
        public float dotSpacing = 0.32f;
        public float dotSize = 0.09f;
        public float scrollSpeed = 1.2f;       // юнит/с — «бегущий» пунктир
        public int maxDots = 140;
        // тёмный пунктир читается на светлом полу; попадание во врага — насыщенный оранжевый
        public Color normalColor = new Color(0.16f, 0.18f, 0.27f, 0.75f);
        public Color enemyHitColor = new Color(1f, 0.5f, 0.05f, 0.95f);
        public Color selfHitColor = new Color(0.95f, 0.15f, 0.15f, 0.95f);

        public AimResult LastResult { get; private set; }

        PlayerWeapon _weapon;
        PlayerTank _tank;
        Transform _root;
        readonly List<SpriteRenderer> _dots = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _marks = new List<SpriteRenderer>();
        readonly List<Vector2> _points = new List<Vector2>();

        void Awake()
        {
            _weapon = GetComponent<PlayerWeapon>();
            _tank = GetComponent<PlayerTank>();
        }

        void OnDisable() { Hide(); }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        void LateUpdate()
        {
            if (_weapon == null || (_tank != null && !_tank.InputEnabled)) { Hide(); LastResult = AimResult.None; return; }

            var hit = Ricochet.Trace(_weapon.Muzzle, _weapon.AimDirection, _weapon.BulletRadius,
                                     _weapon.maxBounces, maxLength, transform, _points);
            LastResult = hit == null ? AimResult.None : hit.transform.IsChildOf(transform) ? AimResult.Self : AimResult.Enemy;
            Color c = LastResult == AimResult.Self ? selfHitColor : LastResult == AimResult.Enemy ? enemyHitColor : normalColor;
            if (!_weapon.CanFire) c.a *= 0.45f; // перезарядка или нет снарядов — пунктир бледнее
            Draw(c);
        }

        void Draw(Color color)
        {
            float total = 0f;
            for (int i = 1; i < _points.Count; i++) total += Vector2.Distance(_points[i - 1], _points[i]);

            int used = 0;
            float walked = 0f;
            float carry = Mathf.Repeat(Time.time * scrollSpeed, dotSpacing);
            for (int i = 1; i < _points.Count && used < maxDots; i++)
            {
                Vector2 a = _points[i - 1], b = _points[i];
                float len = Vector2.Distance(a, b);
                Vector2 dir = len > 0 ? (b - a) / len : Vector2.zero;
                for (float t = carry; t < len && used < maxDots; t += dotSpacing)
                {
                    var dot = Get(_dots, used++, Prims.Circle, 15);
                    float k = 1f - (walked + t) / Mathf.Max(0.01f, total) * 0.75f; // гаснет к концу
                    dot.transform.position = a + dir * t;
                    dot.transform.localScale = Vector3.one * dotSize * Mathf.Lerp(0.7f, 1.15f, k);
                    dot.color = Prims.WithAlpha(color, color.a * k);
                }
                carry = Mathf.Repeat(carry - len, dotSpacing);
                walked += len;
            }
            Show(_dots, used);

            // кольца в точках отскока + большая метка в конце, если во что-то попадаем
            int marks = 0;
            for (int i = 1; i < _points.Count - 1; i++)
            {
                var m = Get(_marks, marks++, Prims.Ring, 15);
                m.transform.position = _points[i];
                m.transform.localScale = Vector3.one * 0.3f;
                m.color = Prims.WithAlpha(color, color.a * 0.8f);
            }
            if (LastResult != AimResult.None && _points.Count > 1)
            {
                var m = Get(_marks, marks++, Prims.Ring, 15);
                m.transform.position = _points[_points.Count - 1];
                m.transform.localScale = Vector3.one * (0.55f + 0.08f * Mathf.Sin(Time.time * 12f));
                m.color = color;
            }
            Show(_marks, marks);
        }

        SpriteRenderer Get(List<SpriteRenderer> list, int i, Sprite sprite, int order)
        {
            if (_root == null)
            {
                // отдельный объект, чтобы точки не крутились вместе с корпусом
                _root = new GameObject("TrajectoryPreview").transform;
                _root.SetParent(transform.parent, false);
            }
            while (list.Count <= i)
            {
                var go = Prims.Create("Dot", _root, sprite, normalColor, Vector3.zero, Vector2.one * dotSize, order);
                list.Add(go.GetComponent<SpriteRenderer>());
            }
            return list[i];
        }

        static void Show(List<SpriteRenderer> list, int n)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) list[i].enabled = i < n;
        }

        void Hide()
        {
            Show(_dots, 0);
            Show(_marks, 0);
        }
    }
}
