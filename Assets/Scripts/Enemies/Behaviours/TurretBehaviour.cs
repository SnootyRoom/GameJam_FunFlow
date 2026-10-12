// Стрелок: ищет выстрел — сначала прямой, потом с рикошетом (симулирует полёт теми же кастами, что и снаряд).
// Нашёл → лазерный прицел по траектории (telegraphTime, направление зафиксировано — можно увернуться) →
// очередь из burst снарядов → перезарядка fireInterval. Параметры оружия берутся из EnemyDefinition.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public class TurretBehaviour : EnemyBehaviour
    {
        public Transform turret;
        public float bodyRadius = 0.4f;
        public int aimSamples = 72;           // сколько углов перебирать (72 = шаг 5°)
        public float turretTurnSpeed = 270f;
        public GameObject bulletPrefab;
        public Color bulletColor = new Color(1f, 0.35f, 0.9f);
        public Color laserColor = new Color(1f, 0.15f, 0.2f, 0.75f);
        public bool drawDebugShot = true;

        [Header("Заполняется из EnemyDefinition")]
        public float sightRange = 14f;
        public float fireInterval = 2.2f;
        public int burst = 1;
        public float burstGap = 0.12f;
        public float bulletSpeed = 6f;
        public int bulletDamage = 1;
        public float bulletRadius = Bullet.DefaultRadius;
        public int maxBounces = 1;
        public float aimErrorDegrees = 2f;
        public float telegraphTime = 0.25f;

        enum State { Seek, Telegraph, Burst }

        State _state;
        float _timer;
        int _burstLeft;
        bool _alerted;
        ChaseBehaviour _chase;   // если враг ещё и бегает (краб) — «!» уже показала погоня
        Vector2 _aimDir = Vector2.right, _lockedDir;
        readonly List<Vector2> _debugPoints = new List<Vector2>();
        readonly List<Vector2> _laserPoints = new List<Vector2>();
        readonly List<SpriteRenderer> _laser = new List<SpriteRenderer>();

        protected override void OnSpawned()
        {
            var d = Def;
            sightRange = d.sightRange;
            fireInterval = d.fireInterval;
            burst = Mathf.Max(1, d.burst);
            burstGap = d.burstGap;
            bulletSpeed = d.bulletSpeed;
            bulletDamage = d.bulletDamage;
            bulletRadius = d.bulletRadius;
            maxBounces = d.maxBounces;
            aimErrorDegrees = d.aimErrorDegrees;
            telegraphTime = d.telegraphTime;

            sightRange *= Ai.awareness;
            fireInterval *= Ai.reaction * Enemy.FireIntervalMultiplier;
            aimErrorDegrees *= Ai.aimError;
            maxBounces += Ai.extraBounces;
            _timer = fireInterval * Random.Range(0.5f, 1.2f); // не все стреляют одновременно
            _chase = GetComponent<ChaseBehaviour>();
        }

        void OnDestroy()
        {
            foreach (var l in _laser) if (l != null) Destroy(l.gameObject);
        }

        protected override void OnDeath(DamageInfo info) => ShowLaser(false);

        void Update()
        {
            if (!Active) { ShowLaser(false); return; }

            if (turret != null)
            {
                float cur = turret.eulerAngles.z;
                Vector2 look = _state == State.Seek ? _aimDir : _lockedDir;
                float want = Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg;
                turret.rotation = Quaternion.Euler(0, 0, Mathf.MoveTowardsAngle(cur, want, turretTurnSpeed * Time.deltaTime));
            }

            _timer -= Time.deltaTime;
            switch (_state)
            {
                case State.Seek: UpdateSeek(); break;
                case State.Telegraph: UpdateTelegraph(); break;
                case State.Burst: UpdateBurst(); break;
            }
        }

        void UpdateSeek()
        {
            if (_timer > 0f) return;
            var player = Ctx.PlayerTransform;
            if (DistanceToPlayer > sightRange || !TryFindShot(player, out var dir))
            {
                _timer = 0.3f; // попробуем ещё раз чуть позже
                return;
            }
            _aimDir = dir;

            // первый найденный выстрел: «!» и короткая пауза, чтобы игрок успел среагировать
            if (!_alerted && (_chase == null || !_chase.IsAggro))
            {
                _alerted = true;
                Enemy.Alert();
                _timer = 0.5f * Ai.reaction;
                return;
            }
            _alerted = true;

            _lockedDir = dir;
            _laserPoints.Clear();
            _laserPoints.AddRange(_debugPoints);
            if (telegraphTime > 0.01f)
            {
                _state = State.Telegraph;
                _timer = telegraphTime;
            }
            else StartBurst();
        }

        void UpdateTelegraph()
        {
            // лазер мигает всё чаще — вот-вот выстрел
            float k = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, telegraphTime));
            bool on = k < 0.6f || Mathf.Repeat(Time.time * Mathf.Lerp(10f, 30f, k), 1f) < 0.6f;
            DrawLaser(on ? Mathf.Lerp(0.35f, 1f, k) : 0.15f, Mathf.Lerp(0.03f, 0.06f, k));
            if (_timer <= 0f) StartBurst();
        }

        void StartBurst()
        {
            ShowLaser(false);
            _state = State.Burst;
            _burstLeft = burst;
            _timer = 0f;
        }

        void UpdateBurst()
        {
            if (_timer > 0f) return;
            float err = Random.Range(-aimErrorDegrees, aimErrorDegrees);
            Vector2 dir = Quaternion.Euler(0, 0, err) * _lockedDir;
            Bullet.Spawn(Muzzle(dir), dir, gameObject, Team.Enemy, bulletColor, bulletSpeed, maxBounces, bulletPrefab, bulletDamage, bulletRadius);
            _burstLeft--;
            if (_burstLeft > 0) { _timer = burstGap; return; }
            _state = State.Seek;
            _timer = fireInterval * Random.Range(0.8f, 1.2f);
        }

        Vector2 Muzzle(Vector2 dir) => Position + dir * (bodyRadius + bulletRadius + 0.05f);

        public bool TryFindShot(Transform player, out Vector2 dir)
        {
            float range = sightRange * 2f;

            // 1) прямой выстрел
            dir = ((Vector2)player.position - Position).normalized;
            if (Ricochet.Simulate(Muzzle(dir), dir, bulletRadius, 0, range, transform, player, _debugPoints))
                return true;

            // 2) рикошет: перебираем углы, начиная со случайного, чтобы выстрелы были разными
            if (maxBounces <= 0) return false;
            float step = 360f / Mathf.Max(8, aimSamples);
            float offset = Random.Range(0f, step);
            for (int i = 0; i < aimSamples; i++)
            {
                float a = (offset + i * step) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (Ricochet.Simulate(Muzzle(d), d, bulletRadius, maxBounces, range, transform, player, _debugPoints))
                {
                    dir = d;
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ Лазерный прицел

        void DrawLaser(float alpha, float width)
        {
            int segs = Mathf.Max(0, _laserPoints.Count - 1);
            while (_laser.Count < segs)
            {
                var go = Prims.Create("Laser", Game.LevelRoot, Prims.Square, laserColor, Vector3.zero, Vector2.one, 18);
                _laser.Add(go.GetComponent<SpriteRenderer>());
            }
            for (int i = 0; i < _laser.Count; i++)
            {
                var l = _laser[i];
                if (i >= segs) { l.enabled = false; continue; }
                Vector2 a = _laserPoints[i], b = _laserPoints[i + 1];
                Vector2 d = b - a;
                l.enabled = true;
                l.transform.position = (a + b) * 0.5f;
                l.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                l.transform.localScale = new Vector3(d.magnitude, width, 1f);
                l.color = Prims.WithAlpha(laserColor, laserColor.a * alpha);
            }
        }

        void ShowLaser(bool on)
        {
            if (on) return;
            foreach (var l in _laser) if (l != null) l.enabled = false;
        }

        void OnDrawGizmos()
        {
            if (!drawDebugShot || _debugPoints.Count < 2) return;
            Gizmos.color = Color.magenta;
            for (int i = 1; i < _debugPoints.Count; i++) Gizmos.DrawLine(_debugPoints[i - 1], _debugPoints[i]);
        }
    }
}
