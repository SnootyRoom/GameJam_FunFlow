// Экран Game Over: карточка с итогом забега и рекордом.
// Часть GameUIBuilder (partial): общие помощники и тема — в GameUIBuilder.cs.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public partial class GameUIBuilder
    {
        // ------------------------------------------------------------------ Game Over

        /// <summary>Карточка в стиле паузы: GAME OVER, итог забега, рекорд, «ещё раз» и «в меню».</summary>
        GameOverView BuildGameOver(Transform root)
        {
            var rt = UIFactory.Stretch(root, "GameOver");
            var v = rt.gameObject.AddComponent<GameOverView>();
            UIFactory.Image(rt, null, t.pauseBackdrop, true); // затемнение замершей арены

            const float w = 900f;
            var card = Card(rt, "Card", "GAME OVER", new Vector2(w, 880));
            v.card = card;
            InkText(card, "Lives", "THE CAT IS OUT OF HP", new Vector2(0, -215), new Vector2(w - 80, 60)).fontSize = 28;
            v.level = InkText(card, "Level", "REACHED LEVEL 1", new Vector2(0, -295), new Vector2(w - 80, 70));
            v.stars = InkText(card, "Stars", "STARS: 0", new Vector2(0, -365), new Vector2(w - 80, 70));
            var best = InkText(card, "Best", "BEST: LEVEL 1, 0 STARS", new Vector2(0, -432), new Vector2(w - 80, 50));
            best.fontSize = 24;
            best.color = Color.Lerp(t.pauseInk, t.pausePaperShade, 0.45f);
            v.best = best;

            var record = InkText(card, "NewRecord", "NEW RECORD!", new Vector2(0, -495), new Vector2(w - 80, 60));
            record.color = t.pauseHighlight;
            record.fontSize = 36;
            v.newRecord = record.gameObject;

            CardDivider(card, -545, w - 2 * PausePadding);
            v.tryAgain = PauseLikeButton(card, -640, t.pauseRestartButton, "TRY AGAIN");
            v.mainMenu = PauseLikeButton(card, -780, t.pauseMainMenuButton, "MAIN MENU");
            rt.gameObject.SetActive(false);
            return v;
        }

        /// <summary>Кнопка как в паузе (картинка pause_* или бумажная плашка), по центру карточки на высоте y.</summary>
        PixelButton PauseLikeButton(RectTransform card, float y, Sprite art, string label)
        {
            var anchor = new Vector2(0.5f, 1);
            var pos = new Vector2(0, y);
            if (art != null) return PixelButton.CreateImage(card, label, anchor, pos, art, PauseScale, null);
            return PixelButton.CreatePaper(card, label, anchor, pos, new Vector2(PauseButtonW, PauseButtonH), _paper, PauseBlock,
                                           label, _font, 36, t.pauseInk, t.pauseHighlight);
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
    }
}
