// Экран Game Over поверх замершей арены: итог забега (уровень, звёзды), рекорд, «ещё раз» и «в меню».
// Лежит на корне префаба GameOver. Что делают кнопки — назначает GameUI.
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class GameOverView : MonoBehaviour
    {
        public RectTransform card;
        public Text level;
        public Text stars;
        public Text best;
        public GameObject newRecord;
        public PixelButton tryAgain;
        public PixelButton mainMenu;

        [Header("Тексты ({0} — число)")]
        public string levelFormat = "REACHED LEVEL {0}";
        public string starsFormat = "STARS: {0}";
        public string bestFormat = "BEST: LEVEL {0}, {1} STARS";

        [Header("Появление")]
        public float dropTime = 0.45f;
        public float dropFrom = 700f;

        Vector2 _rest;
        bool _cached;

        public void Show(int reachedLevel, int totalStars, int bestLevel, int bestStars, bool isRecord)
        {
            gameObject.SetActive(true);
            level.text = string.Format(levelFormat, reachedLevel);
            stars.text = string.Format(starsFormat, totalStars);
            best.text = string.Format(bestFormat, bestLevel, bestStars);
            if (newRecord != null) newRecord.SetActive(isRecord);
            if (!_cached) { _cached = true; _rest = card.anchoredPosition; }
            StopAllCoroutines();
            StartCoroutine(Drop());
        }

        // карточка падает сверху с отскоком; время в игре стоит — анимация на unscaled time
        IEnumerator Drop()
        {
            for (float e = 0f; e < dropTime; e += Time.unscaledDeltaTime)
            {
                float k = e / dropTime;
                const float c1 = 1.70158f, c3 = c1 + 1f;
                float ease = 1f + c3 * Mathf.Pow(k - 1f, 3) + c1 * Mathf.Pow(k - 1f, 2);
                card.anchoredPosition = _rest + new Vector2(0f, dropFrom * (1f - ease));
                if (newRecord != null && newRecord.activeSelf)
                    newRecord.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f));
                yield return null;
            }
            card.anchoredPosition = _rest;
            while (newRecord != null && newRecord.activeSelf)
            {
                newRecord.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f)); // рекорд «дышит»
                yield return null;
            }
        }
    }
}
