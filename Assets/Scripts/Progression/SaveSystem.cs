// То, что переживает перезапуск игры: рекорд, выбранный скин, статистика. Хранится в PlayerPrefs как JSON.
using System;
using UnityEngine;

namespace CatTanks
{
    [Serializable]
    public class SaveData
    {
        public int bestLevel = 1;          // до какого уровня дошёл (открывает скины)
        public string selectedSkin = "classic";
        public string selectedHull = "hull_standard";
        public string selectedCannon = "gun_standard";
        public int totalKills;
        public int bestScore;
        public int bestCombo;

        // настройки
        public float sfxVolume = 0.7f;
        public bool tankControls = false;  // true: W/S вперёд-назад, A/D поворот; false: WASD по сторонам света
        public bool screenShake = true;
        public bool tutorialDone = false;
    }

    public static class SaveSystem
    {
        const string Key = "cattanks.save.v1";

        public static SaveData Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json)) return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            }
            catch (Exception e) { Debug.LogWarning($"[Save] Не удалось прочитать сохранение: {e.Message}"); }
            return new SaveData();
        }

        public static void Store(SaveData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Wipe() => PlayerPrefs.DeleteKey(Key);
    }
}
