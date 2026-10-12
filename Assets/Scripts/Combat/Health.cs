// Здоровье для игрока и врагов. Снаряд ищет IDamageable у того, во что попал.
// Health ничего не знает про игровой цикл: он только сообщает Damaged/Died,
// а что делать со смертью, решают PlayerTank и Enemy.
using System;
using UnityEngine;

namespace CatTanks
{
    /// <summary>Пометка «не мигать белым при уроне» — для теней, свечения, колец.</summary>
    public class NoFlash : MonoBehaviour { }

    public class Health : MonoBehaviour, IDamageable
    {
        public int maxHp = 1;
        public Team team = Team.Neutral;
        public float invulnerableAfterHit = 0.5f;
        public bool destroyOnDeath = true;
        public bool blinkWhileInvulnerable = false;   // мигать полупрозрачным, пока неуязвим после удара

        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;
        public Team Team => team;
        public DamageInfo LastDamage { get; private set; }

        public event Action<Health, DamageInfo> Damaged;
        public event Action<Health, DamageInfo> Died;
        public event Action<Health> Changed;          // любое изменение HP (урон, лечение, новый максимум)

        SpriteRenderer[] _renderers;
        Color[] _colors;
        float _flashUntil, _invulUntil;
        int _visual;   // 0 — обычный вид, 1 — белая вспышка, 2 — полупрозрачный (мигание)

        void Awake()
        {
            Hp = maxHp;
            CacheRenderers();
        }

        public void SetMaxHp(int value, bool refill)
        {
            maxHp = Mathf.Max(1, value);
            Hp = refill ? maxHp : Mathf.Min(Hp, maxHp);
            Changed?.Invoke(this);
        }

        /// <summary>Перечитать спрайты и их цвета (если добавил детей или перекрасил объект после создания).</summary>
        public void CacheRenderers()
        {
            var all = GetComponentsInChildren<SpriteRenderer>(true);
            var list = new System.Collections.Generic.List<SpriteRenderer>(all.Length);
            foreach (var r in all) if (r.GetComponent<NoFlash>() == null) list.Add(r);
            _renderers = list.ToArray();
            _colors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _colors[i] = _renderers[i].color;
        }

        /// <summary>Короткая запись для урона без подробностей.</summary>
        public bool Damage(int amount) => ApplyDamage(new DamageInfo(amount));

        public bool ApplyDamage(DamageInfo info)
        {
            if (IsDead || info.amount <= 0) return false;
            if (Time.time < _invulUntil && info.kind != DamageKind.Ultimate) return false;

            LastDamage = info;
            Hp -= info.amount;
            _invulUntil = Time.time + invulnerableAfterHit;
            _flashUntil = Time.time + 0.08f;
            SetVisual(1);
            Damaged?.Invoke(this, info);
            Changed?.Invoke(this);
            if (Hp <= 0) Die(info);
            return true;
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            Hp = Mathf.Min(maxHp, Hp + amount);
            Changed?.Invoke(this);
        }

        public void Kill(DamageInfo info)
        {
            if (IsDead) return;
            info.amount = Hp;
            info.kind = info.kind == DamageKind.Other ? DamageKind.Ultimate : info.kind;
            _invulUntil = 0;
            ApplyDamage(info);
        }

        public bool IsInvulnerable => Time.time < _invulUntil;

        void Update()
        {
            bool flash = Time.time < _flashUntil;
            bool blink = blinkWhileInvulnerable && !IsDead && IsInvulnerable && (int)(Time.time * 14f) % 2 == 0;
            SetVisual(flash ? 1 : blink ? 2 : 0);
        }

        void SetVisual(int v)
        {
            if (_visual == v) return;
            _visual = v;
            if (_renderers == null || _renderers.Length == 0) CacheRenderers();
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                var c = _colors[i];
                _renderers[i].color = v == 1 ? Color.white : v == 2 ? new Color(c.r, c.g, c.b, c.a * 0.3f) : c;
            }
        }

        void Die(DamageInfo info)
        {
            SetVisual(0);
            Died?.Invoke(this, info);
            if (destroyOnDeath) Destroy(gameObject);
        }
    }
}
