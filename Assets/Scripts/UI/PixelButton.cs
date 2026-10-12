// Кнопка в пиксельном стиле. Два вида:
//  • Create      — текстовая: чёрная плашка + уголки-скобки из спрайта художника, текст пиксельным шрифтом;
//  • CreateImage — картинка художника, где текст уже нарисован (button_start и т.п.).
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
        public RectTransform content;       // всё, что двигается (плашка, уголки, текст)
        public GameObject cursor;           // стрелка слева при наведении (только у текстовых кнопок)
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

        bool _hot, _down;
        float _scale = 1f, _scaleVel;
        Vector2 _shift, _shiftVel;
        bool _busy;

        /// <summary>Внешний вид текстовой кнопки. Пустые спрайты = простая плашка цвета plateColor.</summary>
        public struct Style
        {
            public Sprite plate, cornerTopLeft, cornerBottomRight;
            public Color plateColor, labelColor, hoverColor;
            public Font font;
            public Sprite cursor;          // стрелка при наведении; пусто = сгенерированная
            public int fontSize;
        }

        /// <summary>
        /// Текстовая кнопка 360×90 (размер исходной картинки start_button). Плашка лежит со смещением 5 px внутри,
        /// уголки — в левом верхнем и правом нижнем углах, как нарисовал художник.
        /// </summary>
        public static PixelButton Create(Transform parent, string name, Vector2 anchor, Vector2 pos, string text,
                                         Style st, UnityAction onClick)
        {
            var b = Shell(parent, name, anchor, pos, new Vector2(360, 90));
            var content = b.content;

            var plate = UIFactory.Stretch(content, "Plate");
            bool art = st.plate != null;
            if (art) { plate.offsetMin = new Vector2(5, 5); plate.offsetMax = new Vector2(-5, -5); }
            UIFactory.Image(plate, art ? st.plate : null, art ? Color.white : st.plateColor);

            if (st.cornerTopLeft != null) Corner(content, "CornerTL", st.cornerTopLeft, new Vector2(0, 1));
            if (st.cornerBottomRight != null) Corner(content, "CornerBR", st.cornerBottomRight, new Vector2(1, 0));

            // текст — последним, поверх плашки и уголков; по размеру совпадает с плашкой
            var labelRect = UIFactory.Stretch(content, "Label");
            labelRect.offsetMin = plate.offsetMin;
            labelRect.offsetMax = plate.offsetMax;
            var label = UIFactory.Text(labelRect, st.font, text, st.fontSize, TextAnchor.MiddleCenter, st.labelColor);
            UIFactory.HardShadow(label, 4, new Color(0, 0, 0, 0.9f));

            var cursor = UIFactory.Rect(b.transform, "Cursor", new Vector2(0, 0.5f), new Vector2(-16, 0), new Vector2(24, 36));
            cursor.pivot = new Vector2(1, 0.5f);
            UIFactory.Image(cursor, st.cursor != null ? st.cursor : UIFactory.PixelArrowSprite(), st.hoverColor);
            cursor.gameObject.SetActive(false);

            b.label = label;
            b.cursor = cursor.gameObject;
            b.labelNormal = st.labelColor;
            b.labelHover = st.hoverColor;
            if (onClick != null) b.onActivated.AddListener(onClick);
            return b;
        }

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

        static void Corner(RectTransform parent, string name, Sprite sprite, Vector2 corner)
        {
            var rt = UIFactory.Rect(parent, name, corner, Vector2.zero, sprite.rect.size); // 1 пиксель картинки = 1 пиксель UI
            UIFactory.Image(rt, sprite, Color.white);
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
            _hot = state == SelectionState.Highlighted || state == SelectionState.Pressed;
            _down = state == SelectionState.Pressed;
            if (label != null) label.color = _hot ? labelHover : labelNormal;
            if (cursor != null) cursor.SetActive(_hot);
        }

        void HandleClick()
        {
            if (_busy) return;          // защита от двойного клика, пока ждём действие
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
