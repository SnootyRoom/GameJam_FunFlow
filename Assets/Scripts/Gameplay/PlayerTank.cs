// Тестовый танк игрока: корпус ездит, башня смотрит на курсор, ЛКМ — выстрел.
// Заготовка для разработчика игрока: можно переписать целиком. Важно только, чтобы на объекте
// был Health с team = Player, Collider2D и Rigidbody2D (см. README). ILevelActor — по желанию.
// Заглушка из примитивов — Placeholders.Player.
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

        [Header("Стрельба")]
        public Transform turret;
        public float muzzleDistance = 0.6f;
        public float bulletSpeed = 8f;
        public int maxBounces = 1;
        public float fireCooldown = 0.3f;
        public int maxBulletsAlive = 5;
        public GameObject bulletPrefab;
        public Color bulletColor = new Color(1f, 0.95f, 0.6f);

        Rigidbody2D _rb;
        float _nextFire;
        Vector2 _move;

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
                Vector2 aim = (Vector2)mouse - (Vector2)transform.position;
                if (aim.sqrMagnitude > 0.001f)
                    turret.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            }

            if (GameInput.FirePressed() && Time.time >= _nextFire && Bullet.CountOwnedBy(gameObject) < maxBulletsAlive)
            {
                _nextFire = Time.time + fireCooldown;
                Vector2 dir = turret != null ? (Vector2)turret.right : (Vector2)transform.right;
                Bullet.Spawn((Vector2)transform.position + dir * muzzleDistance, dir, gameObject, bulletColor,
                             bulletSpeed, maxBounces, bulletPrefab);
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
