// Меню паузы. Лежит на корне префаба Pause. Что делают кнопки — назначает GameUI.
// Две страницы на одной подложке: главная и настройки. Обучение — отдельный экран TutorialView.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class PauseView : MonoBehaviour
    {
        public enum Page { Main, Settings }

        [Header("Главная")]
        public GameObject mainGroup;
        public PixelButton resume;
        public PixelButton restart;
        public PixelButton tutorial;
        public PixelButton settings;
        public PixelButton mainMenu;

        [Header("Настройки")]
        public GameObject settingsGroup;
        public PixelButton fullscreen;
        public Text fullscreenLabel;
        public PixelButton sound;
        public Text soundLabel;
        public PixelButton settingsBack;
        public string fullscreenOn = "SCREEN: FULL";
        public string fullscreenOff = "SCREEN: WINDOW";
        public string soundOn = "SOUND: ON";
        public string soundOff = "SOUND: OFF";

        public void ShowPage(Page page)
        {
            if (mainGroup == null) return; // префаб собран старой версией сборщика — пересобери
            mainGroup.SetActive(page == Page.Main);
            settingsGroup.SetActive(page == Page.Settings);
        }

        public void SetFullscreenLabel(bool full)
        {
            if (fullscreenLabel != null) fullscreenLabel.text = full ? fullscreenOn : fullscreenOff;
        }

        public void SetSoundLabel(bool on)
        {
            if (soundLabel != null) soundLabel.text = on ? soundOn : soundOff;
        }
    }
}
