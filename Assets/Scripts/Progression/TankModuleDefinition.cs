// Модуль танка для гаража: корпус (броня, скорость, размер) или пушка (урон, темп, магазин, калибр).
// Модуль задаёт характеристики своего слота целиком, апгрейды из магазина накладываются сверху.
// Create → CatTanks → Tank Module, добавить в GameConfig. Пусто = CreateDefaults().
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public enum ModuleSlot { Hull, Cannon }

    [CreateAssetMenu(menuName = "CatTanks/Tank Module", fileName = "Module_")]
    public class TankModuleDefinition : ScriptableObject
    {
        public string id = "module";
        public ModuleSlot slot;
        public string displayName = "Модуль";
        [TextArea] public string description;
        public int unlockAtLevel = 1;

        [Header("Корпус")]
        public int maxHp = 3;
        public float moveSpeed = 3.5f;
        public float turnSpeed = 180f;
        public float hullScale = 0.8f;          // размер корпуса в клетках (коллайдер тоже)
        public float acceleration = 22f;

        [Header("Пушка")]
        public int damage = 2;
        public float fireCooldown = 0.3f;
        public float bulletSpeed = 8f;
        public int maxBounces = 1;
        public int magazineSize = 5;
        public float reloadTime = 1.6f;
        public float bulletRadius = 0.12f;
        public float barrelLength = 0.5f;
        public float barrelWidth = 0.14f;
        public float shotPitch = 1f;            // ниже — басовитее выстрел
        public float recoil = 1f;               // множитель отдачи/тряски
        public float spreadDegrees = 0f;        // разброс (скорострельные пушки неточны)
        public float splashRadius = 0f;         // фугас: урон по площади при попадании/разрыве
        public int splashDamage = 0;

        public void ApplyTo(ref PlayerStats s)
        {
            if (slot == ModuleSlot.Hull)
            {
                s.maxHp = maxHp;
                s.moveSpeed = moveSpeed;
                s.turnSpeed = turnSpeed;
            }
            else
            {
                s.bulletDamage = damage;
                s.fireCooldown = fireCooldown;
                s.bulletSpeed = bulletSpeed;
                s.maxBounces = maxBounces;
                s.magazineSize = magazineSize;
                s.maxBulletsAlive = magazineSize;
                s.reloadTime = reloadTime;
                s.bulletRadius = bulletRadius;
                s.spreadDegrees = spreadDegrees;
                s.splashRadius = splashRadius;
                s.splashDamage = splashDamage;
            }
        }

        // ------------------------------------------------------------------ Значения по умолчанию

        static TankModuleDefinition Hull(string id, string name, string desc, int unlock, int hp, float speed, float turn, float scale, float accel)
        {
            var m = CreateInstance<TankModuleDefinition>();
            m.name = id; m.id = id; m.slot = ModuleSlot.Hull; m.displayName = name; m.description = desc; m.unlockAtLevel = unlock;
            m.maxHp = hp; m.moveSpeed = speed; m.turnSpeed = turn; m.hullScale = scale; m.acceleration = accel;
            return m;
        }

        static TankModuleDefinition Cannon(string id, string name, string desc, int unlock, int dmg, float cooldown, float speed,
                                           int bounces, int mag, float reload, float radius, float len, float width, float pitch, float recoil)
        {
            var m = CreateInstance<TankModuleDefinition>();
            m.name = id; m.id = id; m.slot = ModuleSlot.Cannon; m.displayName = name; m.description = desc; m.unlockAtLevel = unlock;
            m.damage = dmg; m.fireCooldown = cooldown; m.bulletSpeed = speed; m.maxBounces = bounces; m.magazineSize = mag;
            m.reloadTime = reload; m.bulletRadius = radius; m.barrelLength = len; m.barrelWidth = width; m.shotPitch = pitch; m.recoil = recoil;
            return m;
        }

        // Баланс (средне-повышенная сложность): у всех пушек устойчивый DPS ≈ 3.3–3.6, разница — в роли.
        // Корпуса: «HP × скорость» примерно одинаково, Разведчик — меньше цель, Бегемот — толкает врагов.
        public static List<TankModuleDefinition> CreateDefaultHulls() => new List<TankModuleDefinition>
        {
            Hull("hull_standard", "Стандарт", "Сбалансированный корпус", 1, 4, 3.6f, 200f, 0.8f, 24f),
            Hull("hull_scout", "Разведчик", "Быстрый и маленький, держит 3 удара", 1, 3, 4.8f, 280f, 0.68f, 34f),
            Hull("hull_assault", "Штурмовик", "5 HP при приличной скорости", 2, 5, 3.15f, 170f, 0.86f, 18f),
            Hull("hull_heavy", "Бегемот", "7 HP, расталкивает врагов, но медленный", 3, 7, 2.5f, 120f, 0.95f, 12f),
        };

        public static List<TankModuleDefinition> CreateDefaultCannons()
        {
            var list = new List<TankModuleDefinition>
            {
                Cannon("gun_standard", "Пушка", "Надёжная классика: 2 урона", 1, 2, 0.3f, 8.5f, 1, 5, 1.5f, 0.12f, 0.5f, 0.14f, 1f, 1f),
                Cannon("gun_rapid", "Мурчалка", "Шквал слабых снарядов, неточная", 1, 1, 0.11f, 11f, 1, 12, 2f, 0.08f, 0.62f, 0.09f, 1.35f, 0.45f),
                Cannon("gun_bouncer", "Попрыгунья", "3 отскока — бей из-за углов", 2, 2, 0.32f, 9f, 3, 5, 1.6f, 0.11f, 0.45f, 0.16f, 1.1f, 0.9f),
                Cannon("gun_sniper", "Дальнобой", "4 урона, быстрые снаряды", 3, 4, 0.65f, 16f, 1, 4, 2.1f, 0.1f, 0.78f, 0.1f, 0.9f, 1.2f),
                Cannon("gun_heavy", "Мяуглот", "Фугас: 6 урона + 2 по площади", 4, 6, 0.9f, 7f, 0, 3, 2.5f, 0.2f, 0.42f, 0.26f, 0.7f, 2f),
            };
            list[1].spreadDegrees = 4.5f;
            list[4].splashRadius = 1.25f; list[4].splashDamage = 2;
            return list;
        }
    }
}
