// Все живые враги. Нужен ульте (кого убить), HUD (сколько осталось), ExitZone (зачищен ли уровень)
// и самим врагам (держаться стаей, не толпиться).
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public static class EnemyRegistry
    {
        static readonly List<Enemy> Alive = new List<Enemy>();

        public static IReadOnlyList<Enemy> All => Alive;
        public static int Count => Alive.Count;

        internal static void Add(Enemy e) { if (!Alive.Contains(e)) Alive.Add(e); }
        internal static void Remove(Enemy e) { Alive.Remove(e); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Alive.Clear();

        /// <summary>Живой босс уровня (или null).</summary>
        public static Enemy Boss
        {
            get
            {
                foreach (var e in Alive) if (e.IsAlive && e.IsBoss) return e;
                return null;
            }
        }

        public static Enemy Nearest(Vector2 pos, float maxDistance = float.MaxValue, Enemy except = null)
        {
            Enemy best = null;
            float bestSq = maxDistance * maxDistance;
            foreach (var e in Alive)
            {
                if (e == except || !e.IsAlive) continue;
                float sq = ((Vector2)e.transform.position - pos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = e; }
            }
            return best;
        }

        /// <summary>Заполняет results живыми врагами в радиусе (список очищается).</summary>
        public static void InRadius(Vector2 pos, float radius, List<Enemy> results)
        {
            results.Clear();
            float r2 = radius * radius;
            foreach (var e in Alive)
                if (e.IsAlive && ((Vector2)e.transform.position - pos).sqrMagnitude <= r2) results.Add(e);
        }
    }
}
