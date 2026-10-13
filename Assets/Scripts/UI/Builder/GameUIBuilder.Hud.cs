// HUD: здоровье, уровень, звёзды, стрелка на выход, ульта, боезапас, пулемёт, босс-бар, прицел.
// Часть GameUIBuilder (partial): общие помощники и тема — в GameUIBuilder.cs.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public partial class GameUIBuilder
    {
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
    }
}
