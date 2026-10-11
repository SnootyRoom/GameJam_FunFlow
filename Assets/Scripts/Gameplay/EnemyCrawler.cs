// Ползун: замечает игрока, бежит к нему по A* через сетку уровня и таранит.
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
        public float contactCooldown = 1f;

        LevelContext _ctx;
        Rigidbody2D _rb;
        List<GridPos> _path;
        int _index;
        float _nextRepath, _nextHit;
        bool _aggro;

        public void Init(LevelContext ctx) { _ctx = ctx; }

        void Awake()
        {
            _rb = Prims.AddTopDownBody(gameObject);
        }

        void Update()
        {
            if (_ctx == null || !_ctx.PlayerAlive) { _path = null; return; }

            Vector2 me = transform.position, target = _ctx.Player.transform.position;
            float dist = Vector2.Distance(me, target);
            if (!_aggro)
            {
                if (dist < hearRange) _aggro = true;
                else if (dist < aggroRange && CanSee(me, target, _ctx.Player.transform)) _aggro = true;
                if (!_aggro) return;
            }

            if (Time.time >= _nextRepath)
            {
                _nextRepath = Time.time + repathInterval * Random.Range(0.8f, 1.2f); // разносим по кадрам
                _path = _ctx.Pathfinder.FindPath(LevelGrid.WorldToCell(me), LevelGrid.WorldToCell(target));
                _index = 1;
            }
        }

        void FixedUpdate()
        {
            if (_path == null || _index >= _path.Count) return;
            Vector2 wp = LevelGrid.CellToWorld(_path[_index]);
            Vector2 delta = wp - _rb.position;
            if (delta.magnitude < 0.15f) { _index++; return; }

            Vector2 dir = delta.normalized;
            _rb.MovePosition(_rb.position + dir * (speed * Time.fixedDeltaTime));
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _rb.rotation = Mathf.MoveTowardsAngle(_rb.rotation, angle, 540f * Time.fixedDeltaTime);
        }

        bool CanSee(Vector2 from, Vector2 to, Transform player)
        {
            Vector2 d = to - from;
            return Ricochet.CastNearest(from, 0.05f, d.normalized, d.magnitude + 0.5f, transform, out var hit)
                   && hit.collider.transform.IsChildOf(player);
        }

        void OnCollisionStay2D(Collision2D c)
        {
            if (Time.time < _nextHit) return;
            var hp = c.collider.GetComponentInParent<Health>();
            if (hp == null || !hp.IsPlayer) return; // работает с любым скриптом игрока, важна только команда в Health
            hp.Damage(contactDamage);
            _nextHit = Time.time + contactCooldown;
        }
    }
}
