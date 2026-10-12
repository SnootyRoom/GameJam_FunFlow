// След от гусениц: под каждой гусеницей на полу остаются полосатые отпечатки протектора, которые плавно тают,
// а из-под задней части гусениц вылетает пыль. Путь считается для каждой гусеницы отдельно,
// поэтому при развороте на месте следы тоже остаются. Отпечатки живут в корне уровня и пропадают вместе с ним.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public class TrackTrail : MonoBehaviour, ILevelActor
    {
        [Tooltip("Корпус: следы ставятся в его системе координат (вперёд = +X, гусеницы — по ±Y)")]
        public Transform body;
        [Tooltip("Где гусеницы относительно центра корпуса, в юнитах корпуса (до его масштаба)")]
        public float trackOffset = 0.375f;
        [Tooltip("Ширина гусеницы в юнитах корпуса")]
        public float trackWidth = 0.25f;

        [Header("Отпечатки")]
        [Tooltip("Ставить отпечаток каждые N юнитов пути гусеницы")]
        public float markSpacing = 0.16f;
        public float markLifetime = 4f;
        public Color markColor = new Color(0.22f, 0.17f, 0.12f, 0.35f);
        public int maxMarks = 240;
        public int markSortingOrder = -8;   // над полом (−10), под всем остальным
        public Sprite markSprite;           // пусто = встроенный PixelArt.TreadMark

        [Header("Пыль")]
        public bool dust = true;
        [Tooltip("Облачко пыли каждые N юнитов пути гусеницы")]
        public float dustSpacing = 0.6f;
        public Color dustColor = new Color(0.86f, 0.8f, 0.64f, 0.7f);
        public Sprite dustSprite;           // пусто = встроенный PixelArt.Dust

        struct Mark { public SpriteRenderer sr; public float born; }

        readonly List<Mark> _marks = new List<Mark>();
        int _next;                          // кольцевой буфер: самый старый отпечаток переиспользуется
        Transform _root;
        Vector2[] _lastPos;
        float[] _markDist, _dustDist;

        public void Init(LevelContext ctx) { _root = ctx.Root; }

        void Awake()
        {
            if (body == null)
            {
                var b = transform.Find("Body");
                body = b != null ? b : transform;
            }
            if (markSprite == null) markSprite = PixelArt.TreadMark;
            if (dustSprite == null) dustSprite = PixelArt.Dust;
        }

        void Start()
        {
            _lastPos = new[] { TrackPoint(0, +1), TrackPoint(0, -1) };
            _markDist = new float[2];
            _dustDist = new float[2];
        }

        /// <summary>Точка на гусенице: side = +1 левая, −1 правая; along — вдоль корпуса (−0.5 = зад).</summary>
        Vector2 TrackPoint(float along, int side) => body.TransformPoint(new Vector3(along, side * trackOffset, 0f));

        void Update()
        {
            if (Time.timeScale == 0f) return;

            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? +1 : -1;
                Vector2 p = TrackPoint(0, side);
                float d = Vector2.Distance(p, _lastPos[i]);
                if (d > 1f) { _lastPos[i] = p; continue; } // телепорт (новый уровень) — следов не рисуем
                _lastPos[i] = p;

                _markDist[i] += d;
                while (_markDist[i] >= markSpacing)
                {
                    _markDist[i] -= markSpacing;
                    PlaceMark(p);
                }

                if (dust)
                {
                    _dustDist[i] += d;
                    if (_dustDist[i] >= dustSpacing)
                    {
                        _dustDist[i] = 0f;
                        PuffDust(TrackPoint(-0.5f, side));
                    }
                }
            }
            FadeMarks();
        }

        void PlaceMark(Vector2 at)
        {
            Mark m;
            if (_marks.Count < maxMarks)
            {
                var go = new GameObject("TreadMark");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = markSprite;
                sr.sortingOrder = markSortingOrder;
                m = new Mark { sr = sr };
                _marks.Add(m);
                _next = _marks.Count - 1;
            }
            else
            {
                _next = (_next + 1) % _marks.Count;
                m = _marks[_next];
                if (m.sr == null) // уровень сменился — старые отпечатки удалены вместе с корнем
                {
                    _marks.Clear();
                    _next = 0;
                    PlaceMark(at);
                    return;
                }
            }

            var t = m.sr.transform;
            if (t.parent != _root) t.SetParent(_root, false);
            t.position = at;
            t.rotation = body.rotation;
            // отпечаток: вдоль движения — markSpacing, поперёк — ширина гусеницы (с учётом масштаба корпуса)
            Vector2 s = markSprite.bounds.size;
            float across = trackWidth * Mathf.Abs(body.lossyScale.y);
            t.localScale = new Vector3(markSpacing / s.x, across / s.y, 1f);
            m.sr.color = markColor;
            m.born = Time.time;
            _marks[_next] = m;
        }

        void FadeMarks()
        {
            float now = Time.time;
            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                var m = _marks[i];
                if (m.sr == null) { _marks.RemoveAt(i); if (_next >= _marks.Count) _next = 0; continue; }
                float age = (now - m.born) / markLifetime;
                if (age >= 1f) { if (m.sr.enabled) m.sr.enabled = false; continue; }
                if (!m.sr.enabled) m.sr.enabled = true;
                // первую половину жизни держится, потом тает
                var c = markColor;
                c.a *= age < 0.5f ? 1f : 1f - (age - 0.5f) * 2f;
                m.sr.color = c;
            }
        }

        void PuffDust(Vector2 at)
        {
            var go = Prims.Create("Dust", _root, dustSprite, dustColor, at, Vector2.one, 7);
            go.transform.rotation = Quaternion.Euler(0, 0, 90f * Random.Range(0, 4));
            go.AddComponent<FxSprite>().Setup(0.45f, 1.8f);
        }
    }
}
