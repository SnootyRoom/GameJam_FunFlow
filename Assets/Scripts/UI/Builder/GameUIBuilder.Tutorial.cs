// Обучение: три бумажные карточки (How to play, Ultimate, Your goal) и помощники вёрстки карточек.
// Часть GameUIBuilder (partial): общие помощники и тема — в GameUIBuilder.cs.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public partial class GameUIBuilder
    {
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
    }
}
