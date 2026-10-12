// Погоня: замечает игрока (видит / слышит рядом / слышит выстрел), бежит к нему по A* через сетку уровня.
// Скорость — из EnemyDefinition, «ум» — из AiParams уровня. Крупные враги (footprint 2) ищут путь
// с учётом своего размера, поэтому не застревают в узких местах. Hold/stopDistance — для боссов и стрелков.
using System.Collections.Generic;
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class ChaseBehaviour : EnemyBehaviour
    {
        public float aggroRange = 9f;        // замечает игрока на этой дистанции, если видит его
        public float hearRange = 4f;         // а на этой — даже из-за стены
        public float repathInterval = 0.4f;
        [Tooltip("Останавливаться, если игрок ближе этого и виден (0 — лезть вплотную)")]
        public float stopDistance = 0f;

        public bool IsAggro { get; private set; }
        /// <summary>Другое поведение (атака босса) временно забрало управление движением.</summary>
        public bool Hold { get; set; }
        public float SpeedScale { get; set; } = 1f;

        Rigidbody2D _rb;
        List<GridPos> _path;
        int _index, _footprint = 1;
        float _nextRepath, _speed, _stuckTimer;
        bool _fleeing, _inRange;
        Vector2 _fleeDir, _lastPos;

        void Awake()
        {
            _rb = Prims.AddTopDownBody(gameObject);
        }

        protected override void OnSpawned()
        {
            _speed = Def.moveSpeed * Ai.speed * Enemy.SpeedMultiplier;
            _footprint = Mathf.Max(1, Def.footprint);
            aggroRange *= Ai.awareness;
            hearRange *= Ai.awareness;
            repathInterval *= Ai.reaction;
            if (Ai.reactToShots) GameEvents.BulletFired += OnBulletFired;
            Enemy.Health.Damaged += (h, i) => ForceAggro(); // в тебя попали — значит, тебя нашли
            _lastPos = _rb.position;
        }

        void OnDestroy() { GameEvents.BulletFired -= OnBulletFired; }

        // на высоких уровнях выстрел игрока поблизости поднимает тревогу
        void OnBulletFired(Bullet b)
        {
            if (!IsAggro && b.Team == Team.Player && Vector2.Distance(b.Position, Position) < aggroRange * 1.5f) SetAggro();
        }

        /// <summary>Заставить врага заметить игрока (урон, босс-триггер).</summary>
        public void ForceAggro() { if (!IsAggro) SetAggro(); }

        void Update()
        {
            if (!Active) { _path = null; return; }

            // тень падающего ореха: умные враги (уровень 3+) убегают из зоны удара
            _fleeing = (Ai.reactToShots || Ai.dodgeBullets) && Hazards.IsInDanger(Position, 0.4f, out _fleeDir, out _);
            if (_fleeing) return;

            if (!IsAggro)
            {
                float dist = DistanceToPlayer;
                if (dist < hearRange) SetAggro();
                else if (dist < aggroRange && EnemySenses.CanSee(Position, Ctx.PlayerTransform, transform)) SetAggro();
                if (!IsAggro) return;
            }

            _inRange = stopDistance > 0f && DistanceToPlayer < stopDistance && EnemySenses.CanSee(Position, Ctx.PlayerTransform, transform);

            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + repathInterval * Random.Range(0.8f, 1.2f); // разносим по кадрам
                // узел пути — левая нижняя клетка площадки, а сам враг стоит в её центре
                Vector2 offset = Vector2.one * ((_footprint - 1) * 0.5f);
                var from = LevelBuilder.WorldToCell(Position - offset);
                var to = LevelBuilder.WorldToCell(PlayerPosition - offset);
                _path = Ctx.Pathfinder.FindPath(from, to, 6000, _footprint);
                if (_path == null && _footprint > 1) _path = Ctx.Pathfinder.FindPath(from, to, 6000, 1); // лучше подойти поближе, чем стоять
                _index = 1;
            }
        }

        void SetAggro()
        {
            IsAggro = true;
            Enemy.Alert();
        }

        void FixedUpdate()
        {
            if (!Active || Hold) return;
            float speed = _speed * SpeedScale;
            if (_fleeing)
            {
                _rb.MovePosition(_rb.position + _fleeDir * (speed * 1.3f * Time.fixedDeltaTime));
                return;
            }
            if (_inRange || _path == null || _index >= _path.Count) return;

            Vector2 offset = Vector2.one * ((_footprint - 1) * 0.5f);
            Vector2 wp = LevelBuilder.CellToWorld(_path[_index]) + offset;
            Vector2 delta = wp - _rb.position;
            if (delta.magnitude < 0.15f) { _index++; return; }

            Vector2 dir = delta.normalized;
            _rb.MovePosition(_rb.position + dir * (speed * Time.fixedDeltaTime));
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _rb.rotation = Mathf.MoveTowardsAngle(_rb.rotation, angle, 540f * Time.fixedDeltaTime);

            // застрял (упёрся в другого врага/угол) — пересчитать путь пораньше и чуть сдвинуться
            _stuckTimer = (_rb.position - _lastPos).sqrMagnitude < 0.0004f ? _stuckTimer + Time.fixedDeltaTime : 0f;
            _lastPos = _rb.position;
            if (_stuckTimer > 0.6f)
            {
                _stuckTimer = 0f;
                _nextRepath = 0f;
                _rb.MovePosition(_rb.position + Random.insideUnitCircle * 0.05f);
            }
        }
    }
}
