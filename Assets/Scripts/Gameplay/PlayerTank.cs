// Танк игрока: корпус ездит, башня смотрит на курсор, ЛКМ — основной выстрел с рикошетом, ПКМ (зажать) — пулемёт.
// Заготовка для разработчика игрока: можно переписать целиком. Важно только, чтобы на объекте
// был Health с team = Player, Collider2D и Rigidbody2D (см. README). ILevelActor — по желанию.
// Рядом живут отдельные компоненты: AimPreview (линия прицела), TurretRecoil (отдача, добавится сам),
// TrackScroller/TrackTrail (гусеницы), PlayerUltimate (орех). Магазин пулемёта — класс Magazine.
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
        [Tooltip("Урон основного снаряда. Здоровье врагов: клещ 2, жук 4, краб 10 — основной выстрел убивает клеща, пулемёт — нет")]
        public int bulletDamage = 2;
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
        [Tooltip("Размер пули пулемёта относительно основного снаряда")]
        public float minigunBulletScale = 0.6f;
        public GameObject minigunBulletPrefab;
        public Color minigunColor = new Color(0.85f, 0.45f, 0.2f);

        Rigidbody2D _rb;
        TurretRecoil _recoil;
        float _nextFire, _nextMinigunFire;
        Vector2 _move;

        // ----- события для звука -----
        /// <summary>Выстрел не получился (лимит снарядов, нет зарядов ульты) — для звука «пустого щелчка».</summary>
        public static event System.Action FireBlocked;
        public static void RaiseFireBlocked() => FireBlocked?.Invoke();
        /// <summary>Магазин пулемёта начал перезаряжаться / перезарядился.</summary>
        public static event System.Action MinigunReloadStarted, MinigunReloaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { FireBlocked = null; MinigunReloadStarted = null; MinigunReloaded = null; }

        // ----- для HUD -----
        /// <summary>Сколько снарядов можно выпустить прямо сейчас (лимит в полёте минус уже летящие).</summary>
        public int AmmoLeft => Mathf.Max(0, maxBulletsAlive - Bullet.CountOwnedBy(gameObject));
        public int AmmoMax => maxBulletsAlive;
        /// <summary>0 — только что выстрелил, 1 — перезарядка закончилась.</summary>
        public float Reload01 => fireCooldown <= 0f ? 1f : Mathf.Clamp01(1f - (_nextFire - Time.time) / fireCooldown);
        /// <summary>ЛКМ сейчас выстрелит.</summary>
        public bool CanFire => AmmoLeft > 0 && Time.time >= _nextFire;
        /// <summary>Магазин пулемёта (патроны, перезарядка).</summary>
        public Magazine Minigun { get; private set; }

        /// <summary>Куда смотрит ствол.</summary>
        public Vector2 AimDirection => turret != null ? (Vector2)turret.right : (Vector2)transform.right;

        /// <summary>Откуда вылетает снаряд — от места покоя башни, без учёта отдачи.</summary>
        public Vector2 MuzzlePosition
        {
            get
            {
                Vector2 pivot = turret == null ? (Vector2)transform.position
                              : _recoil != null && _recoil.HasRest && turret.parent != null ? (Vector2)turret.parent.TransformPoint(_recoil.RestLocalPosition)
                              : (Vector2)turret.position;
                return pivot + AimDirection * muzzleDistance;
            }
        }

        void Awake()
        {
            _rb = Prims.AddTopDownBody(gameObject);
            _rb.freezeRotation = true; // столкновения не крутят танк, поворачиваем сами
            _recoil = GetComponent<TurretRecoil>();
            if (_recoil == null) _recoil = gameObject.AddComponent<TurretRecoil>();

            Minigun = new Magazine(minigunMagazine, minigunReloadTime, minigunIdleReload);
            Minigun.ReloadStarted += () => MinigunReloadStarted?.Invoke();
            Minigun.Reloaded += () => MinigunReloaded?.Invoke();
        }

        void Update()
        {
            if (Time.timeScale == 0f) { _move = Vector2.zero; return; } // пауза или экран победы
            _move = GameInput.Move();
            AimTurret();

            Minigun.Configure(minigunMagazine, minigunReloadTime, minigunIdleReload); // правки в инспекторе — сразу
            Minigun.Tick(GameInput.AltFireHeld());

            int alive = Bullet.CountOwnedBy(gameObject);
            if (GameInput.FirePressed())
            {
                if (Time.time >= _nextFire && alive < maxBulletsAlive) FireMain();
                else if (alive >= maxBulletsAlive) RaiseFireBlocked(); // «пустой щелчок»
            }
            else if (minigunEnabled && GameInput.AltFireHeld() && Time.time >= _nextMinigunFire
                     && alive < Mathf.Min(minigunMaxBullets, maxBulletsAlive) && Minigun.TryShoot())
                FireMinigun();
        }

        void AimTurret()
        {
            var cam = Camera.main;
            if (cam == null || turret == null) return;
            Vector3 mouse = cam.ScreenToWorldPoint(GameInput.MouseScreen());
            Vector2 aim = (Vector2)mouse - (Vector2)turret.position;
            if (aim.sqrMagnitude > 0.001f)
                turret.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        }

        void FireMain()
        {
            _nextFire = Time.time + fireCooldown;
            Bullet.Spawn(MuzzlePosition, AimDirection, gameObject, bulletColor, bulletSpeed, maxBounces, bulletPrefab, bulletSprite,
                         1f, bulletDamage);
        }

        void FireMinigun()
        {
            _nextMinigunFire = Time.time + minigunCooldown;
            Bullet.Spawn(MuzzlePosition, AimDirection, gameObject, minigunColor, minigunSpeed, minigunBounces,
                         minigunBulletPrefab != null ? minigunBulletPrefab : bulletPrefab, bulletSprite,
                         minigunBulletScale, minigunDamage);
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
