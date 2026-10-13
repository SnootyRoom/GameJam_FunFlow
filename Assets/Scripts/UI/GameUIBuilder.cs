// Собирает весь UI по теме: холст + экраны (HUD, баннер, пауза, главное меню) с заполненными View-компонентами.
// Используется редакторской командой CatTanks → UI → Собрать префабы UI (результат сохраняется в префабы)
// и как запасной вариант в игре, если префаба нет. Вёрстку дальше удобнее править в префабах.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class GameUIBuilder
    {
        // Колонка главного меню по референсу: центр кнопок по X от левого края и положения по Y от центра экрана (экран 1920×1080).
        // Картинки кнопок button_* нарисованы блоками по 8 px → масштаб 1.25 даёт ровные блоки по 10 px, как в паузе.
        const float MenuColumnX = 530f, MenuFirstButtonY = 300f, MenuButtonStep = 198f;
        const float MenuButtonScale = 1.25f, MenuButtonW = 456f * MenuButtonScale, MenuButtonH = 120f * MenuButtonScale;

        readonly UiTheme t;
        readonly Font _font;
        readonly Sprite _paper; // бумажная плашка: подложки и кнопки без готовой картинки

        GameUIBuilder(UiTheme theme)
        {
            t = theme;
            _font = theme.font != null ? theme.font : UIFactory.DefaultFont();
            _paper = Or(theme.paperPanel, UIFactory.PaperSprite(theme.pauseInk, theme.pausePaperLight, theme.pausePaper,
                                                                theme.pausePaperShade, new Color32(0, 0, 0, 110)));
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
            view.tutorial = b.BuildTutorial(canvas.transform);
            return view;
        }

        static Sprite Or(Sprite sprite, Sprite fallback) => sprite != null ? sprite : fallback;

        // ------------------------------------------------------------------ HUD

        // Каждый виджет живёт в своём контейнере на весь экран, который никогда не выключается:
        // виджет прячет только свои картинки и продолжает следить за игрой (иначе выключенный не смог бы показаться снова).
        HudView BuildHud(Transform root)
        {
            var rt = UIFactory.Stretch(root, "Hud");
            var v = rt.gameObject.AddComponent<HudView>();

            // здоровье кота: полоска из сегментов, если художник её нарисовал, иначе — сердечки
            if (t.hpFrame != null && t.hpSegmentFull != null) BuildPlayerHp(rt);
            else BuildHearts(rt);
            if (t.bossHpFrame != null && t.bossHpSegmentFull != null) BuildBossBar(rt);
            BuildLevelInfo(rt);
            BuildLiveStars(rt);
            BuildExitArrow(rt);
            BuildUltWidget(rt);
            BuildAmmoWidget(rt);
            BuildMinigunWidget(rt);

            v.toast = UIFactory.Text(UIFactory.Rect(rt, "Toast", new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1200, 60)),
                                     _font, "", 24, TextAnchor.MiddleCenter, t.accent);
            v.toast.gameObject.SetActive(false);

            BuildCrosshair(rt); // последним — поверх остального HUD
            return v;
        }

        static T Widget<T>(RectTransform hud, string name) where T : Component
            => UIFactory.Stretch(hud, name).gameObject.AddComponent<T>();

        void BuildHearts(RectTransform hud)
        {
            var w = Widget<HeartsWidget>(hud, "HeartsWidget");
            w.row = UIFactory.Rect(w.transform, "Hearts", new Vector2(0, 1), new Vector2(32, -28), new Vector2(400, 56));
            var row = w.row.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 10;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var heart = UIFactory.Rect(w.row, "HeartTemplate", new Vector2(0, 0.5f), Vector2.zero, new Vector2(48, 48));
            w.heartTemplate = UIFactory.Image(heart, Or(t.heartFull, Or(t.circle, Prims.Circle)), Color.white);
            heart.gameObject.SetActive(false);
            w.heartFull = t.heartFull;
            w.heartEmpty = t.heartEmpty;
            w.heartColor = t.heartColor;
        }

        // Полоски из сегментов: картинки нарисованы блоками по 4 px, ×1.5 — блоки по 6 px. Рамка — 16 px с каждой стороны.
        const float HpScale = 1.5f, HpFrameInset = 16f * HpScale, HpGap = 4f * HpScale;

        /// <summary>Рамка с сегментами (общая для кота и босса): frame — картинка рамки, внутри SegmentBar.</summary>
        SegmentBar SegmentFrame(RectTransform parent, Vector2 anchor, Vector2 pos, Sprite frame, Sprite full, Sprite empty, Sprite lost)
        {
            var frameRt = UIFactory.Rect(parent, "Frame", anchor, pos, frame.rect.size * HpScale);
            UIFactory.Image(frameRt, frame, Color.white);
            var inner = UIFactory.Stretch(frameRt, "Segments");
            inner.offsetMin = new Vector2(HpFrameInset, HpFrameInset);
            inner.offsetMax = -inner.offsetMin;
            var bar = frameRt.gameObject.AddComponent<SegmentBar>();
            bar.inner = inner;
            bar.shakeTarget = frameRt;
            bar.full = full;
            bar.empty = empty;
            bar.lost = lost;
            bar.gap = HpGap;
            return bar;
        }

        /// <summary>Здоровье кота слева сверху: иконка и полоска из сегментов.</summary>
        void BuildPlayerHp(RectTransform hud)
        {
            var w = Widget<PlayerHpWidget>(hud, "PlayerHpWidget");
            w.root = UIFactory.Rect(w.transform, "PlayerHp", new Vector2(0, 1), new Vector2(28, -20), new Vector2(700, 96));
            if (t.hpIcon != null)
            {
                var icon = UIFactory.Rect(w.root, "Icon", new Vector2(0, 0.5f), Vector2.zero, t.hpIcon.rect.size * HpScale);
                w.icon = UIFactory.Image(icon, t.hpIcon, Color.white);
            }
            float x = t.hpIcon != null ? t.hpIcon.rect.width * HpScale + 8f : 0f;
            w.bar = SegmentFrame(w.root, new Vector2(0, 0.5f), new Vector2(x, 0), t.hpFrame, t.hpSegmentFull, t.hpSegmentEmpty, t.hpSegmentLost);
            w.root.gameObject.SetActive(false);
        }

        /// <summary>Здоровье босса внизу по центру: табличка с именем над левым краем и полоска из сегментов.</summary>
        void BuildBossBar(RectTransform hud)
        {
            var w = Widget<BossBarWidget>(hud, "BossBarWidget");
            var size = t.bossHpFrame.rect.size * HpScale;
            w.root = UIFactory.Rect(w.transform, "BossHp", new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(size.x, size.y + 90));
            w.bar = SegmentFrame(w.root, new Vector2(0.5f, 0), Vector2.zero, t.bossHpFrame, t.bossHpSegmentFull, t.bossHpSegmentEmpty, null);
            var plate = UIFactory.Rect(w.root, "Name", new Vector2(0, 1), new Vector2(12, 0), new Vector2(204, 96));
            w.nameplate = UIFactory.Image(plate, null, Color.white);
            w.nameplate.gameObject.SetActive(false);
            w.root.gameObject.SetActive(false);
        }

        void BuildLevelInfo(RectTransform hud)
        {
            var w = Widget<LevelInfoWidget>(hud, "LevelInfoWidget");
            w.level = UIFactory.Text(UIFactory.Rect(w.transform, "Level", new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(400, 56)),
                                     _font, "Level 1", 32, TextAnchor.MiddleCenter, t.textColor);
            w.enemies = UIFactory.Text(UIFactory.Rect(w.transform, "Enemies", new Vector2(1, 1), new Vector2(-32, -24), new Vector2(400, 56)),
                                       _font, "Enemies: 0", 32, TextAnchor.MiddleRight, t.textColor);
        }

        /// <summary>Звёзды уровня под номером уровня и счёт звёзд за забег под счётчиком врагов.</summary>
        void BuildLiveStars(RectTransform hud)
        {
            const int count = 3;
            const float size = 39f, step = 50f; // звезда 13 px → ровно ×3
            var w = Widget<StarsWidget>(hud, "StarsWidget");
            w.stars = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var star = UIFactory.Rect(w.transform, "Star" + i, new Vector2(0.5f, 1), new Vector2((i - (count - 1) / 2f) * step, -92), new Vector2(size, size));
                star.pivot = new Vector2(0.5f, 0.5f);
                w.stars[i] = UIFactory.Image(star, Or(t.starIcon, UIFactory.PixelStarSprite()), t.starPossible);
                w.stars[i].preserveAspect = true;
                star.gameObject.SetActive(false);
            }
            w.starOn = t.starOn;
            w.starPossible = t.starPossible;
            w.starOff = t.starOff;
            if (t.starInactive != null) // у художника две картинки — цвет менять не нужно, только затемнить потерянную
            {
                w.activeSprite = t.starIcon;
                w.inactiveSprite = t.starInactive;
                w.starOn = w.starPossible = Color.white;
                w.starOff = new Color(0.55f, 0.5f, 0.6f, 0.85f);
            }
            w.total = UIFactory.Text(UIFactory.Rect(w.transform, "Total", new Vector2(1, 1), new Vector2(-32, -80), new Vector2(400, 48)),
                                     _font, "Stars: 0", 24, TextAnchor.MiddleRight, t.starOn);
            UIFactory.HardShadow(w.total, 3, new Color(0, 0, 0, 0.85f));
        }

        void BuildExitArrow(RectTransform hud)
        {
            var w = Widget<ExitArrowWidget>(hud, "ExitArrowWidget");
            w.arrow = UIFactory.Rect(w.transform, "ExitArrow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            w.arrow.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(w.arrow, ExitArrowSprite(), ExitArrowColor());
            w.arrow.gameObject.SetActive(false);
        }

        /// <summary>Ульта «Орех» в левом нижнем углу: иконка, клавиша, квадратики-заряды и полоска до следующего заряда.</summary>
        void BuildUltWidget(RectTransform hud)
        {
            var w = Widget<UltWidget>(hud, "UltWidget");
            var root = UIFactory.Rect(w.transform, "Ultimate", Vector2.zero, new Vector2(32, 32), new Vector2(280, 96));
            w.root = root;

            var icon = UIFactory.Rect(root, "Icon", Vector2.zero, Vector2.zero, new Vector2(96, 96));
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(48, 48); // масштаб «дыхания» — от центра
            w.icon = UIFactory.Image(icon, Or(t.ultIcon, Or(t.circle, Prims.Circle)), Color.white);
            w.icon.preserveAspect = true;

            w.key = UIFactory.Text(UIFactory.Rect(root, "Key", Vector2.zero, new Vector2(70, -2), new Vector2(40, 32)),
                                   _font, "Q", 24, TextAnchor.LowerRight, t.textColor);
            UIFactory.HardShadow(w.key, 3, new Color(0, 0, 0, 0.9f));

            w.pipOn = t.accent;
            w.pips = new Image[3];
            for (int i = 0; i < w.pips.Length; i++)
            {
                var pip = UIFactory.Rect(root, "Charge" + i, Vector2.zero, new Vector2(112 + i * 30, 48), new Vector2(22, 22));
                w.pips[i] = UIFactory.Image(pip, null, w.pipOff);
                var outline = pip.gameObject.AddComponent<Outline>(); // пиксельная рамка
                outline.effectColor = new Color(0, 0, 0, 0.85f);
                outline.effectDistance = new Vector2(3, -3);
            }

            var bar = UIFactory.Rect(root, "Bar", Vector2.zero, new Vector2(112, 18), new Vector2(150, 14));
            UIFactory.Image(bar, null, new Color(0f, 0f, 0f, 0.55f));
            var fill = UIFactory.Stretch(bar, "Fill");
            fill.anchorMax = new Vector2(0f, 1f);
            UIFactory.Image(fill, null, t.accent);
            w.barFill = fill;
            root.gameObject.SetActive(false); // покажется, если у танка есть ульта
        }

        /// <summary>Боезапас над ультой: иконка на каждый снаряд в полёте и полоска перезарядки под ними.</summary>
        void BuildAmmoWidget(RectTransform hud)
        {
            const int max = 8;                  // запас иконок; лишние прячет виджет по лимиту танка
            const float icon = 48f, step = 54f; // картинка 16 px → ровно ×3
            var w = Widget<AmmoWidget>(hud, "AmmoWidget");
            var root = UIFactory.Rect(w.transform, "Ammo", Vector2.zero, new Vector2(32, 144), new Vector2(max * step, icon + 22));
            w.root = root;
            w.pips = new Image[max];
            for (int i = 0; i < max; i++)
            {
                var pip = UIFactory.Rect(root, "Shell" + i, Vector2.zero, new Vector2(i * step + icon / 2f, 22 + icon / 2f), new Vector2(icon, icon));
                pip.pivot = new Vector2(0.5f, 0.5f); // «подпрыгивает» от центра
                w.pips[i] = UIFactory.Image(pip, Or(t.ammoIcon, Or(t.circle, Prims.Circle)), Color.white);
                w.pips[i].preserveAspect = true;
                var shadow = pip.gameObject.AddComponent<Shadow>(); // жёсткая тень — читается на любом полу
                shadow.effectColor = new Color(0, 0, 0, 0.6f);
                shadow.effectDistance = new Vector2(3, -3);
            }

            var bar = UIFactory.Rect(root, "Reload", Vector2.zero, new Vector2(0, 2), new Vector2(5 * step - (step - icon), 12));
            UIFactory.Image(bar, null, new Color(0f, 0f, 0f, 0.55f));
            var fill = UIFactory.Stretch(bar, "Fill");
            fill.offsetMin = new Vector2(2, 2);
            fill.offsetMax = new Vector2(-2, -2);
            UIFactory.Image(fill, null, Color.white);
            w.reloadFill = fill;
            w.reloadColor = t.accent;
            root.gameObject.SetActive(false); // покажется, если у игрока PlayerTank
        }

        /// <summary>Магазин пулемёта над боезапасом: маленькие патроны и полоска перезарядки справа.</summary>
        void BuildMinigunWidget(RectTransform hud)
        {
            const int max = 8;
            const float pw = 15f, ph = 30f, step = 21f;
            var w = Widget<MinigunWidget>(hud, "MinigunWidget");
            var root = UIFactory.Rect(w.transform, "Minigun", Vector2.zero, new Vector2(32, 226), new Vector2(max * step + 160, ph));
            w.root = root;
            w.pips = new Image[max];
            for (int i = 0; i < max; i++)
            {
                var pip = UIFactory.Rect(root, "Round" + i, Vector2.zero, new Vector2(i * step, 0), new Vector2(pw, ph));
                w.pips[i] = UIFactory.Image(pip, null, Color.white);
                var outline = pip.gameObject.AddComponent<Outline>(); // пиксельная рамка
                outline.effectColor = new Color(0, 0, 0, 0.85f);
                outline.effectDistance = new Vector2(3, -3);
            }
            // полоска перезарядки — сразу за патронами (видна только во время перезарядки)
            var bar = UIFactory.Rect(root, "Reload", Vector2.zero, new Vector2(4 * step + 8, 9), new Vector2(120, 12));
            UIFactory.Image(bar, null, new Color(0f, 0f, 0f, 0.55f));
            var fill = UIFactory.Stretch(bar, "Fill");
            fill.offsetMin = new Vector2(2, 2);
            fill.offsetMax = new Vector2(-2, -2);
            UIFactory.Image(fill, null, w.roundOn);
            w.reloadFill = fill;
            bar.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }

        void BuildCrosshair(RectTransform hud)
        {
            var w = Widget<CrosshairWidget>(hud, "CrosshairWidget");
            w.crosshair = UIFactory.Rect(w.transform, "Crosshair", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36, 36));
            w.crosshair.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(w.crosshair, Or(t.crosshair, Or(t.ring, Prims.Ring)), t.crosshair != null ? Color.white : t.accent);
            w.crosshair.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Баннер

        BannerView BuildBanner(Transform root)
        {
            var rt = UIFactory.Stretch(root, "Banner");
            var v = rt.gameObject.AddComponent<BannerView>();
            var strip = UIFactory.Rect(rt, "Strip", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2200, 420));
            strip.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(strip, t.panelSprite, t.panelSprite != null ? Color.white : t.dimColor);
            v.title = UIFactory.Text(UIFactory.Rect(strip, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 135), new Vector2(1600, 100)),
                                     _font, "", 64, TextAnchor.MiddleCenter, t.accent);
            v.title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            v.subtitle = UIFactory.Text(UIFactory.Rect(strip, "Subtitle", new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1600, 60)),
                                        _font, "", 32, TextAnchor.MiddleCenter, t.textColor);
            v.subtitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            BannerStars(strip, v);
            rt.gameObject.SetActive(false);
            return v;
        }

        /// <summary>Три звезды под заголовком и подпись под каждой (за что). Вылетают по очереди — анимирует BannerView.</summary>
        void BannerStars(RectTransform strip, BannerView v)
        {
            const int count = 3;
            const float size = 104f, step = 260f; // звезда 13 px → ровно ×8
            var row = UIFactory.Rect(strip, "Stars", new Vector2(0.5f, 0.5f), new Vector2(0, -75), new Vector2(count * step, 200));
            row.pivot = new Vector2(0.5f, 0.5f);
            v.starsRow = row;
            v.stars = new Image[count];
            v.starLabels = new Text[count];
            v.starOn = t.starOn;
            v.starOff = t.starOff;
            if (t.starInactive != null)
            {
                v.activeSprite = t.starIcon;
                v.inactiveSprite = t.starInactive;
                v.starOn = v.starOff = Color.white;
            }
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) / 2f) * step;
                var star = UIFactory.Rect(row, "Star" + i, new Vector2(0.5f, 0.5f), new Vector2(x, 25), new Vector2(size, size));
                star.pivot = new Vector2(0.5f, 0.5f);
                v.stars[i] = UIFactory.Image(star, Or(t.starIcon, UIFactory.PixelStarSprite()), t.starOn);
                v.stars[i].preserveAspect = true;
                var label = UIFactory.Rect(row, "Label" + i, new Vector2(0.5f, 0.5f), new Vector2(x, -65), new Vector2(step, 40));
                label.pivot = new Vector2(0.5f, 0.5f);
                v.starLabels[i] = UIFactory.Text(label, _font, "", 24, TextAnchor.MiddleCenter, Color.white);
                UIFactory.HardShadow(v.starLabels[i], 3, new Color(0, 0, 0, 0.85f));
            }
            row.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Пауза

        // По референсу: бумажная карточка с тёмным контуром, заголовок PAUSE, полоса-разделитель, колонка кнопок.
        // Картинки кнопок pause_* нарисованы блоками по 8 px → масштаб 1.25 даёт ровные блоки по 10 px.
        const float PauseScale = 1.25f, PauseBlock = 8f * PauseScale;
        const float PauseButtonW = 520f * PauseScale, PauseButtonH = 104f * PauseScale;
        const float PauseFirstY = -235f, PauseStep = 145f, PausePadding = 60f;

        PauseView BuildPause(Transform root)
        {
            var rt = UIFactory.Stretch(root, "Pause");
            var v = rt.gameObject.AddComponent<PauseView>();
            UIFactory.Image(rt, null, t.pauseBackdrop, true); // затемнение, заодно блокирует клики по игре

            var paper = _paper;
            float height = -PauseFirstY + 4 * PauseStep + PauseButtonH / 2f + PausePadding;
            var panel = UIFactory.Rect(rt, "Panel", new Vector2(0.5f, 0.5f), Vector2.zero,
                                       new Vector2(PauseButtonW + 2 * PausePadding + PauseBlock, height));
            panel.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.PixelPanel(panel, paper, Color.white, PauseBlock);

            var main = UIFactory.Stretch(panel, "Main");
            v.mainGroup = main.gameObject;
            PauseHeader(main, "PAUSE");
            v.resume = PauseButton(main, 0, t.pauseContinueButton, "CONTINUE", paper);
            v.restart = PauseButton(main, 1, t.pauseRestartButton, "RESTART", paper);
            v.tutorial = PauseButton(main, 2, t.pauseTutorialButton, "TUTORIAL", paper);
            v.settings = PauseButton(main, 3, t.pauseSettingsButton, "SETTINGS", paper);
            v.mainMenu = PauseButton(main, 4, t.pauseMainMenuButton, "MAIN MENU", paper);

            var settings = UIFactory.Stretch(panel, "Settings");
            v.settingsGroup = settings.gameObject;
            PauseHeader(settings, "SETTINGS");
            v.fullscreen = PauseButton(settings, 0, null, v.fullscreenOn, paper);
            v.fullscreenLabel = v.fullscreen.label;
            v.sound = PauseButton(settings, 1, null, v.soundOn, paper);
            v.soundLabel = v.sound.label;
            v.settingsBack = PauseButton(settings, 4, null, "BACK", paper);

            v.ShowPage(PauseView.Page.Main);
            rt.gameObject.SetActive(false);
            return v;
        }

        /// <summary>Заголовок бумажной карточки и полоса-разделитель под ним. width — ширина разделителя.</summary>
        void PauseHeader(RectTransform parent, string text, float width = PauseButtonW + PausePadding)
        {
            var title = UIFactory.Text(UIFactory.Rect(parent, "Title", new Vector2(0.5f, 1), new Vector2(0, -85), new Vector2(width, 100)),
                                       _font, text, 56, TextAnchor.MiddleCenter, t.pauseInk);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Object.DestroyImmediate(title.GetComponent<Shadow>());

            CardDivider(parent, -145, width); // разделитель под заголовком, как на референсе
        }

        /// <summary>Горизонтальная полоса-разделитель на бумажной карточке (y — от верха карточки).</summary>
        void CardDivider(RectTransform parent, float y, float width)
        {
            var line = UIFactory.Rect(parent, "Divider", new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(width, PauseBlock / 2f));
            line.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(line, null, t.pausePaperShade);
        }

        /// <summary>Кнопка колонки паузы: картинка художника или такая же по виду бумажная плашка с текстом.</summary>
        PixelButton PauseButton(RectTransform parent, int index, Sprite art, string label, Sprite paper)
        {
            var anchor = new Vector2(0.5f, 1);
            var pos = new Vector2(0, PauseFirstY - index * PauseStep);
            if (art != null) return PixelButton.CreateImage(parent, label, anchor, pos, art, PauseScale, null);
            return PixelButton.CreatePaper(parent, label, anchor, pos, new Vector2(PauseButtonW, PauseButtonH), paper, PauseBlock,
                                           label, _font, 36, t.pauseInk, t.pauseHighlight);
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

        // ------------------------------------------------------------------ Стрелка на выход (HUD и обучение)

        Sprite ExitArrowSprite() => Or(t.exitArrow, Or(t.pixelArrow, UIFactory.PixelArrowSprite()));
        Color ExitArrowColor() => t.exitArrow != null ? Color.white : t.pauseHighlight;

        // ------------------------------------------------------------------ Обучение

        // Три бумажные карточки по референсам. Координаты — от верхнего края карточки, ось X — от её центра.
        const int TutorialFont = 44;
        const float KeyHeight = 92f, KeyBlock = 6f;
        const float SmallButtonW = 280f * PauseScale, SmallButtonH = 104f * PauseScale;

        TutorialView BuildTutorial(Transform root)
        {
            var rt = UIFactory.Stretch(root, "Tutorial");
            var v = rt.gameObject.AddComponent<TutorialView>();
            UIFactory.Image(rt, null, t.pauseBackdrop, true); // затемнение, заодно блокирует клики под карточкой

            // 1. HOW TO PLAY: клавиши прижаты к линии split слева, подписи — справа от неё
            var howTo = Card(rt, "HowToPlay", "HOW TO PLAY", new Vector2(1160, 900));
            const float split = -90f;
            ControlRow(howTo, -250, split, "MOVE", "W", "A", "S", "D");
            ControlRow(howTo, -375, split, "AIM", "CURSOR");
            ControlRow(howTo, -500, split, "SHOOT", "LMB");
            ControlRow(howTo, -625, split, "MACHINE GUN", "RMB");
            v.skip = SmallButton(howTo, t.skipButton, "SKIP", new Vector2(-190, -780));
            var next1 = SmallButton(howTo, t.nextButton, "NEXT", new Vector2(190, -780));

            // 2. ULTIMATE: полоска заряда и клавиша Q
            var ult = Card(rt, "Ultimate", "ULTIMATE", new Vector2(1000, 830));
            InkText(ult, "Text", "KILL ENEMIES\nTO FILL THE BAR", new Vector2(0, -270), new Vector2(900, 140)).lineSpacing = 1.3f;
            UltBar(ult, new Vector2(0, -410), new Vector2(520, 56), 6, 10);
            var press = InlineRow(ult, "Press", -545, KeyHeight);
            InkText(press, "Press", "PRESS", Vector2.zero, Vector2.zero);
            Keycap(press, "Q");
            InkText(press, "ToUse", "TO USE", Vector2.zero, Vector2.zero);
            var next2 = SmallButton(ult, t.nextButton, "NEXT", new Vector2(0, -700));

            // 3. YOUR GOAL: стрелка → рыба (выход уровня), разделитель, звёзды (2 из 3 — пример, что их надо собрать)
            var goal = Card(rt, "YourGoal", "YOUR GOAL", new Vector2(1000, 920));
            InkText(goal, "Reach", "REACH THE FISH", new Vector2(0, -225), new Vector2(900, 80));
            var icons = InlineRow(goal, "Icons", -318, 104);
            var arrow = UIFactory.Rect(icons, "Arrow", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Fixed(arrow, 40, 60);
            UIFactory.Image(arrow, ExitArrowSprite(), ExitArrowColor()).preserveAspect = true;
            InkText(icons, "Dots", "...", Vector2.zero, Vector2.zero).fontSize = 24;
            var fish = UIFactory.Rect(icons, "Fish", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Fixed(fish, 104, 104);
            UIFactory.Image(fish, Or(t.tutorialFish, Prims.Circle), Color.white).preserveAspect = true;
            InkText(goal, "Follow", "FOLLOW THE ARROW", new Vector2(0, -418), new Vector2(900, 80));

            CardDivider(goal, -485, 1000 - 2 * PausePadding);
            InkText(goal, "Stars", "COLLECT ALL STARS", new Vector2(0, -560), new Vector2(900, 80));
            var starsRow = InlineRow(goal, "StarsRow", -650, 52);
            starsRow.GetComponent<HorizontalLayoutGroup>().spacing = 12;
            for (int i = 0; i < 3; i++)
            {
                bool earned = i < 2;
                var star = UIFactory.Rect(starsRow, "Star" + i, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Fixed(star, 52, 52); // звезда 104 px → ровно ×0.5, 13 px из кода → ×4
                var sprite = earned || t.starInactive == null ? Or(t.starIcon, UIFactory.PixelStarSprite()) : t.starInactive;
                var color = t.starInactive != null ? Color.white : earned ? t.starOn : t.starOff;
                UIFactory.Image(star, sprite, color).preserveAspect = true;
            }
            v.go = SmallButton(goal, t.goButton, "GO", new Vector2(0, -790));

            v.cards = new[] { howTo.gameObject, ult.gameObject, goal.gameObject };
            v.next = new[] { next1, next2 };
            rt.gameObject.SetActive(false);
            return v;
        }

        RectTransform Card(RectTransform root, string name, string title, Vector2 size)
        {
            var card = UIFactory.Rect(root, name, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            card.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.PixelPanel(card, _paper, Color.white, PauseBlock);
            PauseHeader(card, title, size.x - 2 * PausePadding);
            return card;
        }

        /// <summary>Тёмный пиксельный текст без тени. pos/size не важны, если текст лежит в строке с раскладкой.</summary>
        Text InkText(RectTransform parent, string name, string text, Vector2 pos, Vector2 size,
                     TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = UIFactory.Rect(parent, name, new Vector2(0.5f, 1), pos, size);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var label = UIFactory.Text(rt, _font, text, TutorialFont, align, t.pauseInk);
            Object.DestroyImmediate(label.GetComponent<Shadow>());
            return label;
        }

        /// <summary>Строка по центру карточки: дети выстраиваются слева направо по своей ширине.</summary>
        RectTransform InlineRow(RectTransform card, string name, float y, float height)
        {
            var row = UIFactory.Rect(card, name, new Vector2(0.5f, 1), new Vector2(0, y),
                                     new Vector2(card.sizeDelta.x - 2 * PausePadding, height));
            row.pivot = new Vector2(0.5f, 0.5f);
            Row(row, TextAnchor.MiddleCenter, 24);
            return row;
        }

        /// <summary>Строка «клавиши — действие»: клавиши прижаты вправо к split, подпись начинается справа от него.</summary>
        void ControlRow(RectTransform card, float y, float split, string action, params string[] keys)
        {
            var keysRt = UIFactory.Rect(card, action + " Keys", new Vector2(0.5f, 1), new Vector2(split - 15, y), new Vector2(520, KeyHeight));
            keysRt.pivot = new Vector2(1, 0.5f);
            Row(keysRt, TextAnchor.MiddleRight, 12);
            foreach (var k in keys) Keycap(keysRt, k);

            var label = InkText(card, action, action, new Vector2(split + 15, y), new Vector2(600, KeyHeight), TextAnchor.MiddleLeft);
            label.rectTransform.pivot = new Vector2(0, 0.5f);
        }

        /// <summary>Клавиша: маленькая бумажная плашка с буквой, ширина по тексту.</summary>
        void Keycap(RectTransform row, string key)
        {
            const int fontSize = 36;
            var cap = UIFactory.Rect(row, "Key " + key, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Fixed(cap, key.Length * fontSize + 48 + KeyBlock, KeyHeight);
            UIFactory.PixelPanel(cap, _paper, Color.white, KeyBlock).raycastTarget = false;
            var labelRt = UIFactory.Stretch(cap, "Label");
            labelRt.offsetMin = new Vector2(0, KeyBlock);   // без полосы тени справа-снизу
            labelRt.offsetMax = new Vector2(-KeyBlock, 0);
            var label = UIFactory.Text(labelRt, _font, key, fontSize, TextAnchor.MiddleCenter, t.pauseInk);
            Object.DestroyImmediate(label.GetComponent<Shadow>());
        }

        /// <summary>Полоска заряда ульты как в референсе: контур, пустая часть и сегменты заливки.</summary>
        void UltBar(RectTransform card, Vector2 pos, Vector2 size, int filled, int segments)
        {
            var bar = UIFactory.Rect(card, "Bar", new Vector2(0.5f, 1), pos, size);
            bar.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(bar, null, t.pauseInk);
            var inner = UIFactory.Stretch(bar, "Empty");
            inner.offsetMin = new Vector2(PauseBlock * 0.8f, PauseBlock * 0.8f);
            inner.offsetMax = -inner.offsetMin;
            UIFactory.Image(inner, null, t.pausePaperShade);

            var fill = UIFactory.Stretch(inner, "Fill");
            fill.anchorMax = new Vector2((float)filled / segments, 1);
            UIFactory.Image(fill, null, t.ultBarColor);
            var divider = Color.Lerp(t.ultBarColor, t.pausePaper, 0.5f);
            for (int i = 1; i < filled; i++)
            {
                var line = UIFactory.Stretch(fill, "Divider" + i);
                line.anchorMin = new Vector2((float)i / filled, 0);
                line.anchorMax = new Vector2((float)i / filled, 1);
                line.offsetMin = new Vector2(-PauseBlock / 4f, 0);
                line.offsetMax = new Vector2(PauseBlock / 4f, 0);
                UIFactory.Image(line, null, divider);
            }
        }

        /// <summary>NEXT / GO / SKIP — картинка художника 280×104; без картинки — такая же бумажная кнопка.</summary>
        PixelButton SmallButton(RectTransform card, Sprite art, string label, Vector2 pos)
        {
            var anchor = new Vector2(0.5f, 1);
            if (art != null) return PixelButton.CreateImage(card, label, anchor, pos, art, PauseScale, null);
            return PixelButton.CreatePaper(card, label, anchor, pos, new Vector2(SmallButtonW, SmallButtonH), _paper, PauseBlock,
                                           label, _font, 36, t.pauseInk, t.pauseHighlight);
        }

        static void Row(RectTransform rt, TextAnchor align, float spacing)
        {
            var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = align;
            row.spacing = spacing;
            row.childControlWidth = row.childControlHeight = true; // ширина текста — по самому тексту
            row.childForceExpandWidth = row.childForceExpandHeight = false;
        }

        static void Fixed(RectTransform rt, float w, float h)
        {
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = w;
            le.preferredHeight = le.minHeight = h;
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
