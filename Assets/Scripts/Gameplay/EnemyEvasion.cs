// Уклонение от снарядов кота: враг видит летящий в него снаряд и отскакивает вбок.
// Честно, чтобы было что противопоставить:
//   • снаряд в упор (меньше reactionTime до попадания) не заметить — он попадёт;
//   • после отскока — передышка dodgeCooldown, очередь пулемёта всё равно достанет;
//   • рикошет заметить сложнее (ricochetFactor) — хитрый выстрел от стены лучший способ достать умного врага;
//   • пока враг атакует (замах, рывок, зарядка выстрела), он не уворачивается — это окно для удара.
// Решение «уклоняться или нет» принимается один раз на каждый отрезок полёта снаряда. Двигает тело само,
// EnemyCrawler / ShooterTactics на время отскока своё движение пропускают (смотрят на Dodging).
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public class EnemyEvasion : MonoBehaviour, ILevelActor
    {
        [Tooltip("Шанс увернуться от снаряда, летящего прямо в цель")]
        [Range(0f, 1f)] public float dodgeChance = 0.35f;
        [Tooltip("× к шансу, если снаряд уже отскочил от стены (рикошет заметить сложнее)")]
        [Range(0f, 1f)] public float ricochetFactor = 0.4f;
        [Tooltip("Снаряд ближе этого по времени не заметить, сек")]
        public float reactionTime = 0.12f;
        [Tooltip("Снаряд дальше этого по времени ещё не опасен, сек")]
        public float lookahead = 0.7f;
        [Tooltip("Пауза между отскоками, сек")]
        public float dodgeCooldown = 1f;
        public float dodgeSpeed = 7f;
        public float dodgeTime = 0.18f;

        /// <summary>Враг увернулся (точка) — для звука.</summary>
        public static event System.Action<Vector2> Dodged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Dodged = null; }

        /// <summary>Сейчас отскакивает — остальное движение врага ждёт.</summary>
        public bool Dodging => Time.time < _dodgeEnd;

        LevelContext _ctx;
        Rigidbody2D _rb;
        EnemyCrawler _crawler;
        EnemyShooter _shooter;
        float _radius = 0.4f;
        float _dodgeEnd, _nextDodge;
        Vector2 _dodgeDir;
        readonly HashSet<(Bullet, int)> _judged = new HashSet<(Bullet, int)>();   // отрезки полёта, по которым уже решили (снаряд + номер отскока)

        public void Init(LevelContext ctx)
        {
            _ctx = ctx;
            _rb = GetComponent<Rigidbody2D>();
            _crawler = GetComponent<EnemyCrawler>();
            _shooter = GetComponent<EnemyShooter>();
            var col = GetComponent<Collider2D>();
            if (col != null) _radius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y);
        }

        void Update()
        {
            if (_ctx == null || _rb == null || !_ctx.PlayerAlive || dodgeChance <= 0f) return;
            if (Dodging || Time.time < _nextDodge) return;
            if ((_crawler != null && _crawler.Busy) || (_shooter != null && _shooter.Busy)) return;
            if (!_ctx.IsOnScreen(transform.position)) return; // за кадром кот не целится — и уворачиваться не от чего

            if (_judged.Count > 256) _judged.Clear();
            Vector2 me = transform.position;
            var bullets = Bullet.Active;
            for (int i = 0; i < bullets.Count; i++)
            {
                var b = bullets[i];
                if (b == null || b.Team != Team.Player) continue;
                if (!OnCourse(b, me, out float time, out Vector2 closest)) continue;

                var key = (b, b.Bounces);
                if (_judged.Contains(key)) continue;   // по этому отрезку уже решили
                if (WallInBetween(b, me)) continue;    // отскочит раньше — решим по следующему отрезку
                _judged.Add(key);
                if (time < reactionTime) continue;     // в упор — не успеть
                float chance = dodgeChance * (b.Bounces > 0 ? ricochetFactor : 1f);
                if (Random.value >= chance) continue;
                if (TryDodge(me, b.Direction, closest)) return;
            }
        }

        /// <summary>Летит ли снаряд в нас (или впритирку) по нынешнему отрезку и скоро ли долетит. Только геометрия, без физики.</summary>
        bool OnCourse(Bullet b, Vector2 me, out float time, out Vector2 closest)
        {
            Vector2 p = b.transform.position, d = b.Direction;
            float along = Vector2.Dot(me - p, d);
            closest = p + d * along;
            time = along / Mathf.Max(0.1f, b.speed);
            if (along <= 0f || time > lookahead) return false;
            return Vector2.Distance(closest, me) <= _radius + b.radius + 0.15f;
        }

        /// <summary>Упрётся ли снаряд во что-то другое (стену, укрытие, другого врага) раньше, чем долетит до нас.</summary>
        bool WallInBetween(Bullet b, Vector2 me)
        {
            Vector2 p = b.transform.position;
            float along = Vector2.Dot(me - p, b.Direction);
            Transform ignore = b.Bounces == 0 && b.Owner != null ? b.Owner.transform : null; // как и сам снаряд: до отскока стрелок «прозрачен»
            return Ricochet.CastNearest(p, b.radius, b.Direction, along, ignore, out var hit)
                   && !hit.collider.transform.IsChildOf(transform)
                   && hit.distance < along - _radius;
        }

        /// <summary>Отскок поперёк снаряда — в ту сторону, где мы и так ближе к краю его пути и где нет стены.</summary>
        bool TryDodge(Vector2 me, Vector2 bulletDir, Vector2 closest)
        {
            Vector2 side = new Vector2(-bulletDir.y, bulletDir.x);
            float pref = Vector2.Dot(me - closest, side);
            if (Mathf.Abs(pref) < 0.05f) pref = Random.value < 0.5f ? -1f : 1f;
            float dist = dodgeSpeed * dodgeTime;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Vector2 dir = side * (pref >= 0f ? 1f : -1f) * (attempt == 0 ? 1f : -1f);
                if (Ricochet.CastNearest(me, _radius * 0.9f, dir, dist, transform, out _)) continue; // упрёмся в стену
                _dodgeDir = dir;
                _dodgeEnd = Time.time + dodgeTime;
                _nextDodge = _dodgeEnd + dodgeCooldown * Random.Range(0.8f, 1.2f);
                Dodged?.Invoke(me);
                return true;
            }
            return false;
        }

        void FixedUpdate()
        {
            if (!Dodging || _rb == null) return;
            _rb.MovePosition(_rb.position + _dodgeDir * (dodgeSpeed * Time.fixedDeltaTime));
        }
    }
}
