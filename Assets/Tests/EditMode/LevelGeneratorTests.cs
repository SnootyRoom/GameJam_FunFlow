// Тесты генератора уровней (Window → General → Test Runner → EditMode → Run All).
// Ядро не зависит от Unity, поэтому тесты быстрые: сотни уровней за пару секунд.
// Запускай после любых правок генератора или LevelConfig — ловят недостижимый выход, врагов в стенах и т.п.
using System.Collections.Generic;
using System.Linq;
using CatTanks.Level;
using NUnit.Framework;

namespace CatTanks.Tests
{
    public class LevelGeneratorTests
    {
        static LevelSettings Settings() => new LevelSettings { coverClearance = 2 }; // как в LevelConfig_Default

        [Test]
        public void HundredsOfLevels_AreValid([Values(1, 2, 3, 4, 5)] int difficulty)
        {
            var s = Settings();
            for (int seed = 1; seed <= 200; seed++)
            {
                var d = BspLevelGenerator.Generate(s, seed, difficulty);
                Assert.GreaterOrEqual(d.distFromStart[d.exit.x, d.exit.y], 0, $"seed {seed}: выход недостижим");
                Assert.IsNotNull(d.mainPath, $"seed {seed}: нет пути A→Б");
                foreach (var e in d.enemies)
                    for (int x = 0; x < e.footprint; x++)
                        for (int y = 0; y < e.footprint; y++)
                            Assert.IsTrue(d.IsWalkable(e.pos.x + x, e.pos.y + y), $"seed {seed}: {e.id} стоит в стене {e.pos}");
            }
        }

        [Test]
        public void SameSeed_GivesSameLevel()
        {
            var a = BspLevelGenerator.Generate(Settings(), 1234, 3);
            var b = BspLevelGenerator.Generate(Settings(), 1234, 3);
            CollectionAssert.AreEqual(a.tiles, b.tiles);
            CollectionAssert.AreEqual(a.enemies.Select(e => e.id + e.pos), b.enemies.Select(e => e.id + e.pos));
        }

        [Test]
        public void Guard_AppearsFromItsDifficulty_AndOnlyOnce()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                Assert.AreEqual(0, BspLevelGenerator.Generate(Settings(), seed, 1).enemies.Count(e => e.id == "Enemy_Crab"));
                Assert.AreEqual(1, BspLevelGenerator.Generate(Settings(), seed, 3).enemies.Count(e => e.id == "Enemy_Crab"));
            }
        }

        [Test]
        public void SpawnRules_LimitsAreRespected()
        {
            var rules = new List<EnemySpawnRule>
            {
                new EnemySpawnRule { id = "Rare", weight = 1, maxPerLevel = 2 },
                new EnemySpawnRule { id = "Common", weight = 3 },
            };
            for (int seed = 1; seed <= 50; seed++)
            {
                var d = BspLevelGenerator.Generate(Settings(), seed, 5, rules);
                Assert.LessOrEqual(d.enemies.Count(e => e.id == "Rare"), 2);
                Assert.IsTrue(d.enemies.All(e => e.id == "Rare" || e.id == "Common"));
            }
        }
    }
}
