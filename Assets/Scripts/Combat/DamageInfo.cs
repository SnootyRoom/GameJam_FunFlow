// Всё, что нужно знать об уроне: сколько, от кого, какой командой и каким способом.
// По sourceTeam ульта понимает, что убийство засчитать игроку (даже если снаряд срикошетил).
using UnityEngine;

namespace CatTanks
{
    public enum Team
    {
        Neutral,
        Player,
        Enemy,
    }

    public enum DamageKind
    {
        Bullet,
        Contact,    // укус / таран
        Ultimate,
        Explosion,
        Other,
    }

    public struct DamageInfo
    {
        public int amount;
        public GameObject source;   // кто нанёс (может быть уже уничтожен к моменту обработки)
        public Team sourceTeam;
        public DamageKind kind;
        public Vector2 point;
        public Vector2 direction;

        public DamageInfo(int amount, GameObject source = null, Team sourceTeam = Team.Neutral,
                          DamageKind kind = DamageKind.Other, Vector2 point = default, Vector2 direction = default)
        {
            this.amount = amount;
            this.source = source;
            this.sourceTeam = sourceTeam;
            this.kind = kind;
            this.point = point;
            this.direction = direction;
        }

        public bool ByPlayer => sourceTeam == Team.Player;
    }

    /// <summary>Всё, что можно ранить. Сейчас это только Health, но так можно добавить, например, разрушаемые укрытия.</summary>
    public interface IDamageable
    {
        Team Team { get; }
        bool IsDead { get; }
        bool ApplyDamage(DamageInfo info);
    }
}
