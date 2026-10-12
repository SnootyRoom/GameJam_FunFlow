// Опасные зоны, о которых знают заранее: сейчас это тень падающего ореха.
// ИИ может спросить «я в опасности?» и убежать — поэтому ульта не убивает мгновенно и честно.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public static class Hazards
    {
        public struct Zone
        {
            public Vector2 center;
            public float radius;
            public float impactTime;   // Time.time момента удара
            public object owner;
        }

        static readonly List<Zone> Active = new List<Zone>();
        public static IReadOnlyList<Zone> All => Active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active.Clear();

        public static void Add(object owner, Vector2 center, float radius, float impactTime)
            => Active.Add(new Zone { owner = owner, center = center, radius = radius, impactTime = impactTime });

        public static void Remove(object owner) => Active.RemoveAll(z => z.owner == owner);

        /// <summary>
        /// Точка в опасной зоне (с запасом margin)? escape — куда бежать (от центра), timeLeft — сколько секунд до удара.
        /// </summary>
        public static bool IsInDanger(Vector2 p, float margin, out Vector2 escape, out float timeLeft)
        {
            escape = Vector2.zero;
            timeLeft = float.MaxValue;
            bool found = false;
            foreach (var z in Active)
            {
                Vector2 d = p - z.center;
                float r = z.radius + margin;
                if (d.sqrMagnitude > r * r) continue;
                float t = z.impactTime - Time.time;
                if (t >= timeLeft) continue;
                timeLeft = t;
                escape = d.sqrMagnitude > 1e-4f ? d.normalized : Random.insideUnitCircle.normalized;
                found = true;
            }
            return found;
        }
    }
}
