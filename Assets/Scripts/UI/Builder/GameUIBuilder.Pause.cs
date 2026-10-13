// Пауза: бумажная карточка с колонкой кнопок; заголовок и разделитель карточек — общие для всех карточек.
// Часть GameUIBuilder (partial): общие помощники и тема — в GameUIBuilder.cs.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public partial class GameUIBuilder
    {
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
    }
}
