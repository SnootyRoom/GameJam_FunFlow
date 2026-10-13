// Баннер «Level Complete» со звёздами и «The Cat Got Hit!».
// Часть GameUIBuilder (partial): общие помощники и тема — в GameUIBuilder.cs.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public partial class GameUIBuilder
    {
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
    }
}
