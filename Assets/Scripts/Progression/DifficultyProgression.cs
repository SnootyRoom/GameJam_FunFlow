// Рост сложности по уровням: размер арены, лабиринтность, число и состав врагов, «ум» ИИ, лимит времени.
// Create → CatTanks → Difficulty Progression, назначить в GameConfig. Пусто = значения ниже.
using System;
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    /// <summary>
    /// Насколько умён ИИ на уровне. Поведения врагов читают эти множители в OnSpawned.
    /// 1 = базовое значение из поведения.
    /// </summary>
    [Serializable]
    public struct AiParams
    {
        [Tooltip("Множитель задержек (перепоиск пути, интервал выстрелов). Меньше = быстрее реагирует")]
        public float reaction;
        [Tooltip("Множитель разброса при стрельбе. Меньше = точнее")]
        public float aimError;
        [Tooltip("Множитель скорости движения")]
        public float speed;
        [Tooltip("Множитель дальности обнаружения игрока")]
        public float awareness;
        [Tooltip("Дополнительные отскоки при прицеливании рикошетом")]
        public int extraBounces;
        [Tooltip("Слышит выстрелы игрока и идёт на звук")]
        public bool reactToShots;
        [Tooltip("Уворачивается от снарядов (см. EnemySenses.FindIncomingBullet)")]
        public bool dodgeBullets;

        public static AiParams Normal => new AiParams { reaction = 1, aimError = 1, speed = 1, awareness = 1 };

        public static AiParams Lerp(AiParams a, AiParams b, float t) => new AiParams
        {
            reaction = Mathf.Lerp(a.reaction, b.reaction, t),
            aimError = Mathf.Lerp(a.aimError, b.aimError, t),
            speed = Mathf.Lerp(a.speed, b.speed, t),
            awareness = Mathf.Lerp(a.awareness, b.awareness, t),
            extraBounces = Mathf.RoundToInt(Mathf.Lerp(a.extraBounces, b.extraBounces, t)),
        };
    }

    [CreateAssetMenu(menuName = "CatTanks/Difficulty Progression", fileName = "DifficultyProgression")]
    public class DifficultyProgression : ScriptableObject
    {
        [Header("Уровень 1")]
        public LevelSettings baseSettings = new LevelSettings { width = 34, height = 24, maxDepth = 3 };

        [Header("Арена (прибавка за каждый следующий уровень)")]
        public float widthPerLevel = 3f;
        public float heightPerLevel = 2f;
        public int maxWidth = 60;
        public int maxHeight = 42;
        public int extraDepthFromLevel = 3;        // с этого уровня зон становится больше (maxDepth + 1)
        public int levelsPerLoopRemoved = 2;       // каждые N уровней −1 «лишний» проход → больше тупиков
        public int narrowDoorsFromLevel = 5;       // с этого уровня проходы уже (doorWidth − 1)

        [Header("Враги")]
        public int baseEnemies = 5;
        public float enemiesPerLevel = 1.6f;
        public int maxEnemies = 24;

        [Header("Элитные враги: больше HP, быстрее, чаще стреляют, золотая корона")]
        public int eliteFromLevel = 2;
        public float eliteChancePerLevel = 0.06f;   // +6% за уровень после eliteFromLevel
        public float maxEliteChance = 0.35f;

        [Header("ИИ: от уровня 1 до aiMaxLevel плавно меняется от easy к hard")]
        public AiParams easyAi = new AiParams { reaction = 1.15f, aimError = 1.3f, speed = 0.95f, awareness = 0.9f, extraBounces = 0 };
        public AiParams hardAi = new AiParams { reaction = 0.55f, aimError = 0.35f, speed = 1.35f, awareness = 1.45f, extraBounces = 1 };
        public int aiMaxLevel = 9;
        public int reactToShotsFromLevel = 2;
        public int dodgeFromLevel = 4;

        [Header("Секреты и время")]
        public int secretFishPerLevel = 1;
        public float timeLimitSeconds = 0f;        // 0 = без таймера (только секундомер)
        public float timeLimitPerLevel = 10f;      // арены растут — даём больше времени

        public LevelSettings BuildSettings(int level, EnemyCatalog catalog)
        {
            var s = baseSettings.Clone();
            int k = Mathf.Max(0, level - 1);
            s.difficulty = level;
            s.width = Mathf.Min(maxWidth, baseSettings.width + Mathf.RoundToInt(widthPerLevel * k));
            s.height = Mathf.Min(maxHeight, baseSettings.height + Mathf.RoundToInt(heightPerLevel * k));
            if (level >= extraDepthFromLevel) s.maxDepth = baseSettings.maxDepth + 1;
            if (levelsPerLoopRemoved > 0) s.extraLoops = Mathf.Max(0, baseSettings.extraLoops - k / levelsPerLoopRemoved);
            if (level >= narrowDoorsFromLevel) s.doorWidth = Mathf.Max(2, baseSettings.doorWidth - 1);
            s.enemyCount = Mathf.Min(maxEnemies, baseEnemies + Mathf.RoundToInt(enemiesPerLevel * k));
            s.enemyPool = catalog != null ? catalog.BuildPool(level) : new System.Collections.Generic.List<EnemySpawnRule>();
            s.secretFishCount = secretFishPerLevel;
            s.seed = 0;
            return s;
        }

        public AiParams GetAi(int level)
        {
            float t = aiMaxLevel <= 1 ? 1f : Mathf.Clamp01((level - 1) / (float)(aiMaxLevel - 1));
            var ai = AiParams.Lerp(easyAi, hardAi, t);
            ai.reactToShots = level >= reactToShotsFromLevel;
            ai.dodgeBullets = level >= dodgeFromLevel;
            return ai;
        }

        public float GetEliteChance(int level)
            => level < eliteFromLevel ? 0f : Mathf.Min(maxEliteChance, eliteChancePerLevel * (level - eliteFromLevel + 1));

        public float GetTimeLimit(int level)
            => timeLimitSeconds <= 0 ? 0f : timeLimitSeconds + timeLimitPerLevel * Mathf.Max(0, level - 1);

        public static DifficultyProgression CreateDefault()
        {
            var d = CreateInstance<DifficultyProgression>();
            d.name = "DifficultyProgression (default)";
            return d;
        }
    }
}
