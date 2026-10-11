// Тестовый танк игрока: корпус ездит, башня смотрит на курсор, ЛКМ — выстрел.
// Если у тебя в команде танк пишет другой человек — можно выкинуть, генератору он не нужен.
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

        /// <summary>Танк из примитивов: корпус, гусеницы, башня с «котом».</summary>
        public static GameObject CreatePrimitive(Transform parent, Vector3 pos)
        {
            var go = Prims.Create("PlayerTank", parent, Prims.Square, new Color(0.30f, 0.55f, 0.30f), pos, Vector2.one * 0.8f, 10);
            go.tag = "Player";
            Prims.Child("TrackL", go.transform, Prims.Square, new Color(0.15f, 0.2f, 0.15f), new Vector2(0, 0.45f), new Vector2(1.05f, 0.18f), 11);
            Prims.Child("TrackR", go.transform, Prims.Square, new Color(0.15f, 0.2f, 0.15f), new Vector2(0, -0.45f), new Vector2(1.05f, 0.18f), 11);

            // башня — отдельный объект без масштаба корпуса, чтобы вращалась независимо
            var turret = new GameObject("Turret").transform;
            turret.SetParent(go.transform, false);
            turret.localScale = new Vector3(1f / 0.8f, 1f / 0.8f, 1f);
            Prims.Child("Barrel", turret, Prims.Square, new Color(0.2f, 0.35f, 0.2f), new Vector2(0.35f, 0), new Vector2(0.5f, 0.14f), 12);
            Prims.Child("Cat", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), Vector2.zero, Vector2.one * 0.42f, 13);
            Prims.Child("EarL", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), new Vector2(-0.12f, 0.17f), Vector2.one * 0.14f, 13);
            Prims.Child("EarR", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), new Vector2(-0.12f, -0.17f), Vector2.one * 0.14f, 13);

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f; // ×0.8 масштаба = 0.4 юнита, танк пролезает в проход шириной 1 клетка
            var hp = go.AddComponent<Health>();
            hp.maxHp = 3;
            hp.isPlayer = true;
            hp.ResetHp();
            var tank = go.AddComponent<PlayerTank>();
            tank.turret = turret;
            return go;
        }

        void Update()
        {
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
