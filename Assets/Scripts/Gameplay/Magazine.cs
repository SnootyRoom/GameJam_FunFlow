// Магазин оружия: N выстрелов подряд, на пустом — перезарядка; если отпустить спуск с неполным магазином,
// через idleReload секунд он перезарядится сам. Чистая логика без MonoBehaviour — пригодится и врагам, и тестам.
using System;
using UnityEngine;

namespace CatTanks
{
    public class Magazine
    {
        public int Size { get; private set; }
        public int Rounds { get; private set; }
        public bool Reloading => _reloadEnd > 0f;
        /// <summary>Прогресс перезарядки 0…1 (1 — не перезаряжается).</summary>
        public float Reload01 => !Reloading || _reloadTime <= 0f ? 1f : Mathf.Clamp01(1f - (_reloadEnd - Now) / _reloadTime);
        public bool CanShoot => Rounds > 0 && !Reloading;

        public event Action ReloadStarted, Reloaded;

        float _reloadTime, _idleReload, _reloadEnd, _lastShot;
        readonly Func<float> _clock;
        float Now => _clock();

        /// <summary>clock — источник времени (по умолчанию Time.time — игровое, пауза его останавливает).</summary>
        public Magazine(int size, float reloadTime, float idleReload, Func<float> clock = null)
        {
            _clock = clock ?? (() => Time.time);
            Configure(size, reloadTime, idleReload);
            Rounds = Size;
        }

        /// <summary>Поменять параметры на лету (например, из инспектора).</summary>
        public void Configure(int size, float reloadTime, float idleReload)
        {
            Size = Mathf.Max(1, size);
            _reloadTime = Mathf.Max(0.01f, reloadTime);
            _idleReload = idleReload;
            if (Rounds > Size) Rounds = Size;
        }

        /// <summary>Каждый кадр. triggerHeld — держат ли спуск (пока держат, дозарядка не начинается).</summary>
        public void Tick(bool triggerHeld)
        {
            if (Reloading)
            {
                if (Now >= _reloadEnd) { _reloadEnd = 0f; Rounds = Size; Reloaded?.Invoke(); }
                return;
            }
            if (Rounds < Size && !triggerHeld && Now - _lastShot >= _idleReload) StartReload();
        }

        /// <summary>Потратить патрон. false — нечем стрелять.</summary>
        public bool TryShoot()
        {
            if (!CanShoot) return false;
            _lastShot = Now;
            if (--Rounds <= 0) StartReload();
            return true;
        }

        void StartReload()
        {
            _reloadEnd = Now + _reloadTime;
            ReloadStarted?.Invoke();
        }
    }
}
