// Мини-набор для сборки uGUI из кода: палитра, шрифт, панели, тексты, кнопки, раскладки, анимации.
// Весь UI игры строится через эти функции, поэтому единый стиль меняется в одном месте.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CatTanks
{
    public static class UiKit
    {
        // ------------------------------------------------------------------ Палитра
        public static readonly Color Ink = new Color(0.07f, 0.08f, 0.12f, 0.94f);       // фон панелей
        public static readonly Color InkSoft = new Color(0.14f, 0.15f, 0.21f, 0.96f);   // карточки
        public static readonly Color Dim = new Color(0.02f, 0.02f, 0.05f, 0.6f);        // затемнение за окнами
        public static readonly Color TextColor = new Color(0.96f, 0.95f, 0.92f);
        public static readonly Color Muted = new Color(0.68f, 0.7f, 0.78f);
        public static readonly Color Accent = new Color(1f, 0.62f, 0.22f);              // рыжий кот
        public static readonly Color Cyan = new Color(0.4f, 0.88f, 1f);                 // ульта
        public static readonly Color Gold = new Color(1f, 0.8f, 0.25f);                 // рыбки
        public static readonly Color Danger = new Color(1f, 0.36f, 0.32f);
        public static readonly Color Good = new Color(0.45f, 0.9f, 0.5f);
        public static readonly Color ButtonBg = new Color(0.22f, 0.24f, 0.32f, 1f);

        static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        static Sprite _vignette;
        /// <summary>Виньетка: прозрачный центр, плотные края.</summary>
        public static Sprite Vignette
        {
            get
            {
                if (_vignette != null) return _vignette;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Vignette" };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, r));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                return _vignette = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        // ------------------------------------------------------------------ Основа

        public static Canvas MakeCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Привязка к точке родителя: anchor (0..1), pivot, смещение и размер в пикселях референса.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Растянуть на всего родителя с отступами.</summary>
        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Rt(this Component c) => (RectTransform)c.transform;

        // ------------------------------------------------------------------ Элементы

        public static Image Img(Transform parent, Sprite sprite, Color color, string name = "Image")
        {
            var img = Node(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite == Prims.RoundedRect) img.type = UnityEngine.UI.Image.Type.Sliced;
            else img.preserveAspect = true;
            return img;
        }

        /// <summary>Скруглённая панель. radius — множитель скругления (меньше = мягче углы не меняются, больше = острее).</summary>
        public static Image Panel(Transform parent, Color color, string name = "Panel", float radius = 1f)
        {
            var img = Img(parent, Prims.RoundedRect, color, name);
            img.pixelsPerUnitMultiplier = radius;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal, bool shadow = true)
        {
            var t = Node("Text", parent).gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            if (shadow)
            {
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
                sh.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        public static Button Btn(Transform parent, string text, Action onClick, Color? bg = null, int fontSize = 30, float height = 72f)
        {
            var img = Panel(parent, bg ?? ButtonBg, "Button");
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.55f);
            colors.colorMultiplier = 1.2f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() =>
            {
                Sfx.Play(SfxId.UiClick, 0.8f, 0.02f);
                onClick?.Invoke();
            });

            var label = Label(img.transform, text, fontSize, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.Rt().Stretch(16, 16, 4, 4);
            // на яркой кнопке (рыжая, золотая) — тёмный текст без тени, иначе не читается
            var c = bg ?? ButtonBg;
            if (0.299f * c.r + 0.587f * c.g + 0.114f * c.b > 0.6f)
            {
                label.color = new Color(0.22f, 0.12f, 0.02f);
                label.GetComponent<Shadow>().enabled = false;
            }
            img.gameObject.AddComponent<UiButtonFx>();
            Size(img, -1, height);
            return btn;
        }

        public static Text LabelOf(Button b) => b.GetComponentInChildren<Text>();

        // ------------------------------------------------------------------ Раскладка

        public static VerticalLayoutGroup VStack(Component c, float spacing, int padding = 0, TextAnchor align = TextAnchor.UpperCenter)
        {
            var g = c.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = new RectOffset(padding, padding, padding, padding);
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return g;
        }

        public static HorizontalLayoutGroup HStack(Component c, float spacing, int padding = 0, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var g = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = new RectOffset(padding, padding, padding, padding);
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            return g;
        }

        public static LayoutElement Size(Component c, float width = -1, float height = -1, float flexWidth = -1)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) { le.preferredWidth = width; le.minWidth = width; }
            if (height >= 0) { le.preferredHeight = height; le.minHeight = height; }
            if (flexWidth >= 0) le.flexibleWidth = flexWidth;
            return le;
        }

        public static RectTransform Spacer(Transform parent, float height)
        {
            var rt = Node("Spacer", parent);
            Size(rt, -1, height);
            return rt;
        }

        /// <summary>Подпись клавиши: полупрозрачная, поэтому читается и на тёмной, и на яркой кнопке.</summary>
        public static string Hotkey(string key) => $"<color=#80808099><size=22>[{key}]</size></color>";
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>Кнопка чуть растёт при наведении и сжимается при нажатии + тихий звук наведения.</summary>
    public class UiButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        float _target = 1f, _scale = 1f;
        Selectable _sel;

        void Awake() { _sel = GetComponent<Selectable>(); }
        void OnDisable() { _target = _scale = 1f; transform.localScale = Vector3.one; }

        public void OnPointerEnter(PointerEventData e)
        {
            if (_sel != null && !_sel.interactable) return;
            _target = 1.04f;
            Sfx.Play(SfxId.UiHover, 0.6f, 0.03f);
        }

        public void OnPointerExit(PointerEventData e) { _target = 1f; }
        public void OnPointerDown(PointerEventData e) { if (_sel == null || _sel.interactable) _target = 0.96f; }
        public void OnPointerUp(PointerEventData e) { _target = 1.04f; }

        void Update()
        {
            _scale = Mathf.Lerp(_scale, _target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            transform.localScale = Vector3.one * _scale;
        }
    }

    /// <summary>Плавное появление/скрытие экрана: прозрачность + лёгкий «поп» масштаба. Работает на паузе.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UiFade : MonoBehaviour
    {
        public RectTransform popTarget;
        public float speed = 10f;
        CanvasGroup _cg;
        bool _visible;

        public bool Visible => _visible;

        void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            _cg.alpha = 0f;
            _cg.blocksRaycasts = false;
            _cg.interactable = false;
        }

        public void Show(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            _cg.blocksRaycasts = visible;
            _cg.interactable = visible;
            if (visible && popTarget != null) popTarget.localScale = Vector3.one * 0.92f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _cg.alpha = Mathf.MoveTowards(_cg.alpha, _visible ? 1f : 0f, dt * speed);
            if (popTarget != null)
                popTarget.localScale = Vector3.Lerp(popTarget.localScale, Vector3.one, 1f - Mathf.Exp(-14f * dt));
        }
    }
}
