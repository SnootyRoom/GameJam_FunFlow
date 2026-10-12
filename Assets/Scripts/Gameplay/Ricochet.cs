// Общая логика рикошета: и снаряд, и ИИ используют одни и те же касты,
// поэтому то, что «видит» ИИ при прицеливании, совпадает с реальным полётом снаряда.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public static class Ricochet
    {
        static readonly RaycastHit2D[] Buffer = new RaycastHit2D[16];
        static readonly Collider2D[] OverlapBuffer = new Collider2D[16];
        static readonly ContactFilter2D AllContacts = ContactFilter2D.noFilter;
        public const float Skin = 0.01f;

        /// <summary>
        /// Снаряд появился уже внутри чего-то твёрдого (танк вплотную к стене — ствол в стене).
        /// Без этой проверки каст «изнутри» даёт отскок назад, и снаряд летит в своего же стрелка.
        /// Возвращает ближайший к центру коллайдер (не триггер, не ignore) или null.
        /// </summary>
        public static Collider2D SpawnBlocker(Vector2 origin, float radius, Transform ignore)
        {
            int n = Physics2D.OverlapCircle(origin, radius, AllContacts, OverlapBuffer);
            Collider2D best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = OverlapBuffer[i];
                if (c == null || c.isTrigger) continue;
                if (ignore != null && c.transform.IsChildOf(ignore)) continue;
                float d = Vector2.Distance(c.ClosestPoint(origin), origin);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

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

            // выстрел в упор: как и настоящий снаряд, разбивается о то, во что уже упёрся ствол
            var blocker = SpawnBlocker(origin, radius, shooter);
            if (blocker != null) return target != null && blocker.transform.IsChildOf(target);

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
