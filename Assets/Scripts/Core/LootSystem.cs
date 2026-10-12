// Что выпадает из врагов. Рыбки — по шансу из EnemyDefinition (элита — всегда и больше, босс — россыпь),
// сердечко — редко, но чаще, когда коту плохо (поддерживает темп: рискни и подбери).
using UnityEngine;

namespace CatTanks
{
    public class LootSystem : MonoBehaviour
    {
        [Range(0f, 1f)] public float heartChance = 0.05f;
        [Range(0f, 1f)] public float heartChanceLowHp = 0.16f;   // когда у кота ≤ 2 HP

        void OnEnable() { GameEvents.EnemyKilled += OnEnemyKilled; }
        void OnDisable() { GameEvents.EnemyKilled -= OnEnemyKilled; }

        void OnEnemyKilled(Enemy e, DamageInfo info)
        {
            if (e == null || e.Definition == null || !info.ByPlayer) return;
            var def = e.Definition;
            Vector2 pos = e.transform.position;

            int fish = 0;
            if (def.isBoss) fish = def.fishDrop;
            else if (e.IsElite) fish = def.fishDrop + 1;
            else if (Random.value < def.fishDropChance) fish = def.fishDrop;
            for (int i = 0; i < fish; i++)
                Pickup.Spawn(PickupType.Fish, pos, 1, Random.insideUnitCircle.normalized * Random.Range(2f, 4.5f));

            var p = Game.Player;
            if (p != null && p.Health.Hp < p.Health.maxHp)
            {
                float chance = p.Health.Hp <= 2 ? heartChanceLowHp : heartChance;
                if (def.isBoss || Random.value < chance)
                    Pickup.Spawn(PickupType.Heart, pos, 1, Random.insideUnitCircle.normalized * 2f);
            }
        }
    }
}
