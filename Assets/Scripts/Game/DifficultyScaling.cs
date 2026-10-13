// Рост сложности от уровня к уровню (кроме числа врагов — его растит генератор по Difficulty).
// С каждым уровнем враги чуть быстрее и злее: пули летят быстрее, стреляют чаще, меньше мажут,
// а предупреждения (мигание перед выстрелом, замах клеща) становятся короче. После maxSteps уровней рост останавливается.
// Числа — в LevelConfig → «Рост сложности». Применяется к каждому врагу сразу после EnemyDefinition.ApplyTo.
using System;
using UnityEngine;

namespace CatTanks
{
    [Serializable]
    public class DifficultyScaling
    {
        [Tooltip("После скольких уровней рост останавливается (уровень 1 — без изменений)")]
        public int maxSteps = 8;

        [Header("Стрелки")]
        [Tooltip("+доля скорости пули за уровень (0.06 = +6%)")]
        public float bulletSpeedPerLevel = 0.06f;
        [Tooltip("−доля паузы между выстрелами за уровень")]
        public float fireIntervalPerLevel = 0.06f;
        [Tooltip("Предупреждение о выстреле на последнем шаге, секунды (на 1-м уровне — как в EnemyShooter)")]
        public float telegraphTimeMin = 0.28f;
        [Tooltip("Разброс прицела на последнем шаге, градусы")]
        public float aimErrorMin = 0.5f;

        [Header("Клещи")]
        [Tooltip("+доля скорости бега за уровень")]
        public float crawlerSpeedPerLevel = 0.05f;
        [Tooltip("Замах перед рывком на последнем шаге, секунды")]
        public float windupTimeMin = 0.3f;

        /// <summary>0 на первом уровне, 1 — на уровне 1 + maxSteps и дальше.</summary>
        public float Progress(int levelNumber) => maxSteps <= 0 ? 0f : Mathf.Clamp01((levelNumber - 1) / (float)maxSteps);

        public void Apply(GameObject enemy, int levelNumber)
        {
            int steps = Mathf.Clamp(levelNumber - 1, 0, Mathf.Max(0, maxSteps));
            float t = Progress(levelNumber);
            if (steps == 0) return;

            var shooter = enemy.GetComponentInChildren<EnemyShooter>();
            if (shooter != null)
            {
                shooter.bulletSpeed *= 1f + bulletSpeedPerLevel * steps;
                shooter.fireInterval *= Mathf.Max(0.4f, 1f - fireIntervalPerLevel * steps);
                shooter.telegraphTime = Mathf.Lerp(shooter.telegraphTime, Mathf.Min(telegraphTimeMin, shooter.telegraphTime), t);
                shooter.aimErrorDegrees = Mathf.Lerp(shooter.aimErrorDegrees, Mathf.Min(aimErrorMin, shooter.aimErrorDegrees), t);
            }

            var crawler = enemy.GetComponentInChildren<EnemyCrawler>();
            if (crawler != null)
            {
                crawler.speed *= 1f + crawlerSpeedPerLevel * steps;
                crawler.windupTime = Mathf.Lerp(crawler.windupTime, Mathf.Min(windupTimeMin, crawler.windupTime), t);
            }
        }
    }
}
