// Полоска здоровья из сегментов внутри рамки (hp_frame / boss_hp_frame): один сегмент = одно HP.
// Сегменты делят внутреннюю часть рамки поровну, между ними — зазор в пиксель картинки.
// Потеря: сегмент вспыхивает «lost» (светлым) и гаснет до «empty», рамка вздрагивает. Лечение: сегмент подпрыгивает.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class SegmentBar : MonoBehaviour
    {
        [Tooltip("Внутренняя часть рамки — сюда кладутся сегменты")]
        public RectTransform inner;
        [Tooltip("То, что трясётся при уроне (обычно вся полоска с рамкой)")]
        public RectTransform shakeTarget;
        public Sprite full, empty, lost;
        [Tooltip("Зазор между сегментами, px UI")]
        public float gap = 6f;
        public float lostTime = 0.45f;
        public float shakeAmount = 8f;

        readonly List<Image> _segments = new List<Image>();
        readonly List<float> _lostUntil = new List<float>();
        readonly List<float> _pop = new List<float>();
        int _hp = -1, _max = -1;
        float _shake;
        Vector2 _shakeRest;

        void Awake() { if (shakeTarget != null) _shakeRest = shakeTarget.anchoredPosition; }

        /// <summary>Показать hp из max. instant — без анимаций (новый уровень).</summary>
        public void Set(int hp, int max, bool instant = false)
        {
            max = Mathf.Max(0, max);
            hp = Mathf.Clamp(hp, 0, max); // на всякий случай: номер сегмента не может быть отрицательным или больше max
            if (max != _max) Rebuild(max);
            if (!instant && _hp >= 0)
            {
                for (int i = hp; i < _hp && i < max; i++) _lostUntil[i] = Time.unscaledTime + lostTime; // потерянные — вспышка
                for (int i = _hp; i < hp && i < max; i++) _pop[i] = 1f;                                 // вылеченные — прыжок
                if (hp < _hp) _shake = 1f;
            }
            else for (int i = 0; i < max; i++) { _lostUntil[i] = 0f; _pop[i] = 0f; }
            _hp = hp;
            Refresh();
        }

        void Rebuild(int max)
        {
            _max = max;
            while (_segments.Count < max)
            {
                var rt = UIFactory.Rect(inner, "Segment" + _segments.Count, Vector2.zero, Vector2.zero, Vector2.zero);
                rt.pivot = new Vector2(0.5f, 0.5f);
                _segments.Add(UIFactory.Image(rt, full, Color.white));
                _lostUntil.Add(0f);
                _pop.Add(0f);
            }
            // делим внутреннюю часть поровну: ширина сегмента = (вся ширина − зазоры) / max
            float w = inner.rect.width, h = inner.rect.height;
            float seg = max > 0 ? (w - gap * (max - 1)) / max : 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                var rt = _segments[i].rectTransform;
                rt.gameObject.SetActive(i < max);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(Mathf.Round(seg), h);
                rt.anchoredPosition = new Vector2(Mathf.Round(i * (seg + gap) + seg / 2f), 0f);
            }
        }

        void Refresh()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _max; i++)
            {
                var img = _segments[i];
                if (i < _hp) img.sprite = full;
                else if (now < _lostUntil[i] && lost != null)
                    img.sprite = Mathf.Repeat((_lostUntil[i] - now) * 10f, 1f) < 0.6f ? lost : empty; // мигает и гаснет
                else img.sprite = empty != null ? empty : full;
                img.color = i < _hp || empty != null ? Color.white : new Color(1, 1, 1, 0.25f);
                img.rectTransform.localScale = Vector3.one * (1f + 0.35f * _pop[i]);
            }
        }

        void Update()
        {
            if (_max <= 0) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _max; i++) _pop[i] = Mathf.MoveTowards(_pop[i], 0f, dt * 4f);
            Refresh();

            if (shakeTarget == null) return;
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 3f);
            float a = shakeAmount * _shake;
            shakeTarget.anchoredPosition = _shakeRest + new Vector2(Mathf.Round(Random.Range(-a, a)), Mathf.Round(Random.Range(-a, a)));
        }
    }
}
