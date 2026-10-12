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
        [Tooltip("Пулемёт стреляет, только пока в полёте меньше стольких снарядов — чтобы не забивал лимит основного")]
        public int minigunMaxBullets = 4;
        public int minigunDamage = 1;
        public GameObject minigunBulletPrefab;
        public Color minigunColor = new Color(0.85f, 0.45f, 0.2f);

        Rigidbody2D _rb;
        float _nextFire, _nextMinigunFire;
        Vector2 _move;

        /// <summary>Выстрел не получился (лимит снарядов, нет зарядов ульты) — для звука «пустого щелчка».</summary>
        public static event System.Action FireBlocked;
        public static void RaiseFireBlocked() => FireBlocked?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => FireBlocked = null;

        /// <summary>Куда смотрит ствол.</summary>
        public Vector2 AimDirection => turret != null ? (Vector2)turret.right : (Vector2)transform.right;

        /// <summary>Откуда вылетает снаряд.</summary>
        public Vector2 MuzzlePosition
        {
            get
            {
                Vector2 pivot = turret != null ? (Vector2)turret.position : (Vector2)transform.position;
                return pivot + AimDirection * muzzleDistance;
            }
        }

        void Awake()
        {
            _rb = Prims.AddTopDownBody(gameObject);
            _rb.freezeRotation = true; // столкновения не крутят танк, поворачиваем сами
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

            int alive = Bullet.CountOwnedBy(gameObject);

            if (GameInput.FirePressed() && Time.time >= _nextFire && alive < maxBulletsAlive)
            {
                _nextFire = Time.time + fireCooldown;
                Bullet.Spawn(MuzzlePosition, AimDirection, gameObject, bulletColor, bulletSpeed, maxBounces, bulletPrefab, bulletSprite);
            }
            else if (GameInput.FirePressed() && alive >= maxBulletsAlive) RaiseFireBlocked(); // «пустой щелчок»
            else if (minigunEnabled && GameInput.AltFireHeld() && Time.time >= _nextMinigunFire
                     && alive < Mathf.Min(minigunMaxBullets, maxBulletsAlive))
            {
                _nextMinigunFire = Time.time + minigunCooldown;
                var b = Bullet.Spawn(MuzzlePosition, AimDirection, gameObject, minigunColor, minigunSpeed, minigunBounces,
                                     minigunBulletPrefab != null ? minigunBulletPrefab : bulletPrefab, bulletSprite);
                b.damage = minigunDamage;
            }
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
