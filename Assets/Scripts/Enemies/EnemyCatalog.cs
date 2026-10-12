// Список всех типов врагов. Генератор уровня берёт отсюда, кого и с каким весом ставить.
// Create → CatTanks → Enemy Catalog, назначить в GameConfig. Пусто = CreateDefault() ниже.
using System.Collections.Generic;
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    [CreateAssetMenu(menuName = "CatTanks/Enemy Catalog", fileName = "EnemyCatalog")]
    public class EnemyCatalog : ScriptableObject
    {
        public List<EnemyDefinition> definitions = new List<EnemyDefinition>();

        public EnemyDefinition Get(EnemyType type)
        {
            foreach (var d in definitions) if (d != null && d.type == type) return d;
            return null;
        }

        /// <summary>Кто может появиться на этом уровне (для LevelSettings.enemyPool).</summary>
        public List<EnemySpawnRule> BuildPool(int level)
        {
            var pool = new List<EnemySpawnRule>();
            foreach (var d in definitions)
                if (d != null && d.AppearsOn(level)) pool.Add(d.ToSpawnRule());
            return pool;
        }

        /// <summary>
        /// Каталог по умолчанию (средне-повышенная сложность). Шкала: стандартная пушка игрока — 2 урона,
        /// Дальнобой — 4, Мяуглот — 6 + 3 по площади. Обычный враг (4 HP) — 2 выстрела стандартной пушки.
        /// HP растёт с уровнем (hpPerLevel), так что к 8–9 уровню враги заметно толще.
        /// </summary>
        public static EnemyCatalog CreateDefault()
        {
            var c = CreateInstance<EnemyCatalog>();
            c.name = "EnemyCatalog (default)";

            var crawler = EnemyDefinition.Create(EnemyType.Crawler, "Ползун");
            crawler.maxHp = 4; crawler.hpPerLevel = 0.3f; crawler.moveSpeed = 2.4f; crawler.contactDamage = 1;
            crawler.spawnWeight = 0.5f; crawler.color = new Color(0.85f, 0.25f, 0.25f); crawler.size = 0.7f;
            crawler.score = 100; crawler.fishDropChance = 0.3f;

            var runner = EnemyDefinition.Create(EnemyType.Runner, "Шустрик");
            runner.maxHp = 2; runner.hpPerLevel = 0.15f; runner.moveSpeed = 4.1f; runner.contactDamage = 1;
            runner.minLevel = 2; runner.spawnWeight = 0.35f; runner.color = new Color(1f, 0.5f, 0.72f); runner.size = 0.55f;
            runner.score = 80; runner.fishDropChance = 0.25f;

            var brute = EnemyDefinition.Create(EnemyType.Brute, "Громила");
            brute.maxHp = 12; brute.hpPerLevel = 0.8f; brute.moveSpeed = 1.5f; brute.contactDamage = 2;
            brute.minLevel = 3; brute.spawnWeight = 0.2f; brute.color = new Color(0.45f, 0.5f, 0.62f); brute.size = 1.05f;
            brute.score = 300; brute.fishDropChance = 0.7f; brute.fishDrop = 2;

            var shooter = EnemyDefinition.Create(EnemyType.Shooter, "Стрелок");
            shooter.maxHp = 4; shooter.hpPerLevel = 0.3f; shooter.moveSpeed = 0f; shooter.contactDamage = 0;
            shooter.spawnWeight = 0.35f; shooter.prefersCover = true;
            shooter.color = new Color(0.6f, 0.25f, 0.8f); shooter.size = 0.8f;
            shooter.fireInterval = 2f; shooter.bulletSpeed = 6.5f; shooter.aimErrorDegrees = 2f; shooter.maxBounces = 1;
            shooter.score = 150; shooter.fishDropChance = 0.35f;

            var gunner = EnemyDefinition.Create(EnemyType.Gunner, "Пулемётчик");
            gunner.maxHp = 6; gunner.hpPerLevel = 0.4f; gunner.moveSpeed = 0f; gunner.contactDamage = 0;
            gunner.minLevel = 3; gunner.spawnWeight = 0.2f; gunner.prefersCover = true;
            gunner.color = new Color(0.95f, 0.45f, 0.2f); gunner.size = 0.85f;
            gunner.fireInterval = 2.6f; gunner.burst = 4; gunner.burstGap = 0.12f; gunner.bulletSpeed = 7.5f;
            gunner.bulletRadius = 0.09f; gunner.aimErrorDegrees = 6f; gunner.maxBounces = 0; gunner.sightRange = 12f; gunner.telegraphTime = 0.35f;
            gunner.score = 220; gunner.fishDropChance = 0.5f;

            var sniper = EnemyDefinition.Create(EnemyType.Sniper, "Снайпер");
            sniper.maxHp = 4; sniper.hpPerLevel = 0.2f; sniper.moveSpeed = 0f; sniper.contactDamage = 0;
            sniper.minLevel = 4; sniper.spawnWeight = 0.15f; sniper.prefersCover = true;
            sniper.color = new Color(0.2f, 0.72f, 0.68f); sniper.size = 0.75f;
            sniper.fireInterval = 3.2f; sniper.bulletSpeed = 14f; sniper.bulletRadius = 0.1f; sniper.aimErrorDegrees = 0.4f;
            sniper.sightRange = 24f; sniper.maxBounces = 2; sniper.telegraphTime = 0.75f;
            sniper.score = 250; sniper.fishDropChance = 0.5f;

            // босс — на каждом 3-м уровне (3, 6, 9…), стережёт рыбку; атаки — в BossBehaviour
            var boss = EnemyDefinition.Create(EnemyType.Crab, "Краб-Планшет");
            boss.maxHp = 50; boss.hpPerLevel = 4f; boss.moveSpeed = 1.6f; boss.contactDamage = 2;
            boss.minLevel = 3; boss.levelInterval = 3; boss.isGuard = true; boss.isBoss = true; boss.footprint = 2;
            boss.color = new Color(0.95f, 0.55f, 0.1f); boss.size = 1.75f;
            boss.fireInterval = 1.3f; boss.maxBounces = 1; boss.telegraphTime = 0.4f; boss.bulletSpeed = 6.5f;
            boss.score = 2000; boss.fishDropChance = 1f; boss.fishDrop = 6;

            c.definitions.AddRange(new[] { crawler, runner, brute, shooter, gunner, sniper, boss });
            return c;
        }
    }
}
