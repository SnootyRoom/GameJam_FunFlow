// Единое управление Time.timeScale (из ветки shishki). Несколько систем хотят замедлять время
// (замедление при прицеливании ульты, стоп-кадр на экране победы) — каждая ставит свой запрос,
// применяется самый медленный. Пауза важнее всех: на паузе время стоит.
// Никто, кроме GameTime, не должен писать в Time.timeScale напрямую — иначе системы перебивают друг друга.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public static class GameTime
    {
        static readonly Dictionary<object, float> Requests = new Dictionary<object, float>();
        static bool _paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Requests.Clear(); _paused = false; }

        /// <summary>Пауза (ставит GameFlow). Пока true — время стоит независимо от запросов.</summary>
        public static bool Paused
        {
            get => _paused;
            set { _paused = value; Apply(); }
        }

        /// <summary>Попросить замедлить время до scale (0..1). owner — кто просит (обычно this).</summary>
        public static void Set(object owner, float scale)
        {
            Requests[owner] = Mathf.Clamp01(scale);
            Apply();
        }

        public static void Clear(object owner)
        {
            if (Requests.Remove(owner)) Apply();
        }

        /// <summary>Сбросить всё (смена уровня, выход в меню).</summary>
        public static void ClearAll()
        {
            Requests.Clear();
            _paused = false;
            Apply();
        }

        static void Apply()
        {
            if (_paused) { Time.timeScale = 0f; return; }
            float s = 1f;
            foreach (var kv in Requests) s = Mathf.Min(s, kv.Value);
            Time.timeScale = s;
        }
    }
}
