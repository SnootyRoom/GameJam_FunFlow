// Полоса по центру экрана: «Level N Complete!» со звёздами и «The Cat Got Hit!». Лежит на корне префаба Banner.
// Звёзды вылетают по очереди (на unscaled time — игра в этот момент стоит): заработанная — золотая с «прыжком»,
// не заработанная — тёмная. Под каждой — за что она.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class BannerView : MonoBehaviour
    {
        public Text title;
        public Text subtitle;

        [Header("Звёзды")]
        public RectTransform starsRow;
        public Image[] stars;
        public Text[] starLabels;
        public Color starOn = new Color32(255, 196, 56, 255);
        public Color starOff = new Color(0.35f, 0.3f, 0.4f, 0.75f);
        [Tooltip("Картинки звезды: заработанная / нет (star_active / star_inactive). Пусто = одна картинка, разница — цветом")]
        public Sprite activeSprite, inactiveSprite;
        public float starDelay = 0.3f;      // пауза между звёздами
        public float starPopTime = 0.25f;

        [Header("Тексты ({0} — номер уровня)")]
        public string levelCompleteTitle = "Level {0} Complete!";
        public string levelCompleteSubtitle = "The bugs are scattering. Rolling on...";
        public string perfectSubtitle = "Purr-fect run!";
        public string deathTitle = "The Cat Got Hit!";
        public string deathSubtitle = "Trying again...";

        /// <summary>Звезда показана (номер, заработана ли) — для звука.</summary>
        public static event Action<int, bool> StarRevealed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { StarRevealed = null; }

        public void ShowLevelComplete(int level, bool[] earned = null, string[] labels = null)
        {
            bool all = earned != null && earned.Length > 0 && Array.TrueForAll(earned, e => e);
            Show(string.Format(levelCompleteTitle, level), all ? perfectSubtitle : levelCompleteSubtitle);
            StopAllCoroutines();
            bool hasStars = earned != null && earned.Length > 0 && stars != null && stars.Length > 0;
            if (starsRow != null) starsRow.gameObject.SetActive(hasStars);
            if (hasStars) StartCoroutine(RevealStars(earned, labels));
        }

        public void ShowDeath()
        {
            StopAllCoroutines();
            if (starsRow != null) starsRow.gameObject.SetActive(false);
            Show(deathTitle, deathSubtitle);
        }

        void Show(string t, string s)
        {
            title.text = t;
            subtitle.text = s;
        }

        IEnumerator RevealStars(bool[] earned, string[] labels)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                bool used = i < earned.Length;
                stars[i].gameObject.SetActive(used);
                stars[i].rectTransform.localScale = Vector3.zero;
                if (starLabels != null && i < starLabels.Length)
                {
                    starLabels[i].gameObject.SetActive(used);
                    starLabels[i].text = used && labels != null && i < labels.Length ? labels[i] : "";
                    starLabels[i].color = new Color(1, 1, 1, 0);
                }
            }

            for (int i = 0; i < earned.Length && i < stars.Length; i++)
            {
                yield return new WaitForSecondsRealtime(i == 0 ? 0.15f : starDelay);
                var star = stars[i];
                star.color = earned[i] ? starOn : starOff;
                var sprite = earned[i] ? activeSprite : inactiveSprite;
                if (sprite != null) star.sprite = sprite;
                StarRevealed?.Invoke(i, earned[i]);
                if (starLabels != null && i < starLabels.Length)
                    starLabels[i].color = earned[i] ? Color.white : new Color(1, 1, 1, 0.45f);

                // заработанная — выпрыгивает с перелётом, потерянная — просто проявляется
                float overshoot = earned[i] ? 1.45f : 1f;
                for (float e = 0f; e < starPopTime; e += Time.unscaledDeltaTime)
                {
                    float k = e / starPopTime;
                    float s = k < 0.6f ? Mathf.Lerp(0f, overshoot, k / 0.6f) : Mathf.Lerp(overshoot, 1f, (k - 0.6f) / 0.4f);
                    star.rectTransform.localScale = Vector3.one * s;
                    yield return null;
                }
                star.rectTransform.localScale = Vector3.one;
            }
        }
    }
}
