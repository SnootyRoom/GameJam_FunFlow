// Главное меню: стартовый экран, главная колонка и настройки. Лежит на корне префаба MainMenu. Что делают кнопки — назначает GameUI.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class MainMenuView : MonoBehaviour
    {
        public enum Page { Title, Main, Settings }

        [Header("Стартовый экран (пусто = сразу меню)")]
        public TitleView title;

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

        [Header("Появление кнопок")]
        public float introSlide = 700f;     // откуда выезжают кнопки (влево от места), px
        public float introTime = 0.4f;
        public float introStagger = 0.07f;  // задержка между кнопками

        public void ShowPage(Page page)
        {
            if (title != null) title.gameObject.SetActive(page == Page.Title);
            mainGroup.SetActive(page == Page.Main);
            settingsGroup.SetActive(page == Page.Settings);
        }

        /// <summary>Кнопки главной колонки по очереди выезжают слева с лёгким перелётом.</summary>
        public void PlayIntro()
        {
            StopAllCoroutines();
            var buttons = new[] { play, tutorial, settings, quit };
            if (_home == null) // места кнопок запоминаем один раз, чтобы прерванная анимация их не сдвинула
            {
                _home = new Vector2[buttons.Length];
                for (int i = 0; i < buttons.Length; i++)
                    if (buttons[i] != null) _home[i] = ((RectTransform)buttons[i].transform).anchoredPosition;
            }
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null) StartCoroutine(SlideIn((RectTransform)buttons[i].transform, _home[i], i * introStagger));
        }

        Vector2[] _home;

        IEnumerator SlideIn(RectTransform rt, Vector2 to, float delay)
        {
            var from = to - new Vector2(introSlide, 0);
            rt.anchoredPosition = from;
            for (float e = -delay; e < introTime; e += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(e / introTime);
                const float c1 = 1.70158f, c3 = c1 + 1f; // ease-out-back: чуть проскакивает и возвращается
                float ease = 1f + c3 * Mathf.Pow(k - 1f, 3) + c1 * Mathf.Pow(k - 1f, 2);
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, ease);
                yield return null;
            }
            rt.anchoredPosition = to;
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
