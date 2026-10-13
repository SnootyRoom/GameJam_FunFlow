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
    }
}
