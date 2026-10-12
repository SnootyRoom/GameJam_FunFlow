// Стрелок: стоит у укрытия и ищет выстрел — сначала прямой, потом с рикошетом.
// Перебирает углы и симулирует полёт теми же кастами, что и настоящий снаряд.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public class EnemyShooter : MonoBehaviour, ILevelActor
    {
        public Transform turret;
        public float bodyRadius = 0.4f;
        public float sightRange = 14f;
        public float fireInterval = 2.2f;
        public float bulletSpeed = 6f;
        public float bulletRadius = 0.12f;
        public int maxBounces = 1;            // сколько отскоков ИИ учитывает при прицеливании
        public int aimSamples = 72;           // сколько углов перебирать (72 = шаг 5°)
        public float aimErrorDegrees = 2f;    // небольшой разброс, чтобы ИИ не был снайпером
        public float turretTurnSpeed = 270f;
        public GameObject bulletPrefab;
        public Color bulletColor = new Color(1f, 0.35f, 0.9f);
        [Tooltip("Стреляет, только если сам виден на экране — игрок всегда видит, кто по нему бьёт")]
        public bool onlyWhenOnScreen = true;
        [Tooltip("Насколько глубоко внутри экрана должен быть враг, чтобы стрелять (юниты)")]
        public float screenInset = 0.5f;
        public bool drawDebugShot = true;

        LevelContext _ctx;
        float _timer;
        Vector2 _aimDir = Vector2.right;
        readonly List<Vector2> _debugPoints = new List<Vector2>();

        public void Init(LevelContext ctx) { _ctx = ctx; }

        void Start()
        {
            _timer = fireInterval * Random.Range(0.5f, 1.2f); // не все стреляют одновременно
        }

        void Update()
        {
            if (_ctx == null || !_ctx.PlayerAlive) return;
            var player = _ctx.Player.transform;

            if (turret != null)
            {
                float cur = turret.eulerAngles.z;
                float want = Mathf.Atan2(_aimDir.y, _aimDir.x) * Mathf.Rad2Deg;
                turret.rotation = Quaternion.Euler(0, 0, Mathf.MoveTowardsAngle(cur, want, turretTurnSpeed * Time.deltaTime));
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            bool visible = !onlyWhenOnScreen || _ctx.IsOnScreen(transform.position, screenInset);
            if (!visible || Vector2.Distance(transform.position, player.position) > sightRange || !TryFindShot(player, out var dir))
            {
                _timer = 0.3f; // попробуем ещё раз чуть позже
                return;
            }

            float err = Random.Range(-aimErrorDegrees, aimErrorDegrees);
            dir = Quaternion.Euler(0, 0, err) * dir;
            _aimDir = dir;
            Bullet.Spawn(Muzzle(dir), dir, gameObject, bulletColor, bulletSpeed, maxBounces, bulletPrefab);
            _timer = fireInterval * Random.Range(0.8f, 1.2f);
        }

        Vector2 Muzzle(Vector2 dir) => (Vector2)transform.position + dir * (bodyRadius + bulletRadius + 0.05f);

        public bool TryFindShot(Transform player, out Vector2 dir)
        {
            float range = sightRange * 2f;

            // 1) прямой выстрел
            dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
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

        void OnDrawGizmos()
        {
            if (!drawDebugShot || _debugPoints.Count < 2) return;
            Gizmos.color = Color.magenta;
            for (int i = 1; i < _debugPoints.Count; i++) Gizmos.DrawLine(_debugPoints[i - 1], _debugPoints[i]);
        }
    }
}
