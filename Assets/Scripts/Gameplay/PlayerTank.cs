// Танк игрока: корпус ездит, башня смотрит на курсор, ЛКМ — основной выстрел с рикошетом, ПКМ (зажать) — пулемёт.
// Заготовка для разработчика игрока: можно переписать целиком. Важно только, чтобы на объекте
// был Health с team = Player, Collider2D и Rigidbody2D (см. README). ILevelActor — по желанию.
// Заглушка из примитивов — Placeholders.Player. Линия прицела — отдельный компонент AimPreview.
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerTank : MonoBehaviour
    {
        [Header("Движение")]
        public bool tankControls = true;   // true: W/S вперёд-назад, A/D поворот; false: WASD по сторонам света
        public float moveSpeed = 3.5f;
        public float turnSpeed = 180f;

        [Header("Основное оружие (ЛКМ)")]
        public Transform turret;
        public float muzzleDistance = 0.6f;
        public float bulletSpeed = 8f;
        public int maxBounces = 1;
        public float fireCooldown = 0.3f;
        public int maxBulletsAlive = 5;     // общий лимит снарядов в полёте для обоих оружий
        public GameObject bulletPrefab;
        [Tooltip("Спрайт снаряда (если нет префаба). Пусто = кружок цвета bulletColor")]
        public Sprite bulletSprite;
        public Color bulletColor = new Color(1f, 0.95f, 0.6f);

        [Header("Пулемёт (ПКМ — зажать)")]
        public bool minigunEnabled = true;
        public float minigunSpeed = 10f;
        public int minigunBounces = 0;
        public float minigunCooldown = 0.12f;
        [Tooltip("Патронов в магазине: столько выстрелов подряд, потом перезарядка")]
        public int minigunMagazine = 4;
        [Tooltip("Перезарядка магазина, секунды")]
        public float minigunReloadTime = 1.2f;
        [Tooltip("Если отпустить ПКМ с неполным магазином — через столько секунд он начнёт перезаряжаться")]
        public float minigunIdleReload = 0.5f;
        [Tooltip("Пулемёт стреляет, только пока в полёте меньше стольких снарядов — чтобы не забивал лимит основного")]
        public int minigunMaxBullets = 4;
        public int minigunDamage = 1;
        public GameObject minigunBulletPrefab;
        public Color minigunColor = new Color(0.85f, 0.45f, 0.2f);

        [Header("Отдача")]
        [Tooltip("На сколько башня откатывается назад при основном выстреле (юниты)")]
        public float recoilDistance = 0.12f;
        public float minigunRecoil = 0.04f;
        public float recoilReturn = 14f;     // как быстро башня возвращается на место

        Rigidbody2D _rb;
        float _nextFire, _nextMinigunFire;
        Vector2 _move;
        Vector3 _turretLocal;
        float _recoil;                       // текущий откат башни, юниты
        int _minigunRounds = -1;             // -1 — ещё не заряжен (заполним в Awake)
        float _minigunReloadEnd, _lastMinigunShot;

        // ----- для HUD: боезапас и готовность основного выстрела -----
        /// <summary>Сколько снарядов можно выпустить прямо сейчас (лимит в полёте минус уже летящие).</summary>
        public int AmmoLeft => Mathf.Max(0, maxBulletsAlive - Bullet.CountOwnedBy(gameObject));
        public int AmmoMax => maxBulletsAlive;
        /// <summary>0 — только что выстрелил, 1 — перезарядка закончилась.</summary>
        public float Reload01 => fireCooldown <= 0f ? 1f : Mathf.Clamp01(1f - (_nextFire - Time.time) / fireCooldown);
        /// <summary>ЛКМ сейчас выстрелит.</summary>
        public bool CanFire => AmmoLeft > 0 && Time.time >= _nextFire;

        // ----- для HUD: магазин пулемёта -----
        public int MinigunRounds => Mathf.Max(0, _minigunRounds);
        public int MinigunMagazine => minigunMagazine;
        public bool MinigunReloading => _minigunReloadEnd > 0f;
        /// <summary>Прогресс перезарядки магазина 0…1 (1 — не перезаряжается).</summary>
        public float MinigunReload01 => !MinigunReloading || minigunReloadTime <= 0f ? 1f
            : Mathf.Clamp01(1f - (_minigunReloadEnd - Time.time) / minigunReloadTime);

        /// <summary>Выстрел не получился (лимит снарядов, нет зарядов ульты) — для звука «пустого щелчка».</summary>
        public static event System.Action FireBlocked;
        public static void RaiseFireBlocked() => FireBlocked?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { FireBlocked = null; MinigunReloadStarted = null; MinigunReloaded = null; }

        /// <summary>Магазин пулемёта начал перезаряжаться / перезарядился — для звука.</summary>
        public static event System.Action MinigunReloadStarted, MinigunReloaded;

        /// <summary>Куда смотрит ствол.</summary>
        public Vector2 AimDirection => turret != null ? (Vector2)turret.right : (Vector2)transform.right;

        /// <summary>Откуда вылетает снаряд.</summary>
        public Vector2 MuzzlePosition
        {
            get
            {
                Vector2 pivot = turret == null ? (Vector2)transform.position
                              : turret.parent != null ? (Vector2)turret.parent.TransformPoint(_turretLocal) // без учёта отдачи
                              : (Vector2)turret.position;
                return pivot + AimDirection * muzzleDistance;
            }
        }

        void Awake()
        {
            _rb = Prims.AddTopDownBody(gameObject);
            _rb.freezeRotation = true; // столкновения не крутят танк, поворачиваем сами
            if (turret != null) _turretLocal = turret.localPosition;
            _minigunRounds = minigunMagazine;
        }

        void Update()
        {
            if (Time.timeScale == 0f) { _move = Vector2.zero; return; } // пауза или экран победы
            _move = GameInput.Move();

            // башня → на курсор
            var cam = Camera.main;
            if (cam != null && turret != null)
            {
                Vector3 mouse = cam.ScreenToWorldPoint(GameInput.MouseScreen());
                Vector2 aim = (Vector2)mouse - (Vector2)turret.position;
                if (aim.sqrMagnitude > 0.001f)
                    turret.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            }

            UpdateMinigunReload();
            int alive = Bullet.CountOwnedBy(gameObject);

            if (GameInput.FirePressed() && Time.time >= _nextFire && alive < maxBulletsAlive)
            {
                _nextFire = Time.time + fireCooldown;
                Bullet.Spawn(MuzzlePosition, AimDirection, gameObject, bulletColor, bulletSpeed, maxBounces, bulletPrefab, bulletSprite);
                _recoil = recoilDistance;
            }
            else if (GameInput.FirePressed() && alive >= maxBulletsAlive) RaiseFireBlocked(); // «пустой щелчок»
            else if (minigunEnabled && GameInput.AltFireHeld() && Time.time >= _nextMinigunFire
                     && _minigunRounds > 0 && !MinigunReloading && alive < Mathf.Min(minigunMaxBullets, maxBulletsAlive))
            {
                _nextMinigunFire = Time.time + minigunCooldown;
                _lastMinigunShot = Time.time;
                if (--_minigunRounds <= 0) StartMinigunReload(); // магазин пуст — перезарядка
                var b = Bullet.Spawn(MuzzlePosition, AimDirection, gameObject, minigunColor, minigunSpeed, minigunBounces,
                                     minigunBulletPrefab != null ? minigunBulletPrefab : bulletPrefab, bulletSprite);
                b.damage = minigunDamage;
                _recoil = Mathf.Max(_recoil, minigunRecoil);
            }
        }

        void UpdateMinigunReload()
        {
            if (MinigunReloading)
            {
                if (Time.time >= _minigunReloadEnd) { _minigunReloadEnd = 0f; _minigunRounds = minigunMagazine; MinigunReloaded?.Invoke(); }
                return;
            }
            // ПКМ отпущена, магазин неполный — дозаряжаем, чтобы не держать игрока с одним патроном
            if (_minigunRounds < minigunMagazine && !GameInput.AltFireHeld() && Time.time - _lastMinigunShot >= minigunIdleReload)
                StartMinigunReload();
        }

        void StartMinigunReload()
        {
            _minigunReloadEnd = Time.time + Mathf.Max(0.01f, minigunReloadTime);
            MinigunReloadStarted?.Invoke();
        }

        // отдача: башня откатывается назад вдоль ствола и пружинит обратно (визуально; ствол и прицел считаются от исходного места)
        void LateUpdate()
        {
            if (turret == null) return;
            _recoil = Mathf.MoveTowards(_recoil, 0f, _recoil * recoilReturn * Time.deltaTime + 0.01f * Time.deltaTime);
            turret.localPosition = _turretLocal;
            if (_recoil > 0.0001f) turret.position -= (Vector3)(AimDirection * _recoil);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (tankControls)
            {
                _rb.rotation = _rb.rotation - _move.x * turnSpeed * dt;
                Vector2 fwd = transform.right;
                _rb.MovePosition(_rb.position + fwd * (_move.y * moveSpeed * dt));
            }
            else
            {
                Vector2 m = Vector2.ClampMagnitude(_move, 1f);
                if (m.sqrMagnitude > 0.01f)
                {
                    float target = Mathf.Atan2(m.y, m.x) * Mathf.Rad2Deg;
                    _rb.rotation = Mathf.MoveTowardsAngle(_rb.rotation, target, turnSpeed * 2f * dt);
                }
                _rb.MovePosition(_rb.position + m * (moveSpeed * dt));
            }
        }
    }
}
