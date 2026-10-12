// «Паспорт» типа врага: общие характеристики + правила появления в генераторе.
// Создать ассет: Create → CatTanks → Enemy Definition, добавить в EnemyCatalog.
// Специфичные параметры поведения (дальность стрельбы, частота перепоиска пути) живут в самих поведениях.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    [CreateAssetMenu(menuName = "CatTanks/Enemy Definition", fileName = "Enemy_")]
    public class EnemyDefinition : ScriptableObject
    {
        public EnemyType type;
        public string displayName = "Телефон";
        [Tooltip("Пусто = собрать из примитивов в EnemyFactory")]
        public GameObject prefab;

        [Header("Характеристики")]
        public int maxHp = 1;
        public float hpPerLevel = 0f;         // прибавка HP за каждый уровень после minLevel (враги «толстеют»)
        public float moveSpeed = 2f;
        public int contactDamage = 1;
        public float invulnerableAfterHit = 0.05f;

        [Header("Оружие (для стреляющих: TurretBehaviour берёт значения отсюда)")]
        public float fireInterval = 2.2f;     // секунд между очередями
        public int burst = 1;                 // выстрелов в очереди
        public float burstGap = 0.12f;
        public float bulletSpeed = 6f;
        public int bulletDamage = 1;
        public float bulletRadius = 0.12f;
        public float aimErrorDegrees = 2f;
        public float sightRange = 14f;
        public int maxBounces = 1;            // сколько отскоков ИИ учитывает при прицеливании
        public float telegraphTime = 0.25f;   // лазерный прицел перед выстрелом — время среагировать

        [Header("Появление на уровнях")]
        public int minLevel = 1;              // с какого уровня может появиться
        public float spawnWeight = 1f;        // относительная частота среди обычных врагов
        public bool prefersCover = false;     // генератор ставит его рядом с укрытиями
        public bool isGuard = false;          // страж: один на уровень, рядом с рыбкой
        [Range(1, 3)] public int footprint = 1; // сколько клеток занимает (2 = площадка 2×2)
        public bool isBoss = false;           // босс: полоса HP сверху, рыбка заперта, пока он жив
        public int levelInterval = 0;         // >0 — появляется только на каждом N-м уровне (босс-уровни)

        [Header("Награда")]
        public int score = 100;               // очки за убийство (× комбо)
        [Range(0f, 1f)] public float fishDropChance = 0.3f;
        public int fishDrop = 1;

        [Header("Примитив-заглушка")]
        public Color color = Color.red;
        public float size = 0.7f;

        public int HpAtLevel(int level) => maxHp + Mathf.FloorToInt(hpPerLevel * Mathf.Max(0, level - minLevel));

        public bool AppearsOn(int level) => level >= minLevel && (levelInterval <= 0 || level % levelInterval == 0);

        public EnemySpawnRule ToSpawnRule() => new EnemySpawnRule
        {
            type = type,
            weight = spawnWeight,
            prefersCover = prefersCover,
            guard = isGuard,
            footprint = Mathf.Max(1, footprint),
        };

        public static EnemyDefinition Create(EnemyType type, string name)
        {
            var d = CreateInstance<EnemyDefinition>();
            d.type = type;
            d.displayName = name;
            d.name = name;
            return d;
        }
    }
}
