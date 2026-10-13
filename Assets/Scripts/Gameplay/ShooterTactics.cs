// Тактика стрелка: не стоит столбом, а воюет от укрытий.
//   • попали — прячется туда, где кот его не видит, и сидит там hideTime (стрелять рикошетом из-за укрытия он умеет);
//   • отсидевшись или простояв repositionInterval на одном месте — выходит на новую позицию, откуда видит кота;
//   • кот подъехал ближе keepDistance — отходит, держит дистанцию.
// Ходит только по прямой на несколько клеток (проверка круговым кастом) — не застревает в узких проходах и не уходит
// в соседнюю комнату. Пока заряжает выстрел — стоит: пунктир-предупреждение не уезжает. Целится и стреляет EnemyShooter.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(EnemyShooter))]
    public class ShooterTactics : MonoBehaviour, ILevelActor
    {
        public float moveSpeed = 2.6f;
        [Tooltip("Сколько сидит в укрытии после попадания, сек")]
        public float hideTime = 1.8f;
        [Tooltip("Как часто меняет позицию, если в него не попадают, сек")]
        public float repositionInterval = 4.5f;
        [Tooltip("Ближе этого к коту не подпускает — отходит")]
        public float keepDistance = 3.5f;
        [Tooltip("Любимое расстояние до кота, когда выходит на позицию")]
        public float preferredDistance = 6.5f;
        [Tooltip("Дальше этого от нынешнего места новую позицию не ищет, клетки")]
        public int searchRadius = 4;

        enum State { Idle, Hold, Moving, Hiding }

        LevelContext _ctx;
        Rigidbody2D _rb;
        EnemyShooter _shooter;
        EnemyEvasion _evasion;
        Health _hp;
        float _radius = 0.4f;

        State _state;
        float _stateEnd;          // Hold — когда менять позицию; Hiding — когда выходить; Moving — когда сдаться (застрял)
        float _nextSearch;        // поиск позиции — не чаще, чем раз в 0.7 с, если подходящей не нашлось
        Vector2 _target;
        bool _goingToHide;
        int _lastHp;

        public void Init(LevelContext ctx)
        {
            _ctx = ctx;
            _shooter = GetComponent<EnemyShooter>();
            _evasion = GetComponent<EnemyEvasion>();
            var col = GetComponent<Collider2D>();
            if (col != null) _radius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y);
            _hp = GetComponent<Health>();
            if (_hp != null) { _lastHp = _hp.Hp; _hp.OnHpChanged += OnHp; }
        }

        void Awake() { _rb = Prims.AddTopDownBody(gameObject); }

        void OnDestroy() { if (_hp != null) _hp.OnHpChanged -= OnHp; }

        void OnHp(int hp, int max)
        {
            bool hurt = hp < _lastHp;
            _lastHp = hp;
            if (!hurt || hp <= 0 || _ctx == null || !_ctx.PlayerAlive) return;
            // попали — в укрытие (если уже сидит в нём, значит, достали рикошетом: ищем другое);
            // укрыться негде — хотя бы уйти с места, где достали
            if (FindSpot(hidden: true, out var spot)) MoveTo(spot, hide: true);
            else Reposition();
        }

        void Update()
        {
            if (_ctx == null || !_ctx.PlayerAlive) { _state = State.Idle; return; }
            Vector2 me = transform.position, cat = _ctx.Player.transform.position;
            float dist = Vector2.Distance(me, cat);

            switch (_state)
            {
                case State.Idle:
                    // в бой — когда кот рядом и стрелок на экране (как и стрельба)
                    if (dist <= _shooter.sightRange && _ctx.IsOnScreen(me)) Hold();
                    break;

                case State.Hold:
                    // кот слишком близко или пора сменить позицию
                    if (Time.time >= _nextSearch && (dist < keepDistance || Time.time >= _stateEnd) && !Reposition())
                        _nextSearch = Time.time + 0.7f; // некуда — постоим ещё
                    break;

                case State.Moving:
                    if (_shooter.Busy) _stateEnd += Time.deltaTime; // стоит, пока заряжает, — это не «застрял»
                    if ((_target - me).sqrMagnitude < 0.01f || Time.time >= _stateEnd) Arrive();
                    break;

                case State.Hiding:
                    if (Time.time >= _stateEnd && !Reposition()) Hold(); // отсиделся — на позицию
                    break;
            }
        }

        void FixedUpdate()
        {
            if (_rb == null || (_evasion != null && _evasion.Dodging)) return;
            // пока заряжает выстрел или стоит — не двигается (и не уезжает, если его толкнули)
            if (_state != State.Moving || _shooter.Busy) { _rb.linearVelocity = Vector2.zero; return; }
            Vector2 delta = _target - _rb.position;
            float step = moveSpeed * Time.fixedDeltaTime;
            _rb.MovePosition(delta.magnitude <= step ? _target : _rb.position + delta.normalized * step);
        }

        void Hold()
        {
            _state = State.Hold;
            _stateEnd = Time.time + repositionInterval * Random.Range(0.75f, 1.25f);
        }

        /// <summary>Выйти на новую позицию, откуда видно кота. false — подходящей нет.</summary>
        bool Reposition()
        {
            if (!FindSpot(hidden: false, out var spot)) return false;
            MoveTo(spot, hide: false);
            return true;
        }

        void MoveTo(Vector2 spot, bool hide)
        {
            _target = spot;
            _goingToHide = hide;
            _state = State.Moving;
            _stateEnd = Time.time + Vector2.Distance(transform.position, spot) / Mathf.Max(0.1f, moveSpeed) * 1.5f + 0.6f;
        }

        void Arrive()
        {
            if (_goingToHide)
            {
                _state = State.Hiding;
                _stateEnd = Time.time + hideTime * Random.Range(0.8f, 1.2f);
            }
            else Hold();
        }

        /// <summary>
        /// Ищет клетку рядом: hidden — кот её не видит (за стеной или укрытием), иначе — видит, и она на экране
        /// (стрелок стреляет, только когда виден). Клетка должна вмещать стрелка и быть достижима по прямой.
        /// </summary>
        bool FindSpot(bool hidden, out Vector2 best)
        {
            best = default;
            if (_ctx.Data == null) return false;
            Vector2 me = transform.position, cat = _ctx.Player.transform.position;
            var myCell = LevelGrid.WorldToCell(me);
            float bestScore = float.MinValue;

            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                var cell = new GridPos(myCell.x + dx, myCell.y + dy);
                if (!_ctx.Data.IsWalkable(cell)) continue;
                Vector2 c = LevelGrid.CellToWorld(cell);
                float move = Vector2.Distance(c, me);
                if (move < 0.7f || move > searchRadius + 0.5f) continue;
                float toCat = Vector2.Distance(c, cat);
                if (toCat < keepDistance) continue;
                if (!hidden && (toCat > _shooter.sightRange * 0.9f || !_ctx.IsOnScreen(c, 1f))) continue;

                // дешёвые проверки — раньше, физика — только для тех, кто может победить
                float score = hidden
                    ? -move + Mathf.Min(toCat, preferredDistance) * 0.3f
                    : -Mathf.Abs(toCat - preferredDistance) - move * 0.4f;
                score += Random.Range(0f, 0.6f); // чтобы не бегал по одним и тем же клеткам
                if (score <= bestScore) continue;

                if (SeesCat(c, cat) == hidden) continue;
                if (Ricochet.SpawnBlocker(c, _radius * 0.95f, transform) != null) continue;           // не влезет
                Vector2 d = c - me;
                if (Ricochet.CastNearest(me, _radius * 0.9f, d.normalized, d.magnitude, transform, out _)) continue; // не дойти по прямой
                bestScore = score;
                best = c;
            }
            return bestScore > float.MinValue;
        }

        bool SeesCat(Vector2 from, Vector2 cat)
        {
            Vector2 d = cat - from;
            if (!Ricochet.CastNearest(from, 0.15f, d.normalized, d.magnitude + 0.5f, transform, out var hit)) return false;
            // за другим врагом не прячется: союзник — не укрытие, его самого могут убить
            return hit.collider.transform.IsChildOf(_ctx.Player.transform) || hit.collider.GetComponentInParent<Health>() != null;
        }
    }
}
