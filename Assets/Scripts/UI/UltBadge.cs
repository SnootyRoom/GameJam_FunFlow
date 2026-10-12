// Значок ульты «Орех»: круглая плашка с орехом и кольцом из сегментов (1 сегмент = 1 убийство).
// Каждое убийство «выбивает» сегмент: он вспыхивает, плашка вздрагивает. Заряд готов — орех
// цветной, вокруг золотое свечение и волна, в углу — число зарядов, снизу подсказка клавиши.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks
{
    public class UltBadge : MonoBehaviour
    {
        static readonly Color Filled = new Color(1f, 0.74f, 0.28f);
        static readonly Color Empty = new Color(1f, 1f, 1f, 0.13f);

        RectTransform _root, _segRoot, _icon, _burst, _counter;
        CanvasGroup _group;
        Image _glow, _iconImg, _burstImg;
        Text _counterText, _keyHint;
        readonly List<Image> _segments = new List<Image>();
        readonly List<float> _segPunch = new List<float>();
        Vector2 _basePos;
        int _lastProgress = -1, _lastCharges = -1, _segCount = -1;
        float _shake, _pop, _burstT = 1f, _flash;

        public static UltBadge Create(RectTransform parent)
        {
            var root = UiKit.Node("UltBadge", parent).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(132, 132));
            var b = root.gameObject.AddComponent<UltBadge>();
            b.Build(root);
            return b;
        }

        void Build(RectTransform root)
        {
            _root = root;
            _basePos = root.anchoredPosition;
            _group = root.gameObject.AddComponent<CanvasGroup>();

            _glow = UiKit.Img(root, Prims.Glow, Prims.WithAlpha(Filled, 0f), "Glow");
            _glow.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280, 280));

            _burstImg = UiKit.Img(root, Prims.Ring, Prims.WithAlpha(Filled, 0f), "Burst");
            _burst = _burstImg.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132, 132));

            var bg = UiKit.Img(root, Prims.Circle, new Color(0.07f, 0.08f, 0.12f, 0.85f), "Bg");
            bg.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(114, 114));

            _segRoot = UiKit.Node("Segments", root).Stretch();

            _iconImg = UiKit.Img(root, Prims.Walnut, Color.white, "Nut");
            _icon = _iconImg.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72));

            var counterBg = UiKit.Img(root, Prims.Circle, Filled, "Counter");
            _counter = counterBg.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46, 46), new Vector2(38, 38));
            _counterText = UiKit.Label(_counter, "", 26, new Color(0.22f, 0.12f, 0.02f), TextAnchor.MiddleCenter, FontStyle.Bold, false);
            _counterText.Rt().Stretch();

            // подсказка справа от значка — снизу её срезает край экрана
            _keyHint = UiKit.Label(root, "ПКМ / Q", 24, UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            _keyHint.horizontalOverflow = HorizontalWrapMode.Overflow;
            _keyHint.Rt().Place(new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(260, 34));
        }

        void RebuildSegments(int n)
        {
            foreach (var s in _segments) Destroy(s.gameObject);
            _segments.Clear();
            _segPunch.Clear();
            const float gap = 0.035f; // доля круга между сегментами
            for (int i = 0; i < n; i++)
            {
                var img = UiKit.Img(_segRoot, Prims.Ring, Empty, "Seg");
                img.preserveAspect = false;
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Top;
                img.fillClockwise = true;
                img.fillAmount = Mathf.Max(0.01f, 1f / n - gap);
                var rt = img.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132, 132));
                rt.localRotation = Quaternion.Euler(0, 0, -360f * i / n - gap * 180f);
                _segments.Add(img);
                _segPunch.Add(0f);
            }
            _segCount = n;
        }

        /// <summary>Обновить по текущему состоянию ульты. Вызывает HudView каждый кадр.</summary>
        public void Tick(PlayerUltimate u, float dt)
        {
            if (u == null) { _group.alpha = 0f; return; }
            if (_segCount != u.killsPerCharge) { RebuildSegments(u.killsPerCharge); _lastProgress = u.KillProgress; _lastCharges = u.Charges; }

            // события: убийство выбило сегмент / набрался заряд
            if (u.KillProgress > _lastProgress && _lastProgress >= 0)
            {
                int i = Mathf.Clamp(u.KillProgress - 1, 0, _segPunch.Count - 1);
                _segPunch[i] = 1f;
                _shake = 1f;
            }
            if (u.Charges > _lastCharges && _lastCharges >= 0)
            {
                _burstT = 0f;
                _pop = 1f;
                _flash = 1f;
                _shake = 1.4f;
            }
            _lastProgress = u.KillProgress;
            _lastCharges = u.Charges;

            bool full = u.Charges >= u.settings.maxCharges;
            bool ready = u.CanActivate;
            bool depleted = u.UsesLeft <= 0;
            float t = Time.unscaledTime;

            // сегменты
            for (int i = 0; i < _segments.Count; i++)
            {
                _segPunch[i] = Mathf.MoveTowards(_segPunch[i], 0f, dt * 3.5f);
                bool on = full || i < u.KillProgress;
                var c = on ? Filled : Empty;
                c = Color.Lerp(c, Color.white, Mathf.Max(_segPunch[i], _flash * 0.8f));
                _segments[i].color = c;
                _segments[i].rectTransform.localScale = Vector3.one * (1f + 0.16f * _segPunch[i]);
            }
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 2.5f);

            // орех: серый, пока не готов; золотой блик, когда готов; при прицеливании — вжат и крутится
            _iconImg.color = ready ? Color.white : new Color(0.55f, 0.55f, 0.6f, 0.75f);
            float aimSquash = u.IsAiming ? 0.88f : 1f;
            _pop = Mathf.MoveTowards(_pop, 0f, dt * 3f);
            float breathe = ready && !u.IsAiming ? 1f + 0.04f * Mathf.Sin(t * 5f) : 1f;
            _icon.localScale = Vector3.one * (breathe * aimSquash + 0.35f * _pop * _pop);
            _icon.localRotation = Quaternion.Euler(0, 0, u.IsAiming ? t * 240f : 8f * Mathf.Sin(t * 2f) * (ready ? 1f : 0f));

            _glow.color = Prims.WithAlpha(Filled, ready ? 0.35f + 0.2f * Mathf.Sin(t * 4f) : 0f);

            // волна при наборе заряда
            _burstT = Mathf.Min(1f, _burstT + dt * 1.8f);
            _burst.localScale = Vector3.one * Mathf.Lerp(0.9f, 2.1f, _burstT);
            _burstImg.color = Prims.WithAlpha(Filled, (1f - _burstT) * 0.9f);

            _counter.gameObject.SetActive(u.Charges > 0);
            _counterText.text = u.Charges.ToString();
            _counter.localScale = Vector3.one * (1f + 0.4f * _pop);

            _keyHint.gameObject.SetActive(ready);
            _keyHint.text = u.IsAiming ? "отпусти — сбросить" : "ПКМ / Q";
            _keyHint.color = Prims.WithAlpha(UiKit.TextColor, 0.6f + 0.4f * Mathf.Sin(t * 6f));

            // «выбивается»: плашка вздрагивает от каждого убийства
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 5f);
            Vector2 jitter = new Vector2(Mathf.Sin(t * 90f), Mathf.Cos(t * 77f)) * (8f * _shake * _shake);
            _root.anchoredPosition = _basePos + jitter;
            _root.localScale = Vector3.one * (1f + 0.12f * _pop) * (u.IsAiming ? 0.92f : 1f);

            _group.alpha = Mathf.MoveTowards(_group.alpha, depleted ? 0.35f : 1f, dt * 4f);
        }
    }
}
