// Ползун (клещ): замечает игрока, бежит к нему по A* через сетку уровня и нападает рывком.
// Атака честная, как выстрел стрелка: подбежав, клещ замирает и мигает (замах), потом делает рывок
// в ту сторону, где кот был в начале замаха, — успел отъехать, рывок мимо. Урон — только во время рывка.
// Всё, что ему нужно, приходит в Init — синглтонов нет, можно тестировать в пустой сцене.
using System.Collections.Generic;
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyCrawler : MonoBehaviour, ILevelActor
    {
        public float speed = 2.2f;
        public float aggroRange = 9f;        // замечает игрока на этой дистанции, если видит его
        public float hearRange = 4f;         // а на этой — даже из-за стены
        public float repathInterval = 0.4f;
        public int contactDamage = 1;
        [Tooltip("Пауза после рывка, прежде чем снова бежать")]
        public float contactCooldown = 1f;

        [Header("Атака рывком")]
        [Tooltip("С какого расстояния клещ начинает замах")]
        public float attackRange = 1.8f;
        [Tooltip("Замах: клещ стоит и мигает — у кота есть время уйти")]
        public float windupTime = 0.5f;
        public float lungeSpeed = 7f;
        public float lungeTime = 0.25f;

        /// <summary>Клещ начал замах (точка) — для звука.</summary>
        public static event System.Action<Vector2> AttackTelegraphed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { AttackTelegraphed = null; }

        enum State { Chase, Windup, Lunge, Recover }

        LevelContext _ctx;
        Rigidbody2D _rb;
        ShotTelegraph _telegraph;            // то же мигание, что у стрелков (без пунктира)
        List<GridPos> _path;
        int _index;
        float _nextRepath;
        bool _aggro;
        State _state;
        float _stateEnd, _stateStart;
        Vector2 _lungeDir;
        bool _hitThisLunge;

        public void Init(LevelContext ctx) { _ctx = ctx; }

        void Awake()
        {
            _rb = Prims.AddTopDownBody(gameObject);
            _telegraph = GetComponent<ShotTelegraph>();
            if (_telegraph == null) _telegraph = gameObject.AddComponent<ShotTelegraph>();
        }

        void Update()
        {
            if (_ctx == null || !_ctx.PlayerAlive) { _path = null; EndWindup(); _state = State.Chase; return; }

            Vector2 me = transform.position, target = _ctx.Player.transform.position;
            float dist = Vector2.Distance(me, target);
            if (!_aggro)
            {
                if (dist < hearRange) _aggro = true;
                else if (dist < aggroRange && _ctx.IsOnScreen(me) && CanSee(me, target, _ctx.Player.transform)) _aggro = true;
                if (!_aggro) return;
            }

            switch (_state)
            {
                case State.Chase:
                    if (dist <= attackRange && CanSee(me, target, _ctx.Player.transform)) { BeginWindup(target - me); return; }
                    if (Time.time >= _nextRepath)
                    {
                        _nextRepath = Time.time + repathInterval * Random.Range(0.8f, 1.2f); // разносим по кадрам
                        _path = _ctx.Pathfinder.FindPath(LevelGrid.WorldToCell(me), LevelGrid.WorldToCell(target));
                        _index = 1;
                    }
                    break;

                case State.Windup:
                    _telegraph.Show(me, _lungeDir, 0f, 0, Mathf.Clamp01((Time.time - _stateStart) / windupTime), drawLine: false);
                    if (Time.time >= _stateEnd)
                    {
                        EndWindup();
                        Enter(State.Lunge, lungeTime);
                        _hitThisLunge = false;
                    }
                    break;

                case State.Lunge:
                    if (Time.time >= _stateEnd) Enter(State.Recover, contactCooldown);
                    break;

                case State.Recover:
                    if (Time.time >= _stateEnd) { Enter(State.Chase, 0f); _nextRepath = 0f; }
                    break;
            }
        }

        void BeginWindup(Vector2 toPlayer)
        {
            _lungeDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : (Vector2)transform.right;
            _path = null;
            Enter(State.Windup, windupTime);
            AttackTelegraphed?.Invoke(transform.position);
        }

        void EndWindup() { if (_telegraph != null) _telegraph.Hide(); }

        void Enter(State s, float duration)
        {
            _state = s;
            _stateStart = Time.time;
            _stateEnd = Time.time + duration;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            switch (_state)
            {
                case State.Windup:
                    _rb.linearVelocity = Vector2.zero;
                    Face(_lungeDir, 900f * dt); // разворачивается мордой к цели — замах читается
                    return;
                case State.Lunge:
                    _rb.MovePosition(_rb.position + _lungeDir * (lungeSpeed * dt));
                    return;
                case State.Recover:
                    _rb.linearVelocity *= 0.85f; // отскок после удара гаснет
                    return;
            }

            if (_path == null || _index >= _path.Count) return;
            Vector2 wp = LevelGrid.CellToWorld(_path[_index]);
            Vector2 delta = wp - _rb.position;
            if (delta.magnitude < 0.15f) { _index++; return; }

            Vector2 dir = delta.normalized;
            _rb.MovePosition(_rb.position + dir * (speed * dt));
            Face(dir, 540f * dt);
        }

        void Face(Vector2 dir, float maxDegrees)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _rb.rotation = Mathf.MoveTowardsAngle(_rb.rotation, angle, maxDegrees);
        }

        bool CanSee(Vector2 from, Vector2 to, Transform player)
        {
            Vector2 d = to - from;
            return Ricochet.CastNearest(from, 0.05f, d.normalized, d.magnitude + 0.5f, transform, out var hit)
                   && hit.collider.transform.IsChildOf(player);
        }

        void OnCollisionEnter2D(Collision2D c) => TryHit(c);
        void OnCollisionStay2D(Collision2D c) => TryHit(c);

        // урон — только рывком и один раз за рывок; просто стоять рядом с клещом безопасно
        void TryHit(Collision2D c)
        {
            if (_state != State.Lunge || _hitThisLunge) return;
            var hp = c.collider.GetComponentInParent<Health>();
            if (hp == null || !hp.IsPlayer) return; // работает с любым скриптом игрока, важна только команда в Health
            hp.Damage(contactDamage, Team.Enemy);
            _hitThisLunge = true;
            Enter(State.Recover, contactCooldown); // ударил — отскакивает и отдыхает
            _rb.linearVelocity = -_lungeDir * 2f;
        }
    }
}
