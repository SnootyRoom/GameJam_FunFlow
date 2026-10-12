// Единое управление Time.timeScale. Несколько систем хотят замедлять время (хит-стоп, замедление
// при прицеливании ульты, драматичная смерть) — каждая ставит свой запрос, применяется самый медленный.
// Пауза важнее всех: на паузе время стоит.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public static class GameTime
    {
        static readonly Dictionary<object, float> Requests = new Dictionary<object, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Requests.Clear();

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

        public static void ClearAll()
        {
            Requests.Clear();
            Apply();
        }

        public static void Apply()
        {
            if (Game.State == GameState.Paused) { Time.timeScale = 0f; return; }
            float s = 1f;
            foreach (var kv in Requests) s = Mathf.Min(s, kv.Value);
            Time.timeScale = s;
        }
    }
}
