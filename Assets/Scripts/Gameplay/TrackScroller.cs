// Анимация гусениц смещением текстуры (UV offset). Рисунок гусениц берётся из спрайта корпуса:
// поверх каждой гусеницы кладётся полоска с той же текстурой, сдвинутой по U на offset пикселей.
// Рисунок периодичный, поэтому смещение крутится по кругу внутри одного периода: полоска режется на
// «основную» часть [x+o, x+W) и «хвост» [x, x+o), который встаёт в конец — получается бесшовный повтор.
// Шейдеры спрайтов игнорируют material.mainTextureOffset, поэтому UV сдвигаем через прямоугольник спрайта.
// Скорость считается по движению корпуса: вперёд/назад — обе гусеницы, разворот на месте — в разные стороны.
using UnityEngine;

namespace CatTanks
{
    public class TrackScroller : MonoBehaviour
    {
        [Tooltip("Корпус: из его спрайта берётся рисунок гусениц, полоски рисуются поверх него")]
        public SpriteRenderer body;

        [Tooltip("Повторяющаяся часть каждой гусеницы в пикселях спрайта корпуса (от левого нижнего угла). " +
                 "Ширина должна делиться на период")]
        public RectInt[] tracks = { new RectInt(4, 0, 56, 16), new RectInt(4, 48, 56, 16) };

        [Tooltip("Через сколько пикселей рисунок гусеницы повторяется")]
        [Min(1)] public int periodPx = 4;

        [Tooltip("Сколько пикселей рисунка прокручивается на 1 юнит пути. Отрицательное — в другую сторону. " +
                 "Слишком большое значение даёт эффект «стробоскопа» (рисунок будто едет назад)")]
        public float pixelsPerUnit = 16f;

        class Track
        {
            public SpriteRenderer main, tail;
            public Sprite[] mainFrames, tailFrames;   // по кадру на каждое целое смещение 0..period-1
            public Vector2 origin;                    // левый нижний угол полоски в локальных координатах корпуса
            public float width;                       // ширина полоски в юнитах корпуса
            public float side;                        // смещение гусеницы от оси танка (в мире), + = левая
            public float offset;                      // текущее смещение в пикселях
        }

        Track[] _tracks;
        Vector2 _lastPos;
        float _lastAngle;

        void Awake()
        {
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            if (body == null || body.sprite == null) { enabled = false; return; }
            Build();
            var hp = GetComponentInParent<Health>();
            if (hp != null) hp.CacheRenderers(); // вспышка урона должна знать о новых спрайтах
        }

        void Start()
        {
            _lastPos = body.transform.position;
            _lastAngle = body.transform.eulerAngles.z;
        }

        void Build()
        {
            // Полоски гусениц встают ровно на ступень выше корпуса. Всё, что уже было выше корпуса (башня с дулом, кот),
            // поднимаем на ступень — иначе башня окажется на одном уровне с гусеницами и Unity нарисует их в случайном порядке.
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
                if (sr != body && sr.sortingLayerID == body.sortingLayerID && sr.sortingOrder > body.sortingOrder)
                    sr.sortingOrder += 1;

            var src = body.sprite;
            float ppu = src.pixelsPerUnit;
            Rect r = src.rect;          // где спрайт корпуса лежит в текстуре
            Vector2 pivot = src.pivot;  // в пикселях от левого нижнего угла спрайта

            _tracks = new Track[tracks.Length];
            for (int i = 0; i < tracks.Length; i++)
            {
                var px = tracks[i];
                var t = new Track
                {
                    origin = new Vector2(px.x - pivot.x, px.y - pivot.y) / ppu,
                    width = px.width / ppu,
                    side = (px.y + px.height / 2f - pivot.y) / ppu * body.transform.lossyScale.y,
                    mainFrames = new Sprite[periodPx],
                    tailFrames = new Sprite[periodPx],
                };
                for (int o = 0; o < periodPx; o++)
                {
                    t.mainFrames[o] = Slice(src, new Rect(r.x + px.x + o, r.y + px.y, px.width - o, px.height), ppu);
                    if (o > 0) t.tailFrames[o] = Slice(src, new Rect(r.x + px.x, r.y + px.y, o, px.height), ppu);
                }
                t.main = MakeRenderer("Track" + i, t.origin);
                t.tail = MakeRenderer("Track" + i + "Tail", t.origin);
                _tracks[i] = t;
                Apply(t);
            }
        }

        static Sprite Slice(Sprite src, Rect rect, float ppu)
        {
            var s = Sprite.Create(src.texture, rect, Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
            s.name = src.name + "_track";
            return s;
        }

        SpriteRenderer MakeRenderer(string name, Vector2 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(body.transform, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = body.sharedMaterial;
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder + 1; // поверх неподвижных гусениц корпуса
            sr.color = body.color;
            return sr;
        }

        void Update()
        {
            var tr = body.transform;
            Vector2 pos = tr.position;
            float angle = tr.eulerAngles.z;
            Vector2 delta = pos - _lastPos;
            float turn = Mathf.DeltaAngle(_lastAngle, angle) * Mathf.Deg2Rad;
            _lastPos = pos;
            _lastAngle = angle;
            if (delta.sqrMagnitude < 1e-10f && Mathf.Abs(turn) < 1e-6f) return;

            float forward = Vector2.Dot(delta, tr.right);
            foreach (var t in _tracks)
            {
                // путь гусеницы = движение вперёд минус вклад поворота (левая при повороте влево идёт назад)
                float path = forward - turn * t.side;
                t.offset = Mathf.Repeat(t.offset + path * pixelsPerUnit, periodPx);
                Apply(t);
            }
        }

        void Apply(Track t)
        {
            int o = Mathf.FloorToInt(t.offset) % periodPx; // целые пиксели — пиксель-арт остаётся чётким
            t.main.sprite = t.mainFrames[o];
            bool hasTail = o > 0;
            t.tail.enabled = hasTail;
            if (hasTail)
            {
                t.tail.sprite = t.tailFrames[o];
                t.tail.transform.localPosition = t.origin + Vector2.right * (t.width - o / body.sprite.pixelsPerUnit);
            }
        }

        void OnDestroy()
        {
            if (_tracks == null) return;
            foreach (var t in _tracks)
            {
                foreach (var s in t.mainFrames) if (s != null) Destroy(s);
                foreach (var s in t.tailFrames) if (s != null) Destroy(s);
            }
        }
    }
}
