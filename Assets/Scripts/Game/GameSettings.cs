// Сохраняемые настройки игрока в одном месте (PlayerPrefs). Новая настройка — новое свойство здесь,
// а не ещё один ключ где-нибудь в UI.
using UnityEngine;

namespace CatTanks
{
    public static class GameSettings
    {
        const string SoundKey = "sound_on";
        const string TutorialSeenKey = "tutorial_seen";
        const float SoundVolume = 0.7f;

        /// <summary>Звук и музыка. Применяется сразу (Sfx.MasterVolume) и запоминается.</summary>
        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
                Sfx.MasterVolume = value ? SoundVolume : 0f;
            }
        }

        /// <summary>Обучение уже показывали (первый Play его больше не открывает).</summary>
        public static bool TutorialSeen
        {
            get => PlayerPrefs.GetInt(TutorialSeenKey, 0) == 1;
            set => PlayerPrefs.SetInt(TutorialSeenKey, value ? 1 : 0);
        }

        const string BestLevelKey = "best_level";
        const string BestStarsKey = "best_stars";

        /// <summary>Рекорд: до какого уровня дошёл и сколько звёзд собрал за один забег.</summary>
        public static int BestLevel => PlayerPrefs.GetInt(BestLevelKey, 0);
        public static int BestStars => PlayerPrefs.GetInt(BestStarsKey, 0);
        /// <summary>Последний законченный забег побил рекорд (для надписи NEW RECORD!).</summary>
        public static bool LastRunWasRecord { get; private set; }

        /// <summary>Забег закончен (Game Over): обновить рекорды.</summary>
        public static void SubmitRun(int level, int stars)
        {
            LastRunWasRecord = level > BestLevel || (level == BestLevel && stars > BestStars);
            if (LastRunWasRecord)
            {
                PlayerPrefs.SetInt(BestLevelKey, level);
                PlayerPrefs.SetInt(BestStarsKey, stars);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Забыть, что обучение показывали, — следующий START снова его откроет (для проверки первого запуска).</summary>
        public static void ResetTutorial() => PlayerPrefs.DeleteKey(TutorialSeenKey);

        /// <summary>Применить сохранённое при запуске.</summary>
        public static void Apply() => Sfx.MasterVolume = SoundOn ? SoundVolume : 0f;
    }
}
