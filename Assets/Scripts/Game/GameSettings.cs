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

        /// <summary>Забыть, что обучение показывали, — следующий START снова его откроет (для проверки первого запуска).</summary>
        public static void ResetTutorial() => PlayerPrefs.DeleteKey(TutorialSeenKey);

        /// <summary>Применить сохранённое при запуске.</summary>
        public static void Apply() => Sfx.MasterVolume = SoundOn ? SoundVolume : 0f;
    }
}
