// Полоса по центру экрана: «Level N Complete!» и «The Cat Got Hit!». Лежит на корне префаба Banner.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class BannerView : MonoBehaviour
    {
        public Text title;
        public Text subtitle;

        [Header("Тексты ({0} — номер уровня)")]
        public string levelCompleteTitle = "Level {0} Complete!";
        public string levelCompleteSubtitle = "The bugs are scattering. Rolling on...";
        public string deathTitle = "The Cat Got Hit!";
        public string deathSubtitle = "Trying again...";

        public void ShowLevelComplete(int level) => Show(string.Format(levelCompleteTitle, level), levelCompleteSubtitle);
        public void ShowDeath() => Show(deathTitle, deathSubtitle);

        void Show(string t, string s)
        {
            title.text = t;
            subtitle.text = s;
        }
    }
}
