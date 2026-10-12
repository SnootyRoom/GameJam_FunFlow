// Помощники для сборки uGUI из кода: панели, текст, картинки, кнопки.
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CatTanks.UI
{
    public static class UIFactory
    {
        public static Font DefaultFont()
        {
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        /// <summary>Без EventSystem кнопки не нажимаются. Создаём, если в сцене его нет.</summary>
        public static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
#else
            if (Object.FindObjectOfType<EventSystem>() != null) return;
#endif
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); // всё рисуем под 1920×1080
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Пустой RectTransform. anchor — точка привязки (0,0 — левый низ, 1,1 — правый верх).</summary>
        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Растягивается на весь родитель.</summary>
        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Text Text(RectTransform rt, Font font, string text, int size, TextAnchor align, Color color)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rt.gameObject.AddComponent<Shadow>(); // читаемость на любом фоне
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(2, -2);
            return t;
        }

        /// <summary>Жёсткая тень без размытия — для пиксельного текста.</summary>
        public static void HardShadow(Text t, float px, Color color)
        {
            var s = t.GetComponent<Shadow>();
            if (s == null) s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = color;
            s.effectDistance = new Vector2(px, -px);
        }

        // ------------------------------------------------------------------ Пиксельные спрайты

        static Sprite _pixelPanel, _pixelArrow;

        /// <summary>
        /// Пиксельная панель 12×12 для 9-slice: ступенчатые углы, рамка 1 пиксель непрозрачная, заливка полупрозрачная.
        /// Цвет и общая прозрачность задаются цветом Image.
        /// </summary>
        public static Sprite PixelPanelSprite()
        {
            if (_pixelPanel != null) return _pixelPanel;
            const int n = 12;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int dx = Mathf.Min(x, n - 1 - x), dy = Mathf.Min(y, n - 1 - y);
                    byte a = dx + dy < 2 ? (byte)0                                   // срезанный угол
                           : dx == 0 || dy == 0 || dx + dy == 2 ? (byte)255           // рамка
                           : (byte)150;                                                // заливка
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            _pixelPanel = MakePixelSprite("PixelPanel", n, n, px, new Vector4(4, 4, 4, 4));
            return _pixelPanel;
        }

        /// <summary>Пиксельная стрелка-курсор 6×9, смотрит вправо.</summary>
        public static Sprite PixelArrowSprite()
        {
            if (_pixelArrow != null) return _pixelArrow;
            const int w = 6, h = 9;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(x <= 5 - Mathf.Abs(y - 4) ? 255 : 0));
            _pixelArrow = MakePixelSprite("PixelArrow", w, h, px, Vector4.zero);
            return _pixelArrow;
        }

        /// <summary>
        /// «Бумажная» плашка 10×10 для 9-slice в стиле кнопок паузы (pause_restart): тёмный контур со скруглёнными
        /// углами, светлая полоса сверху и слева, тень-полоса снизу и справа, падающая тень со сдвигом на 1 пиксель.
        /// Один пиксель спрайта = один «блок» картинки кнопки (8 px исходника). Используется для подложки паузы
        /// и для кнопок без готовой картинки.
        /// </summary>
        public static Sprite PaperSprite(Color32 ink, Color32 light, Color32 paper, Color32 shade, Color32 shadow)
        {
            const int n = 10, cw = n - 1; // карточка 9×9 + тень на 1 пиксель вправо-вниз
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var c = Card(x, y - 1);                     // y=0 — низ текстуры, карточка сдвинута на 1 вверх
                    if (c.a == 0 && Card(x - 1, y).a > 0) c = shadow; // под карточкой, сдвинутой вправо-вниз
                    px[y * n + x] = c;
                }
            return MakePixelSprite("Paper", n, n, px, new Vector4(4, 4, 4, 4));

            Color32 Card(int cx, int cy)
            {
                if (cx < 0 || cy < 0 || cx >= cw || cy >= cw) return new Color32(0, 0, 0, 0);
                int dl = cx, dr = cw - 1 - cx, db = cy, dt = cw - 1 - cy;
                int dx = Mathf.Min(dl, dr), dy = Mathf.Min(db, dt);
                if (dx == 0 && dy == 0) return new Color32(0, 0, 0, 0);       // срезанный угол
                if (dx == 0 || dy == 0 || (dx == 1 && dy == 1)) return ink;   // контур со ступенькой в углу
                if (dr == 1 || db == 1) return shade;
                if (dl == 1 || dt == 1) return light;
                return paper;
            }
        }

        static Sprite _pixelStar;

        /// <summary>Пиксельная звезда 13×13 с тёмным контуром: заливка белая (красится цветом Image), контур остаётся тёмным.</summary>
        public static Sprite PixelStarSprite()
        {
            if (_pixelStar != null) return _pixelStar;
            string[] rows =
            {
                "......#......",
                ".....#o#.....",
                ".....#o#.....",
                "....#ooo#....",
                "#####ooo#####",
                "#ooooooooooo#",
                ".#ooooooooo#.",
                "..#ooooooo#..",
                "..#ooooooo#..",
                ".#ooo###ooo#.",
                ".#oo#...#oo#.",
                "#oo#.....#oo#",
                "###.......###",
            };
            int n = rows.Length;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    char c = rows[n - 1 - y][x]; // строки сверху вниз, текстура снизу вверх
                    px[y * n + x] = c == 'o' ? new Color32(255, 255, 255, 255)
                                  : c == '#' ? new Color32(42, 34, 51, 255)   // контур — цвет «чернил» UI
                                  : new Color32(0, 0, 0, 0);
                }
            _pixelStar = MakePixelSprite("PixelStar", n, n, px, Vector4.zero);
            return _pixelStar;
        }

        static Sprite MakePixelSprite(string name, int w, int h, Color32[] px, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, // никакого сглаживания — чёткие пиксели при любом масштабе
                wrapMode = TextureWrapMode.Clamp,
                name = name,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
            s.hideFlags = HideFlags.DontSave;
            return s;
        }

        /// <summary>Пиксельная подложка. pixelScale — сколько экранных пикселей (при 1920×1080) на пиксель рамки.</summary>
        public static Image PixelPanel(RectTransform rt, Sprite sprite, Color color, float pixelScale = 4f)
        {
            var img = Image(rt, sprite != null ? sprite : PixelPanelSprite(), color, true);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / pixelScale;
            return img;
        }

        public static Button Button(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size,
                                    Sprite sprite, Color color, Font font, string label, int fontSize,
                                    Color textColor, UnityAction onClick)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = Image(rt, sprite, color, true);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            Text(Stretch(rt, "Label"), font, label, fontSize, TextAnchor.MiddleCenter, textColor);
            return btn;
        }
    }
}
