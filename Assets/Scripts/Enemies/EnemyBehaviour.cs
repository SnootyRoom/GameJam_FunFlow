// База для поведений врага. Наследуйся, переопредели OnSpawned() и пиши логику в Update/FixedUpdate,
// начиная с `if (!Active) return;` — так враг замрёт на паузе, после смерти игрока и до инициализации.
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Enemy))]
    public abstract class EnemyBehaviour : MonoBehaviour
    {
        protected Enemy Enemy { get; private set; }
        protected EnemyContext Ctx => Enemy != null ? Enemy.Context : null;
        protected EnemyDefinition Def => Enemy != null ? Enemy.Definition : null;
        protected AiParams Ai => Ctx != null ? Ctx.Ai : AiParams.Normal;

        /// <summary>Враг инициализирован, жив, игра идёт и игрок жив.</summary>
        protected bool Active => Ctx != null && Enemy.IsAlive && Game.IsPlaying && Ctx.PlayerAlive;

        protected Vector2 Position => transform.position;
        protected Vector2 PlayerPosition => Ctx.PlayerPosition;
        protected float DistanceToPlayer => Vector2.Distance(Position, Ctx.PlayerPosition);

        internal void Bind(Enemy e)
        {
            Enemy = e;
            OnSpawned();
        }

        internal void NotifyDeath(DamageInfo info) => OnDeath(info);

        /// <summary>Def, Ctx и Ai уже доступны. Здесь читай характеристики и подписывайся на события.</summary>
        protected virtual void OnSpawned() { }

        /// <summary>Враг умер (объект уничтожится в конце кадра). Место для эффектов, лута и т.п.</summary>
        protected virtual void OnDeath(DamageInfo info) { }
    }
}
