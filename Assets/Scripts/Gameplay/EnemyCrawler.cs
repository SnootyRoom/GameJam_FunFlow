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
        [Tooltip("Зигзаг, когда кот его видит: 0 — бежит прямо, 1 — сильно виляет (в него сложнее попасть)")]
        [Range(0f, 1f)] public float weave = 0f;
        public float weaveFrequency = 7f;

        [Header("Атака рывком")]
        [Tooltip("С какого расстояния клещ начинает замах")]
        public float attackRange = 1.8f;
        [Tooltip("Замах: клещ стоит и мигает — у кота есть время уйти")]
        public float windupTime = 0.5f;
        public float lungeSpeed = 7f;
        public float lungeTime = 0.25f;
        [Tooltip("Сколько рывков подряд (стадии босса): после промаха — короткий замах и новый рывок туда, где кот сейчас")]
        public int lungeChain = 1;
        [Tooltip("Замах перед повторным рывком серии — доля от windupTime")]
        public float chainWindupFactor = 0.6f;

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
        int _lungesDone;                     // сколько рывков уже сделано в текущей серии
        EnemyEvasion _evasion;
        Health _hp;
        bool _inSight;                       // кот его видит — виляет зигзагом
        float _weavePhase;

        public void Init(LevelContext ctx)
        {
            _ctx = ctx;
            _evasion = GetComponent<EnemyEvasion>();
            _hp = GetComponent<Health>();
            if (_hp != null) _hp.OnHpChanged += OnHp;
        }

        void Awake()
        {
            _rb = Prims.AddTopDownBody(gameObject);
            _telegraph = GetComponent<ShotTelegraph>();
            if (_telegraph == null) _telegraph = gameObject.AddComponent<ShotTelegraph>();
            _weavePhase = Random.Range(0f, 10f); // не виляют в ногу
        }

        void OnDestroy() { if (_hp != null) _hp.OnHpChanged -= OnHp; }

        // в него попали издалека — значит, кот рядом: бежит в бой, а не стоит мишенью
        void OnHp(int hp, int max) { if (hp > 0 && hp < max) _aggro = true; }

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
                    if (dist <= attackRange && CanSee(me, target, _ctx.Player.transform)) { _lungesDone = 0; BeginWindup(target - me, windupTime); return; }
                    if (Time.time >= _nextRepath)
                    {
                        _nextRepath = Time.time + repathInterval * Random.Range(0.8f, 1.2f); // разносим по кадрам
                        _path = _ctx.Pathfinder.FindPath(LevelGrid.WorldToCell(me), LevelGrid.WorldToCell(target));
                        _index = 1;
                        _inSight = weave > 0f && CanSee(me, target, _ctx.Player.transform);
                    }
                    break;

                case State.Windup:
                    _telegraph.Show(me, _lungeDir, 0f, 0, Mathf.Clamp01((Time.time - _stateStart) / Mathf.Max(0.01f, _stateEnd - _stateStart)), drawLine: false);
                    if (Time.time >= _stateEnd)
                    {
                        EndWindup();
                        Enter(State.Lunge, lungeTime);
                        _hitThisLunge = false;
                    }
                    break;

                case State.Lunge:
                    if (Time.time < _stateEnd) break;
                    // серия рывков: промахнулся — тут же замахивается ещё раз (уже короче), пока серия не кончится
                    if (++_lungesDone < lungeChain) BeginWindup(target - me, Mathf.Max(0.2f, windupTime * chainWindupFactor)); // не короче 0.2 с — успеть увидеть
                    else Enter(State.Recover, contactCooldown);
                    break;

                case State.Recover:
                    if (Time.time >= _stateEnd) { Enter(State.Chase, 0f); _nextRepath = 0f; }
                    break;
            }
        }

        void BeginWindup(Vector2 toPlayer, float duration)
        {
            _lungeDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : (Vector2)transform.right;
            _path = null;
            Enter(State.Windup, duration);
            AttackTelegraphed?.Invoke(transform.position);
        }

        /// <summary>Замахивается или делает рывок? Пока да, другие атаки ждут.</summary>
        public bool Busy => _state == State.Windup || _state == State.Lunge;

        /// <summary>Сбросить атаку и встать (например, босс перешёл в новую стадию). Дальше — снова погоня.</summary>
        public void Interrupt()
        {
            EndWindup();
            Enter(State.Chase, 0f);
            _path = null;
            _nextRepath = 0f;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
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
            if (_evasion != null && _evasion.Dodging) return; // отскок двигает тело сам
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
            if (delta.magnitude < (_inSight ? 0.3f : 0.15f)) { _index++; return; }

            Vector2 dir = delta.normalized, move = dir;
            if (_inSight)
            {
                // зигзаг: вперёд всегда больше, чем вбок, — до точки пути всё равно добежит
                Vector2 side = new Vector2(-dir.y, dir.x);
                move = (dir + side * (Mathf.Sin((Time.time + _weavePhase) * weaveFrequency) * weave)).normalized;
            }
            _rb.MovePosition(_rb.position + move * (speed * dt));
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
