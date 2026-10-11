// Здоровье для игрока и врагов. Снаряд ищет этот компонент у того, во что попал.
using System;
using UnityEngine;

namespace CatTanks
{
    public enum Team { Player, Enemy }

    public class Health : MonoBehaviour
    {
        public int maxHp = 1;
        public Team team = Team.Enemy;
        public float invulnerableAfterHit = 0.5f;

        /// <summary>Ранят ли снаряды своих. Задаётся в GameFlow (галочка friendlyFire).</summary>
        public static bool FriendlyFire = true;

        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;
        public bool IsPlayer => team == Team.Player;

        public event Action<int, int> OnHpChanged;  // (сейчас, максимум) — и урон, и лечение
        public event Action OnDeath;

        SpriteRenderer[] _renderers;
        Color[] _colors;
        float _flashUntil, _invulUntil;

        void Awake()
        {
            Hp = maxHp;
            CacheRenderers();
        }

        /// <summary>Может ли снаряд команды attacker ранить target.</summary>
        public static bool CanHurt(Team attacker, Health target) => FriendlyFire || attacker != target.team;

        /// <summary>Вызови, если поменял maxHp после создания объекта.</summary>
        public void ResetHp() { Hp = maxHp; }

        /// <summary>Перечитать спрайты и их цвета (если добавил детей или перекрасил объект после создания).</summary>
        public void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>();
            _colors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _colors[i] = _renderers[i].color;
        }

        public void Damage(int amount)
        {
            if (IsDead || Time.time < _invulUntil) return;
            Hp -= amount;
            _invulUntil = Time.time + invulnerableAfterHit;
            _flashUntil = Time.time + 0.08f;
            SetFlash(true);
            OnHpChanged?.Invoke(Hp, maxHp);
            if (Hp <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (IsDead || Hp >= maxHp) return;
            Hp = Mathf.Min(maxHp, Hp + amount);
            OnHpChanged?.Invoke(Hp, maxHp);
        }

        void Update()
        {
            if (_flashUntil > 0 && Time.time >= _flashUntil)
            {
                _flashUntil = 0;
                SetFlash(false);
            }
        }

        void SetFlash(bool on)
        {
            if (_renderers == null || _renderers.Length == 0) CacheRenderers();
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].color = on ? Color.white : _colors[i];
        }

        void Die()
        {
            OnDeath?.Invoke(); // GameFlow слушает игрока, LevelContext — врагов
            if (IsPlayer) gameObject.SetActive(false);
            else Destroy(gameObject);
        }
    }
}
