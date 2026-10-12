// Здоровье для игрока и врагов. Снаряд ищет этот компонент у того, во что попал.
using System;
using UnityEngine;

namespace CatTanks
{
    public class Health : MonoBehaviour
    {
        public int maxHp = 1;
        public bool isPlayer;
        public float invulnerableAfterHit = 0.5f;

        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;

        public event Action<int> OnDamaged;  // сколько осталось
        public event Action OnDeath;

        SpriteRenderer[] _renderers;
        Color[] _colors;
        float _flashUntil, _invulUntil;

        void Awake()
        {
            Hp = maxHp;
            CacheRenderers();
        }

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
            OnDamaged?.Invoke(Hp);
            if (Hp <= 0) Die();
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
            OnDeath?.Invoke();
            if (isPlayer)
            {
                gameObject.SetActive(false);
                if (LevelBuilder.Instance != null) LevelBuilder.Instance.OnPlayerDied();
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
