// Состояние текущего забега (с первого уровня до поражения/выхода). Не сохраняется между запусками игры —
// для этого SaveData.
using System.Collections.Generic;

namespace CatTanks
{
    public class RunState
    {
        public int levelNumber = 1;
        public int levelSeed;
        public int levelsCompleted;
        public int fish;                 // валюта для прокачки
        public int totalKills;
        public int levelKills;
        public int secretsFound;

        // очки и рекорды забега
        public int score;
        public int bestCombo;
        public int bestScoreAtStart;      // рекорд на момент начала забега — чтобы поймать момент «новый рекорд!»
        public bool newRecordAnnounced;

        // итоги последнего пройденного уровня (для экрана победы)
        public int lastStars;
        public bool lastCleared, lastFast;
        public int lastClearBonus, lastTimeBonus, lastStarFish;
        public readonly Dictionary<string, int> upgradeLevels = new Dictionary<string, int>();

        public int GetUpgradeLevel(string id) => id != null && upgradeLevels.TryGetValue(id, out var l) ? l : 0;
    }
}
