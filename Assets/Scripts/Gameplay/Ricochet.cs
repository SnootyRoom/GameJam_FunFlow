// Общая логика рикошета: и снаряд, и ИИ используют одни и те же касты,
// поэтому то, что «видит» ИИ при прицеливании, совпадает с реальным полётом снаряда.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public static class Ricochet
    {
        static readonly RaycastHit2D[] Buffer = new RaycastHit2D[16];
        static readonly ContactFilter2D AllContacts = new ContactFilter2D().NoFilter();
        public const float Skin = 0.01f;

        /// <summary>
        /// Ближайшее попадание круга радиуса radius по направлению dir.
        /// Пропускает триггеры и (если ignore задан) коллайдеры объекта ignore и его детей.
        /// </summary>
        public static bool CastNearest(Vector2 origin, float radius, Vector2 dir, float distance,
                                       Transform ignore, out RaycastHit2D best)
        {
            best = default(RaycastHit2D);
            int n = Physics2D.CircleCast(origin, radius, dir, AllContacts, Buffer, distance);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = Buffer[i];
                if (h.collider == null || h.collider.isTrigger) continue;
                if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
                if (h.distance < bestDist) { bestDist = h.distance; best = h; found = true; }
            }
            return found;
        }

        /// <summary>
        /// Симулирует выстрел. true — если снаряд долетит до target (с учётом отскоков).
        /// Попадание в любой другой объект со здоровьем считается промахом (ИИ не стреляет по своим).
        /// points (если передан) заполняется точками траектории — удобно для отладки.
        /// </summary>
        public static bool Simulate(Vector2 origin, Vector2 dir, float radius, int maxBounces, float maxDistance,
                                    Transform shooter, Transform target, List<Vector2> points = null)
        {
            if (points != null) { points.Clear(); points.Add(origin); }
            Vector2 pos = origin;
            dir = dir.normalized;
            float left = maxDistance;

            for (int bounce = 0; bounce <= maxBounces && left > 0f; bounce++)
            {
                // до первого отскока стрелок «прозрачен» для своего снаряда — как и в Bullet
                if (!CastNearest(pos, radius, dir, left, bounce == 0 ? shooter : null, out var hit))
                {
                    if (points != null) points.Add(pos + dir * left);
                    return false;
                }

                if (points != null) points.Add(hit.centroid);
                if (target != null && hit.collider.transform.IsChildOf(target)) return true;
                if (hit.collider.GetComponentInParent<Health>() != null) return false; // свой или стрелок сам

                left -= hit.distance;
                pos = hit.centroid + hit.normal * Skin;
                dir = Vector2.Reflect(dir, hit.normal).normalized;
            }
            return false;
        }
    }
}
