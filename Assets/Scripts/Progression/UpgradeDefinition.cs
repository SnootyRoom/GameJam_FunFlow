// Апгрейд кота между уровнями: покупается за рыбок в магазине на экране победы.
// Create → CatTanks → Upgrade, добавить в GameConfig. Пусто = CreateDefaults().
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public enum UpgradeStat
    {
        MaxHp,
        MoveSpeed,
        FireRate,         // amount = доля сокращения кулдауна за уровень (0.15 = −15%)
        BulletSpeed,
        Bounces,
        MaxBullets,       // +магазин и +лимит снарядов на экране
        UltCharge,        // amount = на сколько меньше убийств нужно на заряд
        ReloadSpeed,      // amount = доля сокращения времени перезарядки за уровень
        Damage,           // +урон снаряда
    }

    [CreateAssetMenu(menuName = "CatTanks/Upgrade", fileName = "Upgrade_")]
    public class UpgradeDefinition : ScriptableObject
    {
        public string id = "upgrade";
        public string title = "Апгрейд";
        [TextArea] public string description;
        public Sprite icon;
        public UpgradeStat stat;
        public float amountPerLevel = 1f;
        public int maxLevel = 3;
        public int baseCost = 3;
        public int costPerLevel = 2;

        public int CostForLevel(int currentLevel) => baseCost + costPerLevel * currentLevel;

        public void Apply(ref PlayerStats s, int level)
        {
            float a = amountPerLevel * level;
            switch (stat)
            {
                case UpgradeStat.MaxHp: s.maxHp += Mathf.RoundToInt(a); break;
                case UpgradeStat.MoveSpeed: s.moveSpeed += a; break;
                case UpgradeStat.FireRate: s.fireCooldown *= Mathf.Pow(1f - amountPerLevel, level); break;
                case UpgradeStat.BulletSpeed: s.bulletSpeed += a; break;
                case UpgradeStat.Bounces: s.maxBounces += Mathf.RoundToInt(a); break;
                case UpgradeStat.MaxBullets:
                    s.maxBulletsAlive += Mathf.RoundToInt(a);
                    s.magazineSize += Mathf.RoundToInt(a);
                    break;
                case UpgradeStat.ReloadSpeed: s.reloadTime *= Mathf.Pow(1f - amountPerLevel, level); break;
                case UpgradeStat.UltCharge: s.ultKillsPerCharge -= Mathf.RoundToInt(a); break;
                case UpgradeStat.Damage: s.bulletDamage += Mathf.RoundToInt(a); break;
            }
        }

        static UpgradeDefinition Make(string id, string title, string desc, UpgradeStat stat, float amount, int maxLevel, int cost, int step)
        {
            var u = CreateInstance<UpgradeDefinition>();
            u.name = id; u.id = id; u.title = title; u.description = desc;
            u.stat = stat; u.amountPerLevel = amount; u.maxLevel = maxLevel; u.baseCost = cost; u.costPerLevel = step;
            return u;
        }

        public static List<UpgradeDefinition> CreateDefaults() => new List<UpgradeDefinition>
        {
            // цены растут с уровнем апгрейда; дохода за уровень (база + звёзды + лут + комбо) хватает на 1–2 покупки
            Make("hp", "Девять жизней", "+1 HP", UpgradeStat.MaxHp, 1, 3, 4, 3),
            Make("damage", "Крупный калибр", "+1 урон снаряда", UpgradeStat.Damage, 1, 2, 7, 5),
            Make("speed", "Быстрые лапы", "+0.4 к скорости", UpgradeStat.MoveSpeed, 0.4f, 3, 3, 2),
            Make("firerate", "Скорострел", "−12% пауза между выстрелами", UpgradeStat.FireRate, 0.12f, 3, 4, 3),
            Make("bounce", "Рикошетчик", "+1 отскок снаряда", UpgradeStat.Bounces, 1, 2, 5, 4),
            Make("ammo", "Запасливый", "+1 снаряд в магазине", UpgradeStat.MaxBullets, 1, 3, 3, 3),
            Make("reload", "Ловкие лапы", "−15% время перезарядки", UpgradeStat.ReloadSpeed, 0.15f, 3, 3, 3),
            Make("ult", "Орехолюб", "−1 убийство на заряд ореха", UpgradeStat.UltCharge, 1, 2, 5, 4),
        };
    }
}
