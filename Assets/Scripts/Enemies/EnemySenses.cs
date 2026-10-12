// «Органы чувств» ИИ: зрение и поиск летящих в тебя снарядов. Общие для всех типов врагов.
using UnityEngine;

namespace CatTanks
{
    public static class EnemySenses
    {
        /// <summary>Видно ли target из from (луч не упирается в стену/укрытие/другой объект раньше цели).</summary>
        public static bool CanSee(Vector2 from, Transform target, Transform self, float range = float.MaxValue)
        {
            if (target == null) return false;
            Vector2 d = (Vector2)target.position - from;
            if (d.sqrMagnitude > range * range) return false;
            return Ricochet.CastNearest(from, 0.05f, d.normalized, d.magnitude + 0.5f, self, out var hit)
                   && hit.collider.transform.IsChildOf(target);
        }

        /// <summary>
        /// Ищет снаряд, который в ближайшие lookAhead секунд пролетит ближе bodyRadius от pos (без учёта рикошетов).
        /// dodge — куда отпрыгнуть (перпендикулярно траектории, от линии полёта).
        /// </summary>
        public static bool FindIncomingBullet(Vector2 pos, float bodyRadius, float lookAhead, Team myTeam,
                                              out Bullet threat, out Vector2 dodge)
        {
            threat = null;
            dodge = Vector2.zero;
            float bestT = float.MaxValue;
            foreach (var b in Bullet.All)
            {
                if (b == null || (b.Team == myTeam && b.Bounces == 0)) continue; // свои до отскока не опасны
                Vector2 rel = pos - b.Position;
                float along = Vector2.Dot(rel, b.Direction);
                if (along < 0f || along > b.speed * lookAhead) continue;     // летит мимо/далеко
                Vector2 perp = rel - b.Direction * along;
                if (perp.magnitude > bodyRadius + b.radius) continue;
                float t = along / Mathf.Max(0.01f, b.speed);
                if (t >= bestT) continue;
                bestT = t;
                threat = b;
                dodge = perp.sqrMagnitude > 1e-4f ? perp.normalized : new Vector2(-b.Direction.y, b.Direction.x);
            }
            return threat != null;
        }
    }
}
