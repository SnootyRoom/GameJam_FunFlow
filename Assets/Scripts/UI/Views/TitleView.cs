// Стартовый экран «TANK CAT / OPERATION:FISH / PRESS ANY KEY». Лежит в префабе MainMenu поверх общего фона.
// Всё на unscaled time: заголовок выезжает сверху, подзаголовок пульсирует (как надпись в меню Minecraft),
// «PRESS ANY KEY» мерцает и покачивается. Любая клавиша или кнопка мыши — заголовок улетает, и GameUI показывает меню.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class TitleView : MonoBehaviour
    {
        public RectTransform title;
        public RectTransform subtitle;
        public RectTransform prompt;
        public CanvasGroup group;       // общая прозрачность при уходе
        public Image flash;             // короткая белая вспышка при нажатии

        [Header("Анимация")]
        public float introTime = 0.6f;          // заголовок выезжает сверху
        public float introDrop = 320f;
        public float titleBob = 6f;             // покачивание заголовка, px
        public float subtitlePulse = 0.06f;     // насколько «дышит» подзаголовок
        public float subtitlePulseSpeed = 5f;
        [Tooltip("Наклон подзаголовка в градусах (в Minecraft ≈ -20)")]
        public float subtitleTilt = 0f;
        public Vector2 promptAlpha = new Vector2(0.35f, 1f);
        public float promptBlinkSpeed = 3f;
        public float leaveTime = 0.4f;
        [Tooltip("Сколько секунд после появления нажатия не считаются — чтобы случайно не проскочить экран")]
        public float inputDelay = 0.3f;

        /// <summary>Игрок нажал клавишу — для звука перехода.</summary>
        public static event Action Dismissed;
        /// <summary>Заголовок улетел — пора показывать меню.</summary>
        public event Action Finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Dismissed = null; }

        Vector2 _titlePos, _subtitlePos, _promptPos;
        Graphic[] _promptGraphics;
        float _shownAt;
        bool _leaving, _cached;

        void Cache()
        {
            if (_cached) return;
            _cached = true;
            _titlePos = title.anchoredPosition;
            _subtitlePos = subtitle.anchoredPosition;
            _promptPos = prompt.anchoredPosition;
            _promptGraphics = prompt.GetComponentsInChildren<Graphic>();
        }

        public void Show()
        {
            Cache();
            gameObject.SetActive(true);
            _leaving = false;
            _shownAt = Time.unscaledTime;
            if (group != null) group.alpha = 1f;
            if (flash != null) flash.color = new Color(1, 1, 1, 0);
            Animate(0f);
        }

        void Update()
        {
            if (_leaving) return;
            float t = Time.unscaledTime - _shownAt;
            Animate(t);
            if (t > inputDelay && GameInput.AnyKeyPressed()) StartCoroutine(Leave());
        }

        void Animate(float t)
        {
            // заголовок: падает сверху с перелётом, потом еле заметно покачивается
            float k = Mathf.Clamp01(t / introTime);
            float drop = (1f - EaseOutBack(k)) * introDrop;
            float bob = Mathf.Round(Mathf.Sin(t * 1.6f) * titleBob);
            title.anchoredPosition = _titlePos + new Vector2(0, drop + bob);

            // подзаголовок: выскакивает чуть позже и пульсирует
            float pop = EaseOutBack(Mathf.Clamp01((t - introTime * 0.6f) / 0.35f));
            float pulse = 1f + subtitlePulse * Mathf.Abs(Mathf.Sin(t * subtitlePulseSpeed));
            subtitle.localScale = Vector3.one * (pop * pulse);
            subtitle.localRotation = Quaternion.Euler(0, 0, subtitleTilt);
            subtitle.anchoredPosition = _subtitlePos + new Vector2(0, drop);

            // подсказка: проявляется после заголовка, мерцает и чуть покачивается
            float appear = Mathf.Clamp01((t - introTime) / 0.3f);
            float blink = Mathf.Lerp(promptAlpha.x, promptAlpha.y, 0.5f + 0.5f * Mathf.Cos(t * promptBlinkSpeed));
            SetPromptAlpha(appear * blink);
            prompt.anchoredPosition = _promptPos + new Vector2(0, Mathf.Round(Mathf.Sin(t * 2.4f) * 4f));
        }

        IEnumerator Leave()
        {
            _leaving = true;
            Dismissed?.Invoke();
            Vector2 titleFrom = title.anchoredPosition, subtitleFrom = subtitle.anchoredPosition;
            for (float e = 0f; e < leaveTime; e += Time.unscaledDeltaTime)
            {
                float k = e / leaveTime;
                float up = EaseInBack(k) * 700f; // замах вниз и улёт вверх
                title.anchoredPosition = titleFrom + new Vector2(0, up);
                subtitle.anchoredPosition = subtitleFrom + new Vector2(0, up * 1.15f);
                SetPromptAlpha(1f - Mathf.Clamp01(k * 3f));
                if (flash != null) flash.color = new Color(1, 1, 1, 0.35f * (1f - k));
                if (group != null) group.alpha = 1f - k * k;
                yield return null;
            }
            gameObject.SetActive(false);
            title.anchoredPosition = _titlePos;
            subtitle.anchoredPosition = _subtitlePos;
            Finished?.Invoke();
        }

        void SetPromptAlpha(float a)
        {
            foreach (var g in _promptGraphics)
            {
                var c = g.color;
                c.a = a;
                g.color = c;
            }
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3) + c1 * Mathf.Pow(x - 1f, 2);
        }

        static float EaseInBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return c3 * x * x * x - c1 * x * x;
        }
    }
}
