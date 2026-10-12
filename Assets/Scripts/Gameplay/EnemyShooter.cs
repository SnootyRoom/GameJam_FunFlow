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
        [Tooltip("Сколько углов проверять за кадр: поиск рикошета растягивается на несколько кадров и не даёт рывков")]
        public int aimSamplesPerFrame = 12;
        public float aimErrorDegrees = 2f;    // небольшой разброс, чтобы ИИ не был снайпером
        public float turretTurnSpeed = 270f;
        public GameObject bulletPrefab;
        public Sprite bulletSprite;           // пусто = кружок цвета bulletColor
        public Color bulletColor = new Color(1f, 0.35f, 0.9f);
        [Tooltip("Стреляет, только если сам виден на экране — игрок всегда видит, кто по нему бьёт")]
        public bool onlyWhenOnScreen = true;
        [Tooltip("Насколько глубоко внутри экрана должен быть враг, чтобы стрелять (юниты)")]
        public float screenInset = 0.5f;
        public bool drawDebugShot = true;

        [Tooltip("Между выстрелами башня (или весь спрайт стрелка) смотрит на игрока")]
        public bool trackPlayer = true;

        LevelContext _ctx;
        float _timer, _holdAimUntil;
        Vector2 _aimDir = Vector2.right;
        readonly List<Vector2> _debugPoints = new List<Vector2>();
        bool _searching;                      // идёт поиск рикошета, растянутый по кадрам
        int _searchIndex;
        float _searchOffset;

        public void Init(LevelContext ctx) { _ctx = ctx; }

        void Start()
        {
            _timer = fireInterval * Random.Range(0.5f, 1.2f); // не все стреляют одновременно
        }

        void Update()
        {
            if (_ctx == null || !_ctx.PlayerAlive) return;
            var player = _ctx.Player.transform;

            // между выстрелами следим взглядом за игроком, а после выстрела ненадолго держим направление выстрела
            if (trackPlayer && Time.time >= _holdAimUntil && Vector2.Distance(transform.position, player.position) <= sightRange)
                _aimDir = ((Vector2)player.position - (Vector2)transform.position).normalized;

            if (turret != null)
            {
                float cur = turret.eulerAngles.z;
                float want = Mathf.Atan2(_aimDir.y, _aimDir.x) * Mathf.Rad2Deg;
                turret.rotation = Quaternion.Euler(0, 0, Mathf.MoveTowardsAngle(cur, want, turretTurnSpeed * Time.deltaTime));
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            bool visible = !onlyWhenOnScreen || _ctx.IsOnScreen(transform.position, screenInset);
            if (!visible || Vector2.Distance(transform.position, player.position) > sightRange) { Retry(); return; }

            Vector2 dir;
            if (!_searching)
            {
                if (!TryDirectShot(player, out dir))
                {
                    if (maxBounces <= 0) { Retry(); return; }
                    // прямого выстрела нет — начинаем перебор рикошетов со случайного угла, чтобы выстрелы были разными
                    _searching = true;
                    _searchIndex = 0;
                    _searchOffset = Random.Range(0f, 360f / Mathf.Max(8, aimSamples));
                    return;
                }
            }
            else if (!TryBounceShot(player, out dir))
            {
                if (_searchIndex >= aimSamples) Retry(); // все углы перебраны — выстрела нет
                return;
            }
            _searching = false;

            float err = Random.Range(-aimErrorDegrees, aimErrorDegrees);
            dir = Quaternion.Euler(0, 0, err) * dir;
            _aimDir = dir;
            _holdAimUntil = Time.time + 0.5f;
            Bullet.Spawn(Muzzle(dir), dir, gameObject, bulletColor, bulletSpeed, maxBounces, bulletPrefab, bulletSprite);
            _timer = fireInterval * Random.Range(0.8f, 1.2f);
        }

        Vector2 Muzzle(Vector2 dir) => (Vector2)transform.position + dir * (bodyRadius + bulletRadius + 0.05f);

        void Retry()
        {
            _searching = false;
            _timer = 0.3f; // попробуем ещё раз чуть позже
        }

        List<Vector2> DebugPoints => drawDebugShot ? _debugPoints : null;

        bool TryDirectShot(Transform player, out Vector2 dir)
        {
            dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
            return Ricochet.Simulate(Muzzle(dir), dir, bulletRadius, 0, sightRange * 2f, transform, player, DebugPoints);
        }

        /// <summary>Следующая порция углов рикошета (aimSamplesPerFrame за кадр).</summary>
        bool TryBounceShot(Transform player, out Vector2 dir)
        {
            float step = 360f / Mathf.Max(8, aimSamples);
            int end = Mathf.Min(aimSamples, _searchIndex + Mathf.Max(1, aimSamplesPerFrame));
            for (; _searchIndex < end; _searchIndex++)
            {
                float a = (_searchOffset + _searchIndex * step) * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (Ricochet.Simulate(Muzzle(d), d, bulletRadius, maxBounces, sightRange * 2f, transform, player, DebugPoints))
                {
                    dir = d;
                    return true;
                }
            }
            dir = default;
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
