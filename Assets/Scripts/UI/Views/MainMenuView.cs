// Главное меню: главная колонка и настройки. Лежит на корне префаба MainMenu. Что делают кнопки — назначает GameUI.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class MainMenuView : MonoBehaviour
    {
        public enum Page { Main, Settings }

        [Header("Главная")]
        public GameObject mainGroup;
        public PixelButton play;
        public PixelButton tutorial;
        public PixelButton settings;
        public PixelButton quit;

        [Header("Настройки")]
        public GameObject settingsGroup;
        public PixelButton fullscreen;
        public Text fullscreenLabel;
        public PixelButton sound;
        public Text soundLabel;
        public PixelButton back;
        public string fullscreenOn = "SCREEN: FULL";
        public string fullscreenOff = "SCREEN: WINDOW";
        public string soundOn = "SOUND: ON";
        public string soundOff = "SOUND: OFF";

        public void ShowPage(Page page)
        {
            mainGroup.SetActive(page == Page.Main);
            settingsGroup.SetActive(page == Page.Settings);
        }

        public void SetSoundLabel(bool on)
        {
            if (soundLabel != null) soundLabel.text = on ? soundOn : soundOff;
        }

        public void SetFullscreenLabel(bool full)
        {
            if (fullscreenLabel != null) fullscreenLabel.text = full ? fullscreenOn : fullscreenOff;
        }
    }
}
