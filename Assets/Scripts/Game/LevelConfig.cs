// Пресет генерации уровней (Create → CatTanks → Level Config). Можно держать несколько и менять в GameFlow.
// Только настройки: текущие сид и сложность живут в GameFlow и в ассет не пишутся.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    [CreateAssetMenu(menuName = "CatTanks/Level Config", fileName = "LevelConfig")]
    public class LevelConfig : ScriptableObject
    {
        public LevelSettings generation = new LevelSettings();

        [Header("Сложность")]
        [Range(1, 5)] public int startDifficulty = 1;
        [Range(1, 5)] public int maxDifficulty = 5;
    }
}
