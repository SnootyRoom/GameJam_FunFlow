// Команды редактора для проверки сохранений игрока (CatTanks → Сохранения).
using UnityEditor;
using UnityEngine;

namespace CatTanks.EditorTools
{
    public static class SettingsMenu
    {
        [MenuItem("CatTanks/Сохранения/Показать обучение снова (как при первом запуске)")]
        static void ResetTutorial()
        {
            GameSettings.ResetTutorial();
            PlayerPrefs.Save();
            Debug.Log("[Сохранения] Обучение сброшено — следующий START снова его покажет.");
        }

        [MenuItem("CatTanks/Сохранения/Сбросить рекорд")]
        static void ResetRecord()
        {
            int level = GameSettings.BestLevel, stars = GameSettings.BestStars;
            if (!EditorUtility.DisplayDialog("Сбросить рекорд",
                    $"Сейчас рекорд: уровень {level}, звёзд {stars}.\n\nСтереть его?", "Сбросить", "Отмена"))
                return;
            GameSettings.ResetRecord();
            PlayerPrefs.Save();
            Debug.Log("[Сохранения] Рекорд сброшен.");
        }
    }
}
