// Корень игрока: держит ссылки на модули и характеристики, переводит смерть в событие игры.
// Модули (движение, оружие, предпросмотр, ульта) — отдельные компоненты на том же объекте,
// их можно дорабатывать независимо друг от друга.
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Health))]
    public class PlayerTank : MonoBehaviour
    {
        public PlayerStats Stats { get; private set; } = PlayerStats.Default;
        public Health Health { get; private set; }
        public PlayerMovement Movement { get; private set; }
        public PlayerWeapon Weapon { get; private set; }
        public PlayerUltimate Ultimate { get; private set; }
        public TrajectoryPreview Preview { get; private set; }
        public TankModuleDefinition Hull { get; private set; }
        public TankModuleDefinition Cannon { get; private set; }

        /// <summary>Игрок может управлять танком: идёт игра и он жив.</summary>
        public bool InputEnabled => Game.InputReady && Health != null && !Health.IsDead;

        void Awake()
        {
            Health = GetComponent<Health>();
            Health.team = Team.Player;
            Health.destroyOnDeath = false;
            Health.invulnerableAfterHit = 0.9f;
            Health.blinkWhileInvulnerable = true;
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
            Game.Player = this;
        }

        void OnDestroy()
        {
            if (Game.Player == this) Game.Player = null;
        }

        /// <summary>Вызывается PlayerFactory после того, как все модули добавлены.</summary>
        public void Init(PlayerStats stats, GameConfig cfg, bool tankControls, TankModuleDefinition hull = null, TankModuleDefinition cannon = null)
        {
            Stats = stats;
            Hull = hull;
            Cannon = cannon;
            Movement = GetComponent<PlayerMovement>();
            Weapon = GetComponent<PlayerWeapon>();
            Ultimate = GetComponent<PlayerUltimate>();
            Preview = GetComponent<TrajectoryPreview>();

            Health.SetMaxHp(stats.maxHp, refill: true);
            if (Movement != null)
            {
                Movement.Apply(stats, tankControls);
                if (hull != null) Movement.acceleration = hull.acceleration;
            }
            if (Weapon != null)
            {
                Weapon.Apply(stats);
                if (cannon != null) Weapon.muzzleDistance = 0.1f + cannon.barrelLength + stats.bulletRadius * 0.5f; // снаряд вылетает из конца ствола
            }
            if (Ultimate != null) Ultimate.Configure(cfg.ultimate, stats.ultKillsPerCharge);
            if (Preview != null) Preview.maxLength = cfg.trajectoryPreviewLength;
        }

        void OnDamaged(Health h, DamageInfo info) => GameEvents.RaisePlayerDamaged(h, info);

        void OnDied(Health h, DamageInfo info)
        {
            gameObject.SetActive(false);
            GameEvents.RaisePlayerDied();
        }
    }
}
