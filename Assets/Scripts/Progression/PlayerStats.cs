// Характеристики кота-танка: база из GameConfig → модули гаража (корпус, пушка) → купленные апгрейды.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    [Serializable]
    public struct PlayerStats
    {
        public int maxHp;
        public float moveSpeed;
        public float turnSpeed;
        public float fireCooldown;
        public float bulletSpeed;
        public int maxBounces;
        public int maxBulletsAlive;     // лимит снарядов на экране
        public int magazineSize;        // снарядов до перезарядки
        public float reloadTime;        // секунд на перезарядку
        public int ultKillsPerCharge;
        public int bulletDamage;
        public float bulletRadius;
        public float spreadDegrees;
        public float splashRadius;
        public int splashDamage;

        public static PlayerStats Default => new PlayerStats
        {
            maxHp = 3,
            moveSpeed = 3.5f,
            turnSpeed = 180f,
            fireCooldown = 0.3f,
            bulletSpeed = 8f,
            maxBounces = 1,
            maxBulletsAlive = 5,
            magazineSize = 5,
            reloadTime = 1.6f,
            ultKillsPerCharge = 5,
            bulletDamage = 2,
            bulletRadius = 0.12f,
        };

        /// <summary>База + модули гаража + все купленные в этом забеге апгрейды.</summary>
        public static PlayerStats Build(PlayerStats baseStats, TankModuleDefinition hull, TankModuleDefinition cannon,
                                        RunState run, IReadOnlyList<UpgradeDefinition> upgrades)
        {
            var s = baseStats;
            if (hull != null) hull.ApplyTo(ref s);
            if (cannon != null) cannon.ApplyTo(ref s);
            if (run != null && upgrades != null)
                foreach (var u in upgrades)
                {
                    if (u == null) continue;
                    int lvl = run.GetUpgradeLevel(u.id);
                    if (lvl > 0) u.Apply(ref s, lvl);
                }
            s.maxHp = Mathf.Max(1, s.maxHp);
            s.maxBulletsAlive = Mathf.Max(1, s.maxBulletsAlive);
            s.magazineSize = Mathf.Max(1, s.magazineSize);
            s.reloadTime = Mathf.Max(0.2f, s.reloadTime);
            s.ultKillsPerCharge = Mathf.Max(1, s.ultKillsPerCharge);
            s.bulletDamage = Mathf.Max(1, s.bulletDamage);
            s.bulletRadius = Mathf.Clamp(s.bulletRadius, 0.05f, 0.35f);
            return s;
        }
    }
}
