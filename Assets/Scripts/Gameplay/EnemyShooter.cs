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
        [Tooltip("Снарядов в залпе: 1 — одиночный, больше — веер вокруг выбранного направления (стадии босса)")]
        public int volley = 1;
        [Tooltip("Угол между снарядами веера, градусы")]
        public float volleySpread = 14f;
        [Tooltip("Упреждение прямого выстрела: 0 — бьёт туда, где кот сейчас, 1 — туда, где он будет, если не свернёт")]
        [Range(0f, 1f)] public float leadTarget = 0f;
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

        [Header("Предупреждение о выстреле")]
        [Tooltip("Сколько секунд враг «заряжает» выстрел перед тем, как выстрелить (0 — без предупреждения)")]
        public float telegraphTime = 0.45f;

        /// <summary>Враг начал заряжать выстрел (точка) — для звука.</summary>
        public static event System.Action<Vector2> ShotTelegraphed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ShotTelegraphed = null; }

        LevelContext _ctx;
        float _timer, _holdAimUntil;
        bool _windup;                         // заряжает выстрел
        float _windupStart, _windupEnd;
        Vector2 _windupDir;
        ShotTelegraph _telegraph;             // визуал предупреждения (мигание + пунктир)
        Vector2 _aimDir = Vector2.right;
        readonly List<Vector2> _debugPoints = new List<Vector2>();
        bool _searching;                      // идёт поиск рикошета, растянутый по кадрам
        int _searchIndex;
        float _searchOffset;
        Vector2 _playerLastPos, _playerVel;   // скорость кота для упреждения — по смещению (он ездит через MovePosition)
        bool _hasPlayerPos;

        public void Init(LevelContext ctx)
        {
            _ctx = ctx;
            if (_telegraph != null) _telegraph.SetDotsParent(ctx.Root);
        }

        void Awake()
        {
            _telegraph = GetComponent<ShotTelegraph>();
            if (_telegraph == null) _telegraph = gameObject.AddComponent<ShotTelegraph>();
            if (_ctx != null) _telegraph.SetDotsParent(_ctx.Root);
        }

        void Start()
        {
            _timer = fireInterval * Random.Range(0.5f, 1.2f); // не все стреляют одновременно
        }

        void Update()
        {
            if (_ctx == null || !_ctx.PlayerAlive) { if (_windup) EndWindup(); return; }
            var player = _ctx.Player.transform;
            TrackPlayerVelocity(player.position);

            // между выстрелами следим взглядом за игроком, а после выстрела ненадолго держим направление выстрела
            if (trackPlayer && Time.time >= _holdAimUntil && Vector2.Distance(transform.position, player.position) <= sightRange)
                _aimDir = ((Vector2)player.position - (Vector2)transform.position).normalized;

            if (turret != null)
            {
                float cur = turret.eulerAngles.z;
                float want = Mathf.Atan2(_aimDir.y, _aimDir.x) * Mathf.Rad2Deg;
                turret.rotation = Quaternion.Euler(0, 0, Mathf.MoveTowardsAngle(cur, want, turretTurnSpeed * Time.deltaTime));
            }

            if (_windup) { UpdateWindup(); return; }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            bool visible = !onlyWhenOnScreen || _ctx.IsOnScreen(transform.position, screenInset);
            if (!visible || Vector2.Distance(transform.position, player.position) > sightRange) { Retry(); return; }

            Vector2 dir;
            if (!_searching)
            {
                if (TryDirectShot(player, out dir)) dir = Lead(player.position, dir);
                else
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
            _holdAimUntil = Time.time + telegraphTime + 0.5f;
            if (telegraphTime > 0f) BeginWindup(dir);
            else Fire(dir);
        }

        /// <summary>Заряжает выстрел (мигает, пунктир)? Пока заряжает, другие атаки ждут.</summary>
        public bool Busy => _windup;

        /// <summary>Сбросить зарядку и поиск выстрела (например, босс перешёл в новую стадию). delay — пауза до следующей попытки.</summary>
        public void Interrupt(float delay)
        {
            if (_windup) EndWindup();
            _searching = false;
            _timer = delay;
        }

        void Fire(Vector2 dir)
        {
            // центральный снаряд летит точно по пунктиру, остальные — веером вокруг него
            int n = Mathf.Max(1, volley);
            for (int i = 0; i < n; i++)
            {
                Vector2 d = Quaternion.Euler(0, 0, (i - (n - 1) * 0.5f) * volleySpread) * dir;
                Bullet.Spawn(Muzzle(d), d, gameObject, bulletColor, bulletSpeed, maxBounces, bulletPrefab, bulletSprite);
            }
            _timer = fireInterval * Random.Range(0.8f, 1.2f);
        }

        // ------------------------------------------------------------------ Предупреждение о выстреле
        // Выстрел уже выбран, но вылетит через telegraphTime: враг мигает, а пунктир показывает, куда он бьёт.
        // Направление не меняется — у игрока есть время уйти с линии.

        void BeginWindup(Vector2 dir)
        {
            _windup = true;
            _windupDir = dir;
            _windupStart = Time.time;
            _windupEnd = Time.time + telegraphTime;
            ShotTelegraphed?.Invoke(transform.position);
            UpdateWindup();
        }

        void UpdateWindup()
        {
            float k = Mathf.Clamp01((Time.time - _windupStart) / telegraphTime);
            _telegraph.Show(Muzzle(_windupDir), _windupDir, bulletRadius, maxBounces, k);
            if (Time.time >= _windupEnd)
            {
                EndWindup();
                Fire(_windupDir);
            }
        }

        void EndWindup()
        {
            _windup = false;
            _telegraph.Hide();
        }

        public Vector2 Muzzle(Vector2 dir) => (Vector2)transform.position + dir * (bodyRadius + bulletRadius + 0.05f);

        void Retry()
        {
            _searching = false;
            _timer = 0.3f; // попробуем ещё раз чуть позже
        }

        void TrackPlayerVelocity(Vector2 pos)
        {
            if (_hasPlayerPos && Time.deltaTime > 0f)
                _playerVel = Vector2.Lerp(_playerVel, (pos - _playerLastPos) / Time.deltaTime, 1f - Mathf.Exp(-10f * Time.deltaTime));
            _playerLastPos = pos;
            _hasPlayerPos = true;
        }

        /// <summary>
        /// Прямой выстрел с упреждением: туда, где кот будет к прилёту снаряда (с учётом замаха).
        /// Если до этой точки мешает стена — бьём прямо в кота.
        /// </summary>
        Vector2 Lead(Vector2 target, Vector2 direct)
        {
            if (leadTarget <= 0f || _playerVel.sqrMagnitude < 0.04f) return direct;
            Vector2 from = transform.position;
            float t = telegraphTime + Vector2.Distance(from, target) / Mathf.Max(0.1f, bulletSpeed);
            Vector2 aim = target + _playerVel * (t * leadTarget);
            Vector2 d = (aim - from).normalized;
            Vector2 muzzle = Muzzle(d);
            float dist = Vector2.Distance(muzzle, aim);
            if (Ricochet.CastNearest(muzzle, bulletRadius, d, dist, transform, out var hit) &&
                !hit.collider.transform.IsChildOf(_ctx.Player.transform))
                return direct;
            return d;
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
