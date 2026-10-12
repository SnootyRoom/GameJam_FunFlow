// Собирает сцену из LevelData: пол, стены, единый коллайдер стен без швов, A, Б, игрок, враги.
// Повесь на пустой объект в сцене и нажми Play. Префабы можно не задавать — будут примитивы.
using System;
using System.Collections.Generic;
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public class LevelBuilder : MonoBehaviour
    {
        public static LevelBuilder Instance { get; private set; }

        [Header("Генерация")]
        public LevelSettings settings = new LevelSettings();
        public bool generateOnStart = true;
        public bool debugKeys = true;          // R — новый уровень, N — следующий
        public float restartDelay = 1.5f;

        [Header("Префабы (пусто = примитивы). У префабов стен НЕ должно быть коллайдеров")]
        public GameObject floorPrefab;
        public GameObject wallPrefab;
        public GameObject coverPrefab;
        public GameObject startPrefab;
        public GameObject exitPrefab;
        public GameObject playerPrefab;
        public GameObject crawlerPrefab;
        public GameObject shooterPrefab;
        public GameObject crabPrefab;

        [Header("Цвета примитивов")]
        public Color floorColor = new Color(0.86f, 0.84f, 0.79f);
        public Color wallColor = new Color(0.27f, 0.28f, 0.31f);
        public Color coverColor = new Color(0.55f, 0.40f, 0.24f);
        public Color startColor = new Color(0.2f, 0.7f, 0.3f, 0.5f);
        public Color exitColor = new Color(1f, 0.8f, 0.1f, 0.7f);
        public Color crawlerColor = new Color(0.85f, 0.25f, 0.25f);
        public Color shooterColor = new Color(0.6f, 0.25f, 0.8f);
        public Color crabColor = new Color(0.95f, 0.55f, 0.1f);

        [Header("Отладка")]
        public bool drawGizmos = true;

        public LevelData Data { get; private set; }
        public GridPathfinder Pathfinder { get; private set; }
        public GameObject Player { get; private set; }
        public Transform Root { get; private set; }
        public int LevelNumber { get; private set; } = 1;

        /// <summary>Уровень собран (подпишись, чтобы обновить UI, музыку и т.д.).</summary>
        public event Action<LevelData> OnLevelBuilt;
        /// <summary>Игрок доехал до Б.</summary>
        public event Action OnLevelCompleted;
        /// <summary>Игрок погиб.</summary>
        public event Action OnPlayerKilled;

        readonly List<GameObject> _enemies = new List<GameObject>();
        bool _transition;

        // ------------------------------------------------------------------ Жизненный цикл

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (generateOnStart) Generate();
        }

        void Update()
        {
            if (!debugKeys) return;
            if (GameInput.RegeneratePressed()) { settings.seed = 0; Generate(); }
            if (GameInput.NextLevelPressed()) CompleteLevel();
        }

        /// <summary>Сгенерировать и собрать уровень по текущим settings.</summary>
        public void Generate()
        {
            CancelInvoke();
            _transition = false;
            Clear();
            Data = BspLevelGenerator.Generate(settings);
            Pathfinder = new GridPathfinder(Data);
            Build();
            Debug.Log($"[LevelBuilder] Уровень {LevelNumber}: сид {Data.seed}, сложность {settings.difficulty}, " +
                      $"зон {Data.leaves.Count}, врагов {Data.enemies.Count}, коллайдеров стен {Data.wallRects.Count}");
            OnLevelBuilt?.Invoke(Data);
        }

        /// <summary>Пройти уровень: сложность +1, новый сид.</summary>
        public void CompleteLevel()
        {
            if (_transition) return;
            _transition = true;
            OnLevelCompleted?.Invoke();
            LevelNumber++;
            settings.difficulty = Mathf.Min(settings.difficulty + 1, 5);
            settings.seed = 0;
            Generate();
        }

        /// <summary>Вызывается из Health игрока.</summary>
        public void OnPlayerDied()
        {
            if (_transition) return;
            _transition = true;
            OnPlayerKilled?.Invoke();
            Invoke(nameof(RestartLevel), restartDelay);
        }

        /// <summary>Перезапуск того же уровня (тот же сид).</summary>
        public void RestartLevel()
        {
            if (Data != null) settings.seed = Data.seed;
            Generate();
            settings.seed = 0;
        }

        public int AliveEnemyCount()
        {
            _enemies.RemoveAll(e => e == null);
            return _enemies.Count;
        }

        // ------------------------------------------------------------------ Координаты

        /// <summary>Центр клетки в мире. Клетка (0,0) — левый нижний угол арены, 1 клетка = 1 юнит.</summary>
        public static Vector2 CellToWorld(GridPos p) => new Vector2(p.x + 0.5f, p.y + 0.5f);
        public static GridPos WorldToCell(Vector2 w) => new GridPos(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y));

        // ------------------------------------------------------------------ Сборка

        void Clear()
        {
            if (Root != null) Destroy(Root.gameObject);
            Root = null;
            Player = null;
            _enemies.Clear();
        }

        void Build()
        {
            Root = new GameObject("Level").transform;
            Root.SetParent(transform, false);
            var d = Data;

            BuildFloor(d);
            BuildWallVisuals(d);
            BuildWallColliders(d);

            // A
            Spawn(startPrefab, "Start", CellToWorld(d.start), Prims.Square, startColor, Vector2.one * 1.6f, -5);

            // Б
            var exit = Spawn(exitPrefab, "Exit", CellToWorld(d.exit), Prims.Square, exitColor, Vector2.one * 1.6f, -5);
            if (exit.GetComponent<Collider2D>() == null)
            {
                var c = exit.AddComponent<BoxCollider2D>();
                c.size = Vector2.one * 0.8f; // в локальных единицах примитива 1×1 → 1.3 юнита
            }
            exit.GetComponent<Collider2D>().isTrigger = true;
            if (exit.GetComponent<ExitZone>() == null) exit.AddComponent<ExitZone>();

            // Игрок
            Vector2 sp = CellToWorld(d.start);
            Player = playerPrefab != null
                ? Instantiate(playerPrefab, sp, Quaternion.identity, Root)
                : PlayerTank.CreatePrimitive(Root, sp);
            Player.tag = "Player";

            // Враги
            foreach (var e in d.enemies) _enemies.Add(SpawnEnemy(e));

            SetupCamera();
        }

        void BuildFloor(LevelData d)
        {
            if (floorPrefab == null)
            {
                // один большой спрайт вместо тысячи клеток
                Prims.Create("Floor", Root, Prims.Square, floorColor, new Vector3(d.width / 2f, d.height / 2f, 0),
                             new Vector2(d.width, d.height), -10);
                return;
            }
            var parent = new GameObject("Floor").transform;
            parent.SetParent(Root, false);
            for (int x = 0; x < d.width; x++)
                for (int y = 0; y < d.height; y++)
                    if (d.tiles[x, y] == Tile.Floor)
                        Instantiate(floorPrefab, CellToWorld(new GridPos(x, y)), Quaternion.identity, parent);
        }

        void BuildWallVisuals(LevelData d)
        {
            var parent = new GameObject("WallVisuals").transform;
            parent.SetParent(Root, false);
            for (int x = 0; x < d.width; x++)
                for (int y = 0; y < d.height; y++)
                {
                    var t = d.tiles[x, y];
                    if (t == Tile.Floor) continue;
                    var prefab = t == Tile.Cover && coverPrefab != null ? coverPrefab : wallPrefab;
                    Vector2 pos = CellToWorld(new GridPos(x, y));
                    GameObject go;
                    if (prefab != null)
                    {
                        go = Instantiate(prefab, pos, Quaternion.identity, parent);
                        // физика стен — только в общем коллайдере, иначе вернутся швы и кривые отскоки
                        foreach (var col in go.GetComponentsInChildren<Collider2D>()) Destroy(col);
                    }
                    else
                    {
                        Prims.Create(t == Tile.Cover ? "Cover" : "Wall", parent, Prims.Square,
                                     t == Tile.Cover ? coverColor : wallColor, pos, Vector2.one, 0);
                    }
                }
        }

        /// <summary>
        /// Стены уже слиты генератором в прямоугольники. Дополнительно объединяем их
        /// CompositeCollider2D — получается цельный контур без внутренних швов,
        /// поэтому снаряд не «ловит» стык двух коллайдеров и отскакивает правильно.
        /// </summary>
        void BuildWallColliders(LevelData d)
        {
            var go = new GameObject("WallColliders");
            go.transform.SetParent(Root, false);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var composite = go.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Manual;

            foreach (var r in d.wallRects)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.offset = new Vector2(r.x + r.w / 2f, r.y + r.h / 2f);
                box.size = new Vector2(r.w, r.h);
#if UNITY_2023_1_OR_NEWER
                box.compositeOperation = Collider2D.CompositeOperation.Merge;
#else
                box.usedByComposite = true;
#endif
            }
            composite.GenerateGeometry();
        }

        GameObject SpawnEnemy(EnemySpawn e)
        {
            Vector2 pos = CellToWorld(e.pos);
            switch (e.type)
            {
                case EnemyType.Crawler:
                    return crawlerPrefab != null ? Instantiate(crawlerPrefab, pos, Quaternion.identity, Root)
                                                 : MakeCrawler(pos, 0.7f, 1, 2.2f);
                case EnemyType.Shooter:
                    return shooterPrefab != null ? Instantiate(shooterPrefab, pos, Quaternion.identity, Root)
                                                 : MakeShooter(pos);
                default: // Crab — занимает 2×2 клетки, центр на их стыке
                    pos += new Vector2(0.5f, 0.5f);
                    return crabPrefab != null ? Instantiate(crabPrefab, pos, Quaternion.identity, Root)
                                              : MakeCrab(pos);
            }
        }

        GameObject MakeCrawler(Vector2 pos, float size, int hp, float speed)
        {
            var go = Prims.Create("Crawler", Root, Prims.Square, crawlerColor, pos, Vector2.one * size, 8);
            Prims.Child("Screen", go.transform, Prims.Square, new Color(0.1f, 0.15f, 0.25f), Vector2.zero, new Vector2(0.6f, 0.75f), 9);
            for (int i = -1; i <= 1; i++) // лапки
            {
                Prims.Child("LegL", go.transform, Prims.Square, Prims.Dark(crawlerColor, 0.7f), new Vector2(i * 0.3f, 0.55f), new Vector2(0.1f, 0.3f), 7);
                Prims.Child("LegR", go.transform, Prims.Square, Prims.Dark(crawlerColor, 0.7f), new Vector2(i * 0.3f, -0.55f), new Vector2(0.1f, 0.3f), 7);
            }
            go.AddComponent<BoxCollider2D>().size = Vector2.one * 0.9f;
            var h = go.AddComponent<Health>(); h.maxHp = hp; h.invulnerableAfterHit = 0.05f; h.ResetHp();
            var c = go.AddComponent<EnemyCrawler>(); c.speed = speed;
            return go;
        }

        GameObject MakeShooter(Vector2 pos)
        {
            var go = Prims.Create("Shooter", Root, Prims.Square, shooterColor, pos, Vector2.one * 0.8f, 8);
            Prims.Child("Screen", go.transform, Prims.Square, new Color(0.1f, 0.15f, 0.25f), Vector2.zero, new Vector2(0.6f, 0.75f), 9);
            var turret = new GameObject("Turret").transform;
            turret.SetParent(go.transform, false);
            turret.localScale = new Vector3(1f / 0.8f, 1f / 0.8f, 1f);
            Prims.Child("Stinger", turret, Prims.Square, Prims.Dark(shooterColor, 0.6f), new Vector2(0.4f, 0), new Vector2(0.4f, 0.14f), 10);
            Prims.Child("Camera", turret, Prims.Circle, new Color(0.9f, 0.9f, 1f), new Vector2(0.6f, 0), Vector2.one * 0.16f, 11);
            go.AddComponent<BoxCollider2D>().size = Vector2.one * 0.95f;
            var h = go.AddComponent<Health>(); h.maxHp = 2; h.invulnerableAfterHit = 0.05f; h.ResetHp();
            var s = go.AddComponent<EnemyShooter>(); s.turret = turret; s.bodyRadius = 0.45f;
            return go;
        }

        GameObject MakeCrab(Vector2 pos)
        {
            var go = MakeCrawler(pos, 1.7f, 5, 1.0f);
            go.name = "Crab";
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
                if (sr.gameObject == go) sr.color = crabColor;
            var crawler = go.GetComponent<EnemyCrawler>();
            crawler.aggroRange = 6f;   // страж: не уходит далеко от Б, пока игрок не подъедет
            crawler.hearRange = 3f;
            var turret = new GameObject("Turret").transform;
            turret.SetParent(go.transform, false);
            turret.localScale = new Vector3(1f / 1.7f, 1f / 1.7f, 1f);
            Prims.Child("Claw", turret, Prims.Square, Prims.Dark(crabColor, 0.6f), new Vector2(1.0f, 0), new Vector2(0.5f, 0.25f), 10);
            var s = go.AddComponent<EnemyShooter>();
            s.turret = turret; s.bodyRadius = 0.9f; s.fireInterval = 1.6f; s.maxBounces = 2;
            go.GetComponent<Health>().CacheRenderers(); // перекрасили и добавили клешню после создания Health
            return go;
        }

        GameObject Spawn(GameObject prefab, string name, Vector2 pos, Sprite sprite, Color color, Vector2 scale, int order)
        {
            if (prefab != null) return Instantiate(prefab, pos, Quaternion.identity, Root);
            return Prims.Create(name, Root, sprite, color, pos, scale, order);
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.backgroundColor = wallColor * 0.6f;
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.Snap();
        }

        // ------------------------------------------------------------------ Гизмо

        void OnDrawGizmos()
        {
            if (!drawGizmos || Data == null) return;
            Gizmos.color = new Color(0.2f, 0.6f, 1f);
            for (int i = 1; i < Data.mainPath.Count; i++)
                Gizmos.DrawLine(CellToWorld(Data.mainPath[i - 1]), CellToWorld(Data.mainPath[i]));
            for (int i = 0; i < Data.leaves.Count; i++)
            {
                var l = Data.leaves[i];
                Gizmos.color = i == Data.restLeafIndex ? Color.green : new Color(1f, 1f, 1f, 0.25f);
                Gizmos.DrawWireCube(new Vector3(l.x + l.w / 2f, l.y + l.h / 2f), new Vector3(l.w, l.h));
            }
        }
    }
}
