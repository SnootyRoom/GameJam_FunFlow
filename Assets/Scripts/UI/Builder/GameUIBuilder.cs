// Собирает весь UI по теме: холст + экраны (HUD, баннер, пауза, Game Over, главное меню, обучение) с заполненными View.
// Разбит на части по экранам (partial): GameUIBuilder.Hud.cs, .Menu.cs, .Pause.cs, .Tutorial.cs, .GameOver.cs, .Banner.cs.
// Здесь — общее: тема, шрифт, бумажная плашка, сборка холста и стрелка на выход.
// Используется редакторской командой CatTanks → UI → Собрать префабы UI (результат сохраняется в префабы)
// и как запасной вариант в игре, если префаба нет. Вёрстку дальше удобнее править в префабах.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public partial class GameUIBuilder
    {
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
            view.gameOver = b.BuildGameOver(canvas.transform);
            view.menu = b.BuildMenu(canvas.transform);
            view.tutorial = b.BuildTutorial(canvas.transform);
            return view;
        }

        static Sprite Or(Sprite sprite, Sprite fallback) => sprite != null ? sprite : fallback;

        // ------------------------------------------------------------------ Стрелка на выход (HUD и обучение)

        Sprite ExitArrowSprite() => Or(t.exitArrow, Or(t.pixelArrow, UIFactory.PixelArrowSprite()));
        Color ExitArrowColor() => t.exitArrow != null ? Color.white : t.pauseHighlight;
    }
}
