// Главное меню и экран настроек. Лежит на корне префаба MainMenu. Что делают кнопки — назначает GameUI.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class MainMenuView : MonoBehaviour
    {
        [Header("Главная")]
        public GameObject mainGroup;
        public PixelButton play;
        public PixelButton settings;
        public PixelButton quit;

        [Header("Настройки")]
        public GameObject settingsGroup;
        public PixelButton fullscreen;
        public Text fullscreenLabel;
        public PixelButton sound;
        public Text soundLabel;
        public PixelButton back;
        public string fullscreenOn = "Screen: Full";
        public string fullscreenOff = "Screen: Window";
        public string soundOn = "Sound: ON";
        public string soundOff = "Sound: OFF";

        public void SetSoundLabel(bool on)
        {
            if (soundLabel != null) soundLabel.text = on ? soundOn : soundOff;
        }

        public void ShowSettings(bool show)
        {
            settingsGroup.SetActive(show);
            mainGroup.SetActive(!show);
        }

        public void SetFullscreenLabel(bool full)
        {
            if (fullscreenLabel != null) fullscreenLabel.text = full ? fullscreenOn : fullscreenOff;
        }
    }
}
