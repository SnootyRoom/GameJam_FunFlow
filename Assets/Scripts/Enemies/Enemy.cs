// Ядро любого врага: «паспорт» (EnemyDefinition), контекст уровня, здоровье, регистрация, смерть.
// Поведение НЕ здесь — оно в компонентах-наследниках EnemyBehaviour на том же объекте
// (Chase, Turret, ContactDamage, ...). Враг = Enemy + Health + набор поведений.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public class Enemy : MonoBehaviour
    {
        public EnemyDefinition Definition { get; private set; }
        public EnemyContext Context { get; private set; }
        public Health Health { get; private set; }

        public EnemyType Type => Definition != null ? Definition.type : default;
        public bool IsAlive => Health != null && !Health.IsDead;
        public bool IsInitialized => Context != null;

        /// <summary>Элитный враг: больше HP, быстрее, чаще стреляет. Даёт рыбку и двойной заряд ореха.</summary>
        public bool IsElite { get; private set; }
        public bool IsBoss => Definition != null && Definition.isBoss;
        /// <summary>Множители для поведений: элита + небольшой разброс у каждого экземпляра.</summary>
        public float SpeedMultiplier { get; private set; } = 1f;
        public float FireIntervalMultiplier { get; private set; } = 1f;

        public const float EliteHp = 1.8f, EliteSpeed = 1.15f, EliteFireInterval = 0.7f, EliteSize = 1.15f;

        void Awake()
        {
            Health = GetComponent<Health>();
            Health.team = Team.Enemy;
            Health.Died += OnDied;
            Health.Damaged += OnDamaged;
        }

        void OnEnable() { EnemyRegistry.Add(this); }
        void OnDisable() { EnemyRegistry.Remove(this); }

        /// <summary>Вызывает EnemyFactory сразу после создания. После этого поведения получают OnSpawned().</summary>
        public void Init(EnemyDefinition def, EnemyContext ctx, bool elite = false)
        {
            Definition = def;
            Context = ctx;
            IsElite = elite;
            SpeedMultiplier = Random.Range(0.92f, 1.08f) * (elite ? EliteSpeed : 1f);
            FireIntervalMultiplier = Random.Range(0.9f, 1.1f) * (elite ? EliteFireInterval : 1f);
            int hp = def.HpAtLevel(ctx != null ? ctx.LevelNumber : 1);
            Health.SetMaxHp(elite ? Mathf.CeilToInt(hp * EliteHp) : hp, refill: true);
            Health.invulnerableAfterHit = def.invulnerableAfterHit;
            Health.CacheRenderers();
            foreach (var b in GetComponents<EnemyBehaviour>()) b.Bind(this);
            GameEvents.RaiseEnemySpawned(this);
        }

        /// <summary>Сообщить, что враг заметил игрока: над ним появится «!» и прозвучит сигнал. Зови из поведений.</summary>
        public void Alert() => GameEvents.RaiseEnemyAlerted(this);

        void OnDamaged(Health h, DamageInfo info)
        {
            if (!h.IsDead) GameEvents.RaiseEnemyDamaged(this, info);
        }

        void OnDied(Health h, DamageInfo info)
        {
            foreach (var b in GetComponents<EnemyBehaviour>()) b.NotifyDeath(info);
            GameEvents.RaiseEnemyKilled(this, info);
        }
    }
}
