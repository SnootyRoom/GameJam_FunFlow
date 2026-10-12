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

        [Header("Предупреждение о выстреле")]
        [Tooltip("Сколько секунд враг «заряжает» выстрел перед тем, как выстрелить (0 — без предупреждения)")]
        public float telegraphTime = 0.45f;
        [Tooltip("Цвет, которым мигает враг перед выстрелом")]
        public Color telegraphColor = new Color(1f, 0.3f, 0.3f);
        [Tooltip("Показывать пунктир, куда полетит снаряд")]
        public bool telegraphLine = true;
        public Color telegraphLineColor = new Color(1f, 0.35f, 0.35f, 0.85f);
        [Tooltip("Длина пунктира (юниты). Короче — только направление, длиннее — видно и рикошет")]
        public float telegraphLineLength = 5f;

        /// <summary>Враг начал заряжать выстрел (точка) — для звука.</summary>
        public static event System.Action<Vector2> ShotTelegraphed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ShotTelegraphed = null; }

        LevelContext _ctx;
        float _timer, _holdAimUntil;
        bool _windup;                         // заряжает выстрел
        float _windupStart, _windupEnd;
        Vector2 _windupDir;
        SpriteRenderer[] _renderers;
        Color[] _colors;
        readonly List<Vector2> _linePoints = new List<Vector2>();
        readonly List<SpriteRenderer> _dots = new List<SpriteRenderer>();
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
            if (_ctx == null || !_ctx.PlayerAlive) { if (_windup) EndWindup(); return; }
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

            if (_windup) { UpdateWindup(); return; }

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
            _holdAimUntil = Time.time + telegraphTime + 0.5f;
            if (telegraphTime > 0f) BeginWindup(dir);
            else Fire(dir);
        }

        void Fire(Vector2 dir)
        {
            Bullet.Spawn(Muzzle(dir), dir, gameObject, bulletColor, bulletSpeed, maxBounces, bulletPrefab, bulletSprite);
            _timer = fireInterval * Random.Range(0.8f, 1.2f);
        }

        // ------------------------------------------------------------------ Предупреждение о выстреле
        // Выстрел уже выбран, но вылетит через telegraphTime: враг мигает, а пунктир показывает, куда он бьёт.
        // Направление не меняется — у игрока есть время уйти с линии.

        void BeginWindup(Vector2 dir)
        {
            if (_renderers == null)
            {
                _renderers = GetComponentsInChildren<SpriteRenderer>();
                _colors = new Color[_renderers.Length];
            }
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _colors[i] = _renderers[i].color;
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
            // мигание ускоряется к выстрелу
            bool on = Mathf.Repeat((Time.time - _windupStart) * Mathf.Lerp(8f, 20f, k), 1f) < 0.5f;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].color = Color.Lerp(_colors[i], telegraphColor, on ? 0.85f : 0.2f);
            if (telegraphLine) DrawLine(Mathf.Lerp(0.35f, 1f, k));

            if (Time.time >= _windupEnd)
            {
                EndWindup();
                Fire(_windupDir);
            }
        }

        void EndWindup()
        {
            _windup = false;
            if (_renderers != null)
                for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].color = _colors[i];
            foreach (var d in _dots) if (d != null) d.gameObject.SetActive(false);
        }

        void OnDisable() { if (_windup) EndWindup(); }

        void OnDestroy()
        {
            foreach (var d in _dots) if (d != null) Destroy(d.gameObject);
        }

        /// <summary>Пунктир по траектории будущего снаряда (тот же расчёт, что у полёта). Пересчитываем каждый кадр — краб ходит.</summary>
        void DrawLine(float alpha)
        {
            Vector2 from = Muzzle(_windupDir);
            Ricochet.Simulate(from, _windupDir, bulletRadius, maxBounces, telegraphLineLength, transform, null, _linePoints);
            const float spacing = 0.3f;
            int used = 0;
            for (int s = 1; s < _linePoints.Count; s++)
            {
                Vector2 a = _linePoints[s - 1], b = _linePoints[s];
                float len = Vector2.Distance(a, b);
                for (float t = 0f; t < len; t += spacing)
                {
                    var dot = TakeDot(used++);
                    dot.transform.position = Vector2.Lerp(a, b, t / Mathf.Max(0.001f, len));
                    var c = telegraphLineColor;
                    c.a *= alpha * Mathf.Lerp(1f, 0.25f, (float)used * spacing / telegraphLineLength); // к концу тает
                    dot.color = c;
                }
            }
            for (int i = used; i < _dots.Count; i++) _dots[i].gameObject.SetActive(false);
        }

        SpriteRenderer TakeDot(int index)
        {
            if (index < _dots.Count)
            {
                _dots[index].gameObject.SetActive(true);
                return _dots[index];
            }
            // точки живут в корне уровня: не крутятся вместе со стрелком и удаляются вместе с уровнем
            var go = new GameObject("ShotWarning");
            go.transform.SetParent(_ctx != null && _ctx.Root != null ? _ctx.Root : null, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelArt.AimDot;
            sr.sortingOrder = 9;
            if (_renderers != null && _renderers.Length > 0 && _renderers[0] != null) sr.sharedMaterial = _renderers[0].sharedMaterial;
            _dots.Add(sr);
            return sr;
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
