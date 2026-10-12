// Кнопка в пиксельном стиле. Два вида:
//  • CreateImage — картинка художника, где текст уже нарисован (button_start и т.п.).
//  • CreatePaper — бумажная плашка с текстом пиксельным шрифтом, для пунктов без готовой картинки.
// Анимация пружиной (на unscaled time — работает и на паузе):
//  наведение — кнопка приподнимается и чуть растёт; нажатие — сжимается и проседает;
//  клик — «отскок» с перелётом, а само действие срабатывает через clickDelay, чтобы отскок успели увидеть.
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class PixelButton : Button
    {
        public Text label;
        public RectTransform content;       // всё, что двигается (плашка и текст)
        public Color labelNormal = Color.white;
        public Color labelHover = new Color(1f, 0.65f, 0.25f);

        [Header("Анимация")]
        public float hoverScale = 1.05f;
        public float pressScale = 0.92f;
        public float clickPunch = 0.12f;            // насколько «выстреливает» при отпускании
        public Vector2 hoverShift = new Vector2(0, 6);
        public Vector2 pressShift = new Vector2(0, -6);
        public float stiffness = 600f;              // жёсткость пружины
        public float damping = 22f;                 // затухание (меньше — дольше качается)
        [Tooltip("Через сколько секунд после клика выполнить действие")]
        public float clickDelay = 0.15f;

        public readonly ButtonClickedEvent onActivated = new ButtonClickedEvent(); // действие кнопки (после задержки)

        // для звука и аналитики: любая пиксельная кнопка навелась / нажалась
        public static event System.Action Hovered;
        public static event System.Action Clicked;
        /// <summary>Кнопку нажали (мышь вниз или Enter) — для звука: щелчок должен быть сразу, а не на отпускании.</summary>
        public static event System.Action Pressed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Hovered = null; Clicked = null; Pressed = null; }

        bool _hot, _down;
        float _scale = 1f, _scaleVel;
        Vector2 _shift, _shiftVel;
        bool _busy;

        /// <summary>
        /// Кнопка-картинка: художник уже нарисовал и плашку, и текст. Размер = размер картинки × scale
        /// (1 px картинки = scale px UI при 1920×1080). Для пиксель-арта бери scale, при котором пиксель картинки
        /// остаётся целым (картинка из блоков 8 px → 1.5 даёт ровные блоки по 12 px).
        /// </summary>
        public static PixelButton CreateImage(Transform parent, string name, Vector2 anchor, Vector2 pos, Sprite art,
                                              float scale, UnityAction onClick)
        {
            var b = Shell(parent, name, anchor, pos, art.rect.size * scale);
            UIFactory.Image(b.content, art, Color.white);
            if (onClick != null) b.onActivated.AddListener(onClick);
            return b;
        }

        /// <summary>
        /// Кнопка на «бумажной» плашке (UIFactory.PaperSprite) — для пунктов, у которых нет готовой картинки.
        /// По размеру и виду совпадает с картинками pause_*: block — экранных пикселей на пиксель плашки.
        /// </summary>
        public static PixelButton CreatePaper(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
                                              Sprite paper, float block, string text, Font font, int fontSize,
                                              Color ink, Color hover)
        {
            var b = Shell(parent, name, anchor, pos, size);
            UIFactory.PixelPanel(b.content, paper, Color.white, block).raycastTarget = false;

            // текст — по центру карточки, без полосы падающей тени справа-снизу
            var labelRect = UIFactory.Stretch(b.content, "Label");
            labelRect.offsetMin = new Vector2(0, block);
            labelRect.offsetMax = new Vector2(-block, 0);
            var label = UIFactory.Text(labelRect, font, text, fontSize, TextAnchor.MiddleCenter, ink);
            Object.DestroyImmediate(label.GetComponent<Shadow>()); // на светлой бумаге тень не нужна

            b.label = label;
            b.labelNormal = ink;
            b.labelHover = hover;
            return b;
        }

        static PixelButton Shell(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = UIFactory.Rect(parent, name, anchor, pos, size);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // невидимая область клика на всю кнопку (не анимируется, чтобы курсор не «соскальзывал» с края)
            var hit = UIFactory.Image(rt, null, new Color(0, 0, 0, 0), true);

            var content = UIFactory.Stretch(rt, "Content");
            content.pivot = new Vector2(0.5f, 0.5f); // масштаб — от центра кнопки

            var b = rt.gameObject.AddComponent<PixelButton>();
            b.transition = Transition.None;   // вид меняем сами
            b.targetGraphic = hit;
            b.content = content;
            return b;
        }

        // ------------------------------------------------------------------ Состояния

        protected override void Awake()
        {
            base.Awake();
            // подписка кодом в префаб не сохраняется — восстанавливаем при каждом создании кнопки
            onClick.AddListener(HandleClick);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // меню спрятали — при следующем показе кнопка в покое, без хвоста анимации
            _hot = _down = _busy = false;
            _scale = 1f; _scaleVel = 0f;
            _shift = _shiftVel = Vector2.zero;
            ApplyLook();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            bool wasHot = _hot;
            _hot = state == SelectionState.Highlighted || state == SelectionState.Pressed;
            if (_hot && !wasHot && Application.isPlaying) Hovered?.Invoke();
            _down = state == SelectionState.Pressed;
            if (label != null) label.color = _hot ? labelHover : labelNormal;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button == PointerEventData.InputButton.Left && IsInteractable() && !_busy) Pressed?.Invoke();
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (IsInteractable() && !_busy) Pressed?.Invoke(); // с клавиатуры «нажатие» и «клик» — одно событие
            base.OnSubmit(eventData);
        }

        void HandleClick()
        {
            if (_busy) return;          // защита от двойного клика, пока ждём действие
            Clicked?.Invoke();
            _scaleVel += clickPunch * 60f; // толчок пружины — кнопка «выстреливает» и успокаивается
            if (clickDelay <= 0f || !isActiveAndEnabled) { onActivated.Invoke(); return; }
            StartCoroutine(InvokeLater());
        }

        IEnumerator InvokeLater()
        {
            _busy = true;
            yield return new WaitForSecondsRealtime(clickDelay);
            _busy = false;
            onActivated.Invoke();
        }

        // ------------------------------------------------------------------ Пружина

        void Update()
        {
            float targetScale = _down ? pressScale : _hot ? hoverScale : 1f;
            Vector2 targetShift = _down ? pressShift : _hot ? hoverShift : Vector2.zero;

            // несколько мелких шагов — пружина устойчива даже при просадке FPS
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            int steps = Mathf.CeilToInt(dt / 0.004f);
            float h = dt / Mathf.Max(1, steps);
            for (int i = 0; i < steps; i++)
            {
                _scaleVel += ((targetScale - _scale) * stiffness - _scaleVel * damping) * h;
                _scale += _scaleVel * h;
                _shiftVel += ((targetShift - _shift) * stiffness - _shiftVel * damping) * h;
                _shift += _shiftVel * h;
            }
            ApplyLook();
        }

        void ApplyLook()
        {
            if (content == null) return;
            content.localScale = new Vector3(_scale, _scale, 1f);
            // сдвиг округляем до целых пикселей — пиксель-арт не «плывёт» дробно
            content.anchoredPosition = new Vector2(Mathf.Round(_shift.x), Mathf.Round(_shift.y));
        }
    }
}
