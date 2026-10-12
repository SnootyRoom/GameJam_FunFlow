// Кнопка в пиксельном стиле: чёрная плашка + уголки-скобки из спрайта художника.
// Наведение — рядом появляется стрелка-курсор, кнопка сдвигается вправо, текст красится в акцент.
// Нажатие — кнопка «вдавливается» на пару пикселей. Всё мгновенно, без плавности — как в старых играх.
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class PixelButton : Button
    {
        public Text label;
        public RectTransform content;       // всё, что двигается (плашка, уголки, текст)
        public GameObject cursor;           // стрелка слева, видна при наведении
        public Color labelNormal = Color.white;
        public Color labelHover = new Color(1f, 0.65f, 0.25f);
        public Vector2 hoverShift = new Vector2(8, 0);
        public Vector2 pressShift = new Vector2(0, -4);

        /// <summary>Внешний вид кнопки. Пустые спрайты = простая плашка цвета plateColor.</summary>
        public struct Style
        {
            public Sprite plate, cornerTopLeft, cornerBottomRight;
            public Color plateColor, labelColor, hoverColor;
            public Font font;
            public int fontSize;
        }

        /// <summary>
        /// Размер кнопки = размер исходной картинки (360×90). Плашка лежит со смещением 5 px внутри,
        /// уголки — в левом верхнем и правом нижнем углах, как нарисовал художник.
        /// </summary>
        public static PixelButton Create(Transform parent, string name, Vector2 anchor, Vector2 pos, string text,
                                         Style st, UnityAction onClick)
        {
            var rt = UIFactory.Rect(parent, name, anchor, pos, new Vector2(360, 90));
            rt.pivot = new Vector2(0.5f, 0.5f);

            // невидимая область клика на всю кнопку (не двигается, чтобы курсор не «соскальзывал»)
            var hit = UIFactory.Image(rt, Prims.Square, new Color(0, 0, 0, 0), true);

            var content = UIFactory.Stretch(rt, "Content");
            var plate = UIFactory.Stretch(content, "Plate");
            bool art = st.plate != null;
            if (art) { plate.offsetMin = new Vector2(5, 5); plate.offsetMax = new Vector2(-5, -5); }
            UIFactory.Image(plate, art ? st.plate : Prims.Square, art ? Color.white : st.plateColor);

            if (st.cornerTopLeft != null) Corner(content, "CornerTL", st.cornerTopLeft, new Vector2(0, 1));
            if (st.cornerBottomRight != null) Corner(content, "CornerBR", st.cornerBottomRight, new Vector2(1, 0));

            // текст — последним, поверх плашки и уголков; по размеру совпадает с плашкой
            var labelRect = UIFactory.Stretch(content, "Label");
            labelRect.offsetMin = plate.offsetMin;
            labelRect.offsetMax = plate.offsetMax;
            var label = UIFactory.Text(labelRect, st.font, text, st.fontSize, TextAnchor.MiddleCenter, st.labelColor);
            UIFactory.HardShadow(label, 4, new Color(0, 0, 0, 0.9f));

            var cursor = UIFactory.Rect(rt, "Cursor", new Vector2(0, 0.5f), new Vector2(-16, 0), new Vector2(24, 36));
            cursor.pivot = new Vector2(1, 0.5f);
            UIFactory.Image(cursor, UIFactory.PixelArrowSprite(), st.hoverColor);
            cursor.gameObject.SetActive(false);

            var b = rt.gameObject.AddComponent<PixelButton>();
            b.transition = Transition.None;   // вид меняем сами в DoStateTransition
            b.targetGraphic = hit;
            b.label = label;
            b.content = content;
            b.cursor = cursor.gameObject;
            b.labelNormal = st.labelColor;
            b.labelHover = st.hoverColor;
            b.onClick.AddListener(onClick);
            return b;
        }

        static void Corner(RectTransform parent, string name, Sprite sprite, Vector2 corner)
        {
            var rt = UIFactory.Rect(parent, name, corner, Vector2.zero, sprite.rect.size); // 1 пиксель картинки = 1 пиксель UI
            UIFactory.Image(rt, sprite, Color.white);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Apply(false, false); // меню спрятали с наведённой кнопкой — при следующем показе она обычная
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            Apply(state == SelectionState.Highlighted || state == SelectionState.Pressed, state == SelectionState.Pressed);
        }

        void Apply(bool hot, bool down)
        {
            if (label != null) label.color = hot ? labelHover : labelNormal;
            if (cursor != null) cursor.SetActive(hot);
            if (content != null) content.anchoredPosition = (hot ? hoverShift : Vector2.zero) + (down ? pressShift : Vector2.zero);
        }
    }
}
