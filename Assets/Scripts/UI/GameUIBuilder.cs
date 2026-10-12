// Собирает весь UI по теме: холст + экраны (HUD, баннер, пауза, главное меню) с заполненными View-компонентами.
// Используется редакторской командой CatTanks → UI → Собрать префабы UI (результат сохраняется в префабы)
// и как запасной вариант в игре, если префаба нет. Вёрстку дальше удобнее править в префабах.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class GameUIBuilder
    {
        // Колонка главного меню: центр кнопок по X от левого края и положения по Y от центра экрана (экран 1920×1080)
        const float MenuColumnX = 735f, MenuTitleY = 290f, MenuFirstButtonY = 40f, MenuButtonStep = 200f;
        const float MenuButtonScale = 1.5f; // картинки кнопок из блоков по 8 px → блоки по 12 px, пиксели ровные

        readonly UiTheme t;
        readonly Font _font;

        GameUIBuilder(UiTheme theme)
        {
            t = theme;
            _font = theme.font != null ? theme.font : UIFactory.DefaultFont();
        }

        /// <summary>Создаёт холст GameCanvas под parent. Экраны — его дети в порядке отрисовки.</summary>
        public static GameUIView Build(Transform parent, UiTheme theme)
        {
            var b = new GameUIBuilder(theme);
            var canvas = UIFactory.CreateCanvas(parent, "GameCanvas", 100);
            var view = canvas.gameObject.AddComponent<GameUIView>();
            view.hud = b.BuildHud(canvas.transform);
            view.banner = b.BuildBanner(canvas.transform);
            view.pause = b.BuildPause(canvas.transform);
            view.menu = b.BuildMenu(canvas.transform);
            return view;
        }

        static Sprite Or(Sprite sprite, Sprite fallback) => sprite != null ? sprite : fallback;

        // ------------------------------------------------------------------ HUD

        HudView BuildHud(Transform root)
        {
            var rt = UIFactory.Stretch(root, "Hud");
            var v = rt.gameObject.AddComponent<HudView>();

            v.hearts = UIFactory.Rect(rt, "Hearts", new Vector2(0, 1), new Vector2(32, -28), new Vector2(400, 56));
            var row = v.hearts.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 10;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var heart = UIFactory.Rect(v.hearts, "HeartTemplate", new Vector2(0, 0.5f), Vector2.zero, new Vector2(48, 48));
            v.heartTemplate = UIFactory.Image(heart, Or(t.heartFull, Or(t.circle, Prims.Circle)), Color.white);
            heart.gameObject.SetActive(false);
            v.heartFull = t.heartFull;
            v.heartEmpty = t.heartEmpty;
            v.heartColor = t.heartColor;

            v.level = UIFactory.Text(UIFactory.Rect(rt, "Level", new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(400, 56)),
                                     _font, "Level 1", 32, TextAnchor.MiddleCenter, t.textColor);
            v.enemies = UIFactory.Text(UIFactory.Rect(rt, "Enemies", new Vector2(1, 1), new Vector2(-32, -24), new Vector2(400, 56)),
                                       _font, "Enemies: 0", 32, TextAnchor.MiddleRight, t.textColor);
            v.toast = UIFactory.Text(UIFactory.Rect(rt, "Toast", new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(1200, 60)),
                                     _font, "", 24, TextAnchor.MiddleCenter, t.accent);
            v.toast.gameObject.SetActive(false);

            v.exitArrow = UIFactory.Rect(rt, "ExitArrow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            v.exitArrow.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(v.exitArrow, Or(t.exitArrow, Or(t.triangle, Prims.Triangle)),
                            t.exitArrow != null ? Color.white : new Color(1f, 0.85f, 0.2f));
            v.exitArrow.gameObject.SetActive(false);

            BuildUltWidget(rt, v);

            // прицел — последним, поверх остального HUD
            v.crosshair = UIFactory.Rect(rt, "Crosshair", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36, 36));
            v.crosshair.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(v.crosshair, Or(t.crosshair, Or(t.ring, Prims.Ring)), t.crosshair != null ? Color.white : t.accent);
            return v;
        }

        /// <summary>Ульта «Орех» в левом нижнем углу: иконка, клавиша, квадратики-заряды и полоска до следующего заряда.</summary>
        void BuildUltWidget(RectTransform hud, HudView v)
        {
            var root = UIFactory.Rect(hud, "Ultimate", Vector2.zero, new Vector2(32, 32), new Vector2(280, 96));
            v.ultRoot = root;

            var icon = UIFactory.Rect(root, "Icon", Vector2.zero, Vector2.zero, new Vector2(96, 96));
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(48, 48); // масштаб «дыхания» — от центра
            v.ultIcon = UIFactory.Image(icon, Or(t.ultIcon, Or(t.circle, Prims.Circle)), Color.white);
            v.ultIcon.preserveAspect = true;

            v.ultKey = UIFactory.Text(UIFactory.Rect(root, "Key", Vector2.zero, new Vector2(70, -2), new Vector2(40, 32)),
                                      _font, "Q", 24, TextAnchor.LowerRight, t.textColor);
            UIFactory.HardShadow(v.ultKey, 3, new Color(0, 0, 0, 0.9f));

            v.ultPipOn = t.accent;
            v.ultPips = new Image[3];
            for (int i = 0; i < v.ultPips.Length; i++)
            {
                var pip = UIFactory.Rect(root, "Charge" + i, Vector2.zero, new Vector2(112 + i * 30, 48), new Vector2(22, 22));
                v.ultPips[i] = UIFactory.Image(pip, null, v.ultPipOff);
                var outline = pip.gameObject.AddComponent<Outline>(); // пиксельная рамка
                outline.effectColor = new Color(0, 0, 0, 0.85f);
                outline.effectDistance = new Vector2(3, -3);
            }

            var bar = UIFactory.Rect(root, "Bar", Vector2.zero, new Vector2(112, 18), new Vector2(150, 14));
            UIFactory.Image(bar, null, new Color(0f, 0f, 0f, 0.55f));
            var fill = UIFactory.Stretch(bar, "Fill");
            fill.anchorMax = new Vector2(0f, 1f);
            UIFactory.Image(fill, null, t.accent);
            v.ultBarFill = fill;
            root.gameObject.SetActive(false); // покажет GameUI, если у танка есть ульта
        }

        // ------------------------------------------------------------------ Баннер

        BannerView BuildBanner(Transform root)
        {
            var rt = UIFactory.Stretch(root, "Banner");
            var v = rt.gameObject.AddComponent<BannerView>();
            var strip = UIFactory.Rect(rt, "Strip", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2200, 240));
            strip.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(strip, t.panelSprite, t.panelSprite != null ? Color.white : t.dimColor);
            v.title = UIFactory.Text(UIFactory.Rect(strip, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1600, 100)),
                                     _font, "", 64, TextAnchor.MiddleCenter, t.accent);
            v.title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            v.subtitle = UIFactory.Text(UIFactory.Rect(strip, "Subtitle", new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(1600, 60)),
                                        _font, "", 32, TextAnchor.MiddleCenter, t.textColor);
            v.subtitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rt.gameObject.SetActive(false);
            return v;
        }

        // ------------------------------------------------------------------ Пауза

        PauseView BuildPause(Transform root)
        {
            var rt = UIFactory.Stretch(root, "Pause");
            var v = rt.gameObject.AddComponent<PauseView>();
            UIFactory.Image(rt, null, t.dimColor, true); // затемнение, заодно блокирует клики по игре

            var panel = UIFactory.Rect(rt, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 600));
            panel.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.PixelPanel(panel, t.pixelPanel, t.menuPanelColor);
            PanelTitle(panel, "Paused", -100, 64);
            v.resume = TextButton(panel, -240, "Resume");
            v.restart = TextButton(panel, -360, "Restart");
            v.mainMenu = TextButton(panel, -480, "Main Menu");
            rt.gameObject.SetActive(false);
            return v;
        }

        // ------------------------------------------------------------------ Главное меню и настройки

        MainMenuView BuildMenu(Transform root)
        {
            var rt = UIFactory.Stretch(root, "MainMenu");
            var v = rt.gameObject.AddComponent<MainMenuView>();
            UIFactory.Image(rt, null, new Color(0.12f, 0.13f, 0.15f, 1f), true);

            if (t.menuBackground != null)
            {
                // как background-size: cover — на любом соотношении сторон без полос и растяжения
                var bg = UIFactory.Rect(rt, "Background", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                UIFactory.Image(bg, t.menuBackground, Color.white);
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = t.menuBackground.rect.width / t.menuBackground.rect.height;
            }

            // Раскладка по референсу: без подложки, крупный заголовок над колонкой кнопок слева,
            // кнопки чуть заходят на кота.
            var main = UIFactory.Stretch(rt, "Main");
            v.mainGroup = main.gameObject;

            if (t.logo != null)
            {
                var l = UIFactory.Rect(main, "Logo", new Vector2(0, 0.5f), new Vector2(MenuColumnX, MenuTitleY), new Vector2(820, 240));
                l.pivot = new Vector2(0.5f, 0.5f);
                UIFactory.Image(l, t.logo, Color.white).preserveAspect = true;
            }
            else MenuTitle(main, t.gameTitle);

            v.play = ColumnButton(main, 0, t.startButton, "Play");
            v.settings = ColumnButton(main, 1, t.settingsButton, "Settings");
            v.quit = ColumnButton(main, 2, t.exitButton, "Quit");

            // подсказка по управлению — одной строкой в левом нижнем углу
            var hint = UIFactory.Text(UIFactory.Rect(main, "Hint", new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(1800, 32)),
                                      _font, t.controlsHint, 16, TextAnchor.LowerCenter, Color.white);
            UIFactory.HardShadow(hint, 3, new Color(0, 0, 0, 0.8f));

            // Настройки — на месте главной колонки, в том же стиле. Новые пункты добавляй сюда (или прямо в префаб).
            var settings = UIFactory.Stretch(rt, "Settings");
            v.settingsGroup = settings.gameObject;
            MenuTitle(settings, "Settings");
            v.fullscreen = ColumnButton(settings, 0, null, v.fullscreenOn, 24);
            v.fullscreenLabel = v.fullscreen.label;
            v.sound = ColumnButton(settings, 1, null, v.soundOn, 24);
            v.soundLabel = v.sound.label;
            v.back = ColumnButton(settings, 2, null, "Back");
            settings.gameObject.SetActive(false);
            return v;
        }

        /// <summary>Кнопка колонки меню: index — номер сверху. Картинка художника или текстовая кнопка того же масштаба.</summary>
        PixelButton ColumnButton(RectTransform parent, int index, Sprite art, string label, int fontSize = 32)
        {
            var anchor = new Vector2(0, 0.5f);
            var pos = new Vector2(MenuColumnX, MenuFirstButtonY - index * MenuButtonStep);
            if (art != null) return PixelButton.CreateImage(parent, label, anchor, pos, art, MenuButtonScale, null);

            var b = TextButton(parent, 0, label, fontSize);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one * MenuButtonScale;
            return b;
        }

        /// <summary>Крупный белый заголовок над колонкой, как на референсе, но пиксельным шрифтом.</summary>
        void MenuTitle(RectTransform parent, string text)
        {
            var rt = UIFactory.Rect(parent, "Title", new Vector2(0, 0.5f), new Vector2(MenuColumnX, MenuTitleY), new Vector2(1100, 160));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var title = UIFactory.Text(rt, _font, text, 72, TextAnchor.MiddleCenter, Color.white);
            UIFactory.HardShadow(title, 6, new Color(0.1f, 0.12f, 0.2f, 0.75f));
            var outline = title.gameObject.AddComponent<Outline>(); // тонкий тёмный контур — читается и на небе, и на облаках
            outline.effectColor = new Color(0.1f, 0.12f, 0.2f, 0.5f);
            outline.effectDistance = new Vector2(3, -3);
        }

        void PanelTitle(RectTransform panel, string text, float y, int size)
        {
            var title = UIFactory.Text(UIFactory.Rect(panel, "Title", new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(560, 120)),
                                       _font, text, size, TextAnchor.MiddleCenter, t.accent);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.HardShadow(title, 6, Color.black); // жёсткая пиксельная тень
        }

        PixelButton TextButton(RectTransform panel, float y, string label, int fontSize = 32)
        {
            return PixelButton.Create(panel, label, new Vector2(0.5f, 1), new Vector2(0, y), label, new PixelButton.Style
            {
                plate = t.buttonSprite,
                cornerTopLeft = t.buttonCornerTopLeft,
                cornerBottomRight = t.buttonCornerBottomRight,
                plateColor = t.buttonColor,
                labelColor = t.textColor,
                hoverColor = t.accent,
                font = _font,
                fontSize = fontSize,
                cursor = t.pixelCursor,
            }, null);
        }
    }
}
