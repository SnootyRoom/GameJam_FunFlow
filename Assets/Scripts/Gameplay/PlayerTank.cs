using UnityEngine;
using System.Collections.Generic;

namespace CatTanks
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(LineRenderer))]
    public class PlayerTank : MonoBehaviour
    {
        public bool tankControls = false;
        public float moveSpeed = 3.5f;
        public float turnSpeed = 180f;

        // Эту переменную НЕ НУЖНО назначать в инспекторе, код сам всё сделает.
        public Transform turret;
        public float muzzleDistance = 0.6f;

        [Header("Основное оружие (ЛКМ)")]
        public float bulletSpeed = 8f;
        public int maxBounces = 1;
        public float fireCooldown = 0.3f;
        public int maxBulletsAlive = 5;
        public GameObject bulletPrefab;
        public Color bulletColor = new Color(1f, 0.95f, 0.6f);

        [Header("Пулемёт (ПКМ — зажим)")]
        public float minigunSpeed = 10f;
        public int minigunBounces = 0;
        public float minigunCooldown = 0.12f;
        public int minigunMaxBullets = 4;
        public Color minigunColor = new Color(0.85f, 0.45f, 0.2f);
        public int minigunDamage = 1;

        public float simulateDistance = 15f;
        public float bulletRadiusForSimulation = 0.15f;

        Rigidbody2D _rb;
        LineRenderer _lineRenderer;
        float _nextFire;
        float _nextMinigunFire;
        Vector2 _move;
        readonly List<Vector2> _simPoints = new List<Vector2>();

        void Awake()
        {
            _rb = Prims.AddTopDownBody(gameObject);
            _rb.freezeRotation = true;

            _lineRenderer = GetComponent<LineRenderer>();
            ConfigureLineRenderer();

            // --- БЛОК НАДЕЖНОЙ НАСТРОЙКИ БАШНИ ---
            // 1. Пытаемся найти существующую башню среди детей
            Transform existingTurret = transform.Find("Turret");
            if (existingTurret != null)
            {
                turret = existingTurret;
                Debug.Log("PlayerTank: Нашли существующую башню.");
            }
            else
            {
                // 2. Если её нет (или она None в коде), создаем новую.
                Debug.LogWarning("PlayerTank: Башня не найдена или сброшена! Создаю новую.");
                SetupNewTurret();
            }
        }

        // Вынесли создание башни в отдельный метод, чтобы вызывать его и при сбое
        void SetupNewTurret()
        {
            // Создаем чистый GameObject
            GameObject turretGO = new GameObject("Turret");
            turret = turretGO.transform;
            turret.SetParent(transform, false);
            turret.localPosition = Vector3.zero;

            // Наполняем его примитивами
            Prims.Child("Barrel", turret, Prims.Square, new Color(0.2f, 0.35f, 0.2f), new Vector2(0.35f, 0), new Vector2(0.5f, 0.14f), 12);
            Prims.Child("CatHead", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), Vector2.zero, Vector2.one * 0.42f, 13);
            Prims.Child("EarL", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), new Vector2(-0.12f, 0.17f), Vector2.one * 0.14f, 13);
            Prims.Child("EarR", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), new Vector2(-0.12f, -0.17f), Vector2.one * 0.14f, 13);
        }

        void ConfigureLineRenderer()
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.startWidth = 0.08f;
                _lineRenderer.endWidth = 0.03f;

                var defMaterial = Resources.GetBuiltinResource<Material>("Sprites-Default.mat");
                if (defMaterial != null)
                {
                    _lineRenderer.material = defMaterial;
                }

                _lineRenderer.startColor = new Color(1f, 1f, 0.5f, 0.9f);
                _lineRenderer.endColor = new Color(1f, 0.3f, 0.3f, 0.5f);

                _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _lineRenderer.receiveShadows = false;
            }
        }

        public static GameObject CreatePrimitive(Transform parent, Vector3 pos)
        {
            var go = Prims.Create("PlayerTank", parent, Prims.Square, new Color(0.30f, 0.55f, 0.30f), pos, Vector2.one * 0.8f, 10);
            go.tag = "Player";

            Prims.Child("TrackL", go.transform, Prims.Square, new Color(0.15f, 0.2f, 0.15f), new Vector2(0, 0.45f), new Vector2(1.05f, 0.18f), 11);
            Prims.Child("TrackR", go.transform, Prims.Square, new Color(0.15f, 0.2f, 0.15f), new Vector2(0, -0.45f), new Vector2(1.05f, 0.18f), 11);

            // Обрати внимание: мы НЕ создаем башню здесь, мы создаем её ТОЛЬКО в Awake() компонента,
            // чтобы быть уверенными, что она будет создана только один раз при старте танка,
            // независимо от того, как он был создан (префаб или Prims).

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;
            var hp = go.AddComponent<Health>();
            hp.maxHp = 3;
            hp.isPlayer = true;
            hp.ResetHp();

            go.AddComponent<LineRenderer>();

            // Назначаем компонент, но НЕ НАЗНАЧАЕМ turret. Оставим это для Awake().
            go.AddComponent<PlayerTank>();

            return go;
        }

        void Update()
        {
            _move = GameInput.Move();

            Camera cam = Camera.main;
            if (cam == null) cam = FindObjectOfType<Camera>();

            // --- ДОПОЛНИТЕЛЬНАЯ ЗАЩИТА ---
            // Если башня по какой-то причине пропала (стала None) во время Update,
            // пробуем найти её снова или создать новую, чтобы предотвратить сбой прицеливания.
            if (turret == null)
            {
                Debug.LogWarning("PlayerTank: Башня пропала во время игры! Восстанавливаю.");
                SetupNewTurret();
            }

            Vector2 muzzlePos = (Vector2)transform.position;
            Vector2 dir = transform.right;

            // Теперь мы гарантированно имеем башню. Направляем её за мышью.
            if (cam != null && turret != null)
            {
                Vector3 mouse = cam.ScreenToWorldPoint(GameInput.MouseScreen());
                Vector2 aim = (Vector2)mouse - (Vector2)transform.position;
                if (aim.sqrMagnitude > 0.001f)
                {
                    float targetAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                    turret.rotation = Quaternion.Euler(0, 0, targetAngle);
                }

                dir = turret.right;
                muzzlePos = (Vector2)turret.position + dir * muzzleDistance;
            }
            else
            {
                muzzlePos = (Vector2)transform.position + dir * muzzleDistance;
            }

            UpdateTrajectory(muzzlePos, dir);

            // ЛКМ — Одиночный мощный выстрел
            if (GameInput.FirePressed() && Time.time >= _nextFire && Bullet.CountOwnedBy(gameObject) < maxBulletsAlive)
            {
                _nextFire = Time.time + fireCooldown;
                Bullet.Spawn(muzzlePos, dir, gameObject, bulletColor, bulletSpeed, maxBounces, bulletPrefab);
            }

            // ПКМ (зажим) — Сбалансированный пулемёт
            if (GameInput.MinigunPressed() && Time.time >= _nextMinigunFire && Bullet.CountOwnedBy(gameObject) < minigunMaxBullets)
            {
                _nextMinigunFire = Time.time + minigunCooldown;
                var b = Bullet.Spawn(muzzlePos, dir, gameObject, minigunColor, minigunSpeed, minigunBounces, bulletPrefab);
                if (b != null)
                {
                    b.damage = minigunDamage;
                }
            }
        }

        void UpdateTrajectory(Vector2 origin, Vector2 dir)
        {
            if (_lineRenderer == null) return;

            _simPoints.Clear();
            Ricochet.Simulate(origin, dir, bulletRadiusForSimulation, maxBounces, simulateDistance,
                              shooter: transform, target: null, points: _simPoints);

            if (_simPoints != null && _simPoints.Count > 1)
            {
                _lineRenderer.enabled = true;
                _lineRenderer.positionCount = _simPoints.Count;
                for (int i = 0; i < _simPoints.Count; i++)
                {
                    _lineRenderer.SetPosition(i, new Vector3(_simPoints[i].x, _simPoints[i].y, 0f));
                }
            }
            else
            {
                _lineRenderer.enabled = false;
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

        void OnGUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 12;
            style.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
            style.alignment = TextAnchor.LowerCenter;

            string info = "WASD — Движение  |  ЛКМ — Одиночный выстрел  |  ПКМ (зажим) — Пулемёт";
            Rect rect = new Rect(0, Screen.height - 30, Screen.width, 25);
            GUI.Label(rect, info, style);
        }
    }
}