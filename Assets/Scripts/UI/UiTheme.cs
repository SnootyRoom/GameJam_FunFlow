// Тема интерфейса: шрифт, картинки, цвета и тексты, из которых GameUIBuilder собирает префабы UI.
// Нужна только при (пере)сборке префабов: меню CatTanks → UI → Собрать префабы UI.
// После сборки вёрстку правят прямо в префабах (Assets/Prefabs/UI) — тема их не трогает, пока не пересоберёшь.
using UnityEngine;

namespace CatTanks.UI
{
    [CreateAssetMenu(menuName = "CatTanks/UI Theme", fileName = "UiTheme")]
    public class UiTheme : ScriptableObject
    {
        [Header("Тексты")]
        public string gameTitle = "Cat in a Tank";
        public string controlsHint = "WASD - move   Mouse - aim   LMB - fire   RMB - machine gun   Q - nut strike   Esc - pause";

        [Header("Шрифт и картинки художника (пусто = примитивы)")]
        public Font font;
        public Sprite logo;                   // вместо текстового заголовка, ~1024×512
        public Sprite menuBackground;         // фон главного меню; заполняет экран, лишнее обрезается
        public Sprite heartFull;              // 48×48
        public Sprite heartEmpty;
        public Sprite exitArrow;              // 48×48, смотрит вправо
        public Sprite crosshair;              // 32×32
        public Sprite panelSprite;            // полоса баннера «Level Complete»
        public Sprite ultIcon;                // иконка ульты в HUD (Assets/Art/Ult/walnut)

        [Header("Кнопки главного меню (картинки с готовым текстом; 456×120; пусто = бумажная кнопка с текстом)")]
        public Sprite startButton;
        public Sprite tutorialButton;
        public Sprite settingsButton;
        public Sprite exitButton;

        [Header("Пауза (картинки с готовым текстом, 520×104; пусто = подставятся из Assets/Art/UI/pause_*)")]
        public Sprite pauseContinueButton;
        public Sprite pauseRestartButton;
        public Sprite pauseTutorialButton;
        public Sprite pauseSettingsButton;
        [Tooltip("Картинки пока нет — будет текстовая кнопка на бумажной плашке")]
        public Sprite pauseMainMenuButton;
        [Tooltip("Палитра паузы — снята с кнопок pause_*")]
        public Color pauseInk = new Color32(42, 34, 51, 255);
        public Color pausePaper = new Color32(242, 234, 211, 255);
        public Color pausePaperLight = new Color32(255, 252, 238, 255);
        public Color pausePaperShade = new Color32(196, 184, 150, 255);
        public Color pauseHighlight = new Color32(137, 17, 70, 255);  // малиновый, как у CONTINUE
        public Color pauseBackdrop = new Color32(40, 40, 48, 170);

        [Header("Пиксельные заготовки (создаются сборщиком в Assets/Art/UI/Generated)")]
        public Sprite pixelPanel;             // запасная пиксельная подложка, 9-slice
        public Sprite paperPanel;             // подложка паузы и плашка текстовых кнопок паузы, 9-slice
        public Sprite circle, ring, triangle; // заглушки сердец, прицела и стрелки на выход

        [Header("Цвета")]
        public Color accent = new Color(1f, 0.65f, 0.25f);
        public Color textColor = Color.white;
        public Color dimColor = new Color(0f, 0f, 0f, 0.6f);
        public Color heartColor = new Color(0.95f, 0.3f, 0.35f);
    }
}
