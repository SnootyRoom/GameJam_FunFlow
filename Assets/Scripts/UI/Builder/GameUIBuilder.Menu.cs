// Главное меню, настройки и стартовый экран «TANK CAT».
// Часть GameUIBuilder (partial): общие помощники и тема — в GameUIBuilder.cs.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public partial class GameUIBuilder
    {
        // Колонка главного меню по референсу: центр кнопок по X от левого края и положения по Y от центра экрана (экран 1920×1080).
        // Картинки кнопок button_* нарисованы блоками по 8 px → масштаб 1.25 даёт ровные блоки по 10 px, как в паузе.
        const float MenuColumnX = 530f, MenuFirstButtonY = 300f, MenuButtonStep = 198f;
        const float MenuButtonScale = 1.25f, MenuButtonW = 456f * MenuButtonScale, MenuButtonH = 120f * MenuButtonScale;

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

            v.title = BuildTitle(rt);

            // Раскладка по референсу: без подложки и заголовка, колонка из четырёх кнопок слева, справа кот на фоне.
            var main = UIFactory.Stretch(rt, "Main");
            v.mainGroup = main.gameObject;

            v.play = ColumnButton(main, 0, t.startButton, "START");
            v.tutorial = ColumnButton(main, 1, t.tutorialButton, "TUTORIAL");
            v.settings = ColumnButton(main, 2, t.settingsButton, "SETTINGS");
            v.quit = ColumnButton(main, 3, t.exitButton, "EXIT");

            // Настройки — на месте главной колонки, в том же стиле. Новые пункты добавляй сюда (или прямо в префаб).
            var settings = UIFactory.Stretch(rt, "Settings");
            v.settingsGroup = settings.gameObject;
            MenuTitle(settings, "SETTINGS");
            v.fullscreen = ColumnButton(settings, 1, null, v.fullscreenOn);
            v.fullscreenLabel = v.fullscreen.label;
            v.sound = ColumnButton(settings, 2, null, v.soundOn);
            v.soundLabel = v.sound.label;
            v.back = ColumnButton(settings, 3, null, "BACK");
            settings.gameObject.SetActive(false);
            return v;
        }

        /// <summary>Кнопка колонки меню: index — номер сверху. Картинка художника или такая же по виду бумажная плашка с текстом.</summary>
        PixelButton ColumnButton(RectTransform parent, int index, Sprite art, string label)
        {
            var anchor = new Vector2(0, 0.5f);
            var pos = new Vector2(MenuColumnX, MenuFirstButtonY - index * MenuButtonStep);
            if (art != null) return PixelButton.CreateImage(parent, label, anchor, pos, art, MenuButtonScale, null);
            return PixelButton.CreatePaper(parent, label, anchor, pos, new Vector2(MenuButtonW, MenuButtonH), _paper, PauseBlock,
                                           label, _font, 36, t.pauseInk, t.pauseHighlight);
        }

        /// <summary>Крупный белый заголовок на месте первой кнопки колонки, пиксельным шрифтом.</summary>
        void MenuTitle(RectTransform parent, string text)
        {
            var rt = UIFactory.Rect(parent, "Title", new Vector2(0, 0.5f), new Vector2(MenuColumnX, MenuFirstButtonY), new Vector2(1100, 160));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var title = UIFactory.Text(rt, _font, text, 64, TextAnchor.MiddleCenter, Color.white);
            UIFactory.HardShadow(title, 6, t.pauseInk);
            var outline = title.gameObject.AddComponent<Outline>(); // тёмный контур — читается и на небе, и на облаках
            outline.effectColor = t.pauseInk;
            outline.effectDistance = new Vector2(3, -3);
        }

        // ------------------------------------------------------------------ Стартовый экран

        // По референсу: малиновый «TANK CAT» в светлой обводке с тёмной тенью, ниже светлый подзаголовок
        // и внизу «PRESS ANY KEY». Шрифт пиксельный — размеры кратны 8, чтобы пиксели оставались ровными.
        TitleView BuildTitle(RectTransform menu)
        {
            var rt = UIFactory.Stretch(menu, "Title");
            var v = rt.gameObject.AddComponent<TitleView>();
            v.group = rt.gameObject.AddComponent<CanvasGroup>();
            v.group.blocksRaycasts = false;

            if (t.logo != null)
            {
                v.title = UIFactory.Rect(rt, "Logo", new Vector2(0, 0.5f), new Vector2(MenuColumnX, 270), new Vector2(980, 300));
                v.title.pivot = new Vector2(0.5f, 0.5f);
                UIFactory.Image(v.title, t.logo, Color.white).preserveAspect = true;
            }
            else v.title = TitleText(rt, "TankCat", t.titleText, 120, new Vector2(MenuColumnX, 270), t.pauseHighlight, t.pausePaperLight, 8);

            v.subtitle = TitleText(rt, "Subtitle", t.subtitleText, 40, new Vector2(MenuColumnX, 140), t.pausePaperLight, t.pauseInk, 4);
            v.prompt = TitleText(rt, "PressAnyKey", t.pressKeyText, 48, new Vector2(MenuColumnX, -360), t.pausePaperLight, t.pauseInk, 4);

            var flash = UIFactory.Stretch(rt, "Flash");
            v.flash = UIFactory.Image(flash, null, new Color(1, 1, 1, 0));
            return v;
        }

        /// <summary>Пиксельная надпись: заливка, обводка на outline px и жёсткая тень того же цвета, что и тёмный контур.</summary>
        RectTransform TitleText(RectTransform parent, string name, string text, int size, Vector2 pos, Color fill, Color outline, float px)
        {
            var rt = UIFactory.Rect(parent, name, new Vector2(0, 0.5f), pos, new Vector2(1200, size * 2));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var label = UIFactory.Text(rt, _font, text, size, TextAnchor.MiddleCenter, fill);
            Object.DestroyImmediate(label.GetComponent<Shadow>());
            var o = label.gameObject.AddComponent<Outline>();
            o.effectColor = outline;
            o.effectDistance = new Vector2(px, -px);
            var s = label.gameObject.AddComponent<Shadow>(); // после обводки — тень падает от всей надписи вместе с ней
            s.effectColor = t.pauseInk;
            s.effectDistance = new Vector2(px * 1.5f, -px * 1.5f);
            return rt;
        }
    }
}
