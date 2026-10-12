// Собирает геометрию уровня из LevelData: пол, стены, единый коллайдер стен без швов, старт, рыбка, секреты.
// Игрока и врагов создаёт GameManager (через PlayerFactory/EnemyFactory) — здесь только мир.
// Префабы можно не задавать — будут примитивы.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public class LevelBuilder : MonoBehaviour
    {
        [Header("Префабы (пусто = примитивы). У префабов стен НЕ должно быть коллайдеров")]
        public GameObject floorPrefab;
        public GameObject wallPrefab;
        public GameObject coverPrefab;
        public GameObject startPrefab;
        public GameObject exitPrefab;        // рыбка — цель уровня
        public GameObject secretFishPrefab;

        [Header("Цвета примитивов")]
        public Color floorColor = new Color(0.93f, 0.89f, 0.80f);
        public Color wallColor = new Color(0.36f, 0.39f, 0.50f);
        public Color coverColor = new Color(0.72f, 0.52f, 0.32f);
        public Color startColor = new Color(0.3f, 0.8f, 0.45f, 0.55f);
        public Color exitColor = new Color(1f, 0.78f, 0.15f, 1f);
        public Color secretColor = new Color(0.35f, 0.8f, 1f, 1f);
        [Tooltip("Лёгкий оттенок пола для каждой зоны — помогает ориентироваться")]
        public Color[] roomTints =
        {
            new Color(1f, 1f, 1f), new Color(0.96f, 1f, 0.95f), new Color(1f, 0.96f, 0.93f),
            new Color(0.95f, 0.97f, 1f), new Color(1f, 0.97f, 1f),
        };

        [Header("Отладка")]
        public bool drawGizmos = true;

        public LevelData Data { get; private set; }
        public GridPathfinder Pathfinder { get; private set; }
        public Transform Root { get; private set; }
        public Vector2 PlayerSpawn => Data != null ? CellToWorld(Data.start) : Vector2.zero;
        public Vector2 ExitPosition => Data != null ? CellToWorld(Data.exit) : Vector2.zero;

        GameObject _wallVisuals, _wallColliders;

        // ------------------------------------------------------------------ Жизненный цикл

        void Awake() { Game.Level = this; }
        void OnDestroy() { if (Game.Level == this) Game.Level = null; }

        /// <summary>LevelBuilder из сцены, а если его нет — новый.</summary>
        public static LevelBuilder EnsureExists()
        {
            if (Game.Level != null) return Game.Level;
            var lb = FindAnyObjectByType<LevelBuilder>();
            if (lb == null) lb = new GameObject("LevelBuilder").AddComponent<LevelBuilder>();
            Game.Level = lb;
            return lb;
        }

        /// <summary>Удалить старый уровень, сгенерировать и собрать новый по settings.</summary>
        public void Build(LevelSettings settings)
        {
            Clear();
            Data = BspLevelGenerator.Generate(settings);
            Pathfinder = new GridPathfinder(Data);

            Root = new GameObject("Level").transform;
            Root.SetParent(transform, false);
            var d = Data;

            BuildFloor(d);
            BuildWallVisuals(d);
            BuildWallColliders(d);
            Spawn(startPrefab, "Start", CellToWorld(d.start), Prims.Ring, startColor, Vector2.one * 1.8f, -8);
            BuildExit(d);
            foreach (var p in d.secrets) BuildSecret(p);

            Debug.Log($"[LevelBuilder] сид {d.seed}, {d.width}×{d.height}, зон {d.leaves.Count}, " +
                      $"врагов {d.enemies.Count}, коллайдеров стен {d.wallRects.Count}");
        }

        public void Clear()
        {
            if (Root != null)
            {
                // выключаем сразу: OnDisable уберёт врагов и снаряды из реестров в этом же кадре
                Root.gameObject.SetActive(false);
                Destroy(Root.gameObject);
            }
            Root = null;
        }

        // ------------------------------------------------------------------ Разрушение

        /// <summary>
        /// Снести стены и укрытия в радиусе (внешняя рамка арены неразрушима). Перестраивает визуал и коллайдеры.
        /// A* читает сетку напрямую, так что враги сразу начинают ходить через пролом. Возвращает число снесённых клеток.
        /// </summary>
        public int DestroyCells(Vector2 center, float radius)
        {
            var d = Data;
            if (d == null) return 0;
            int changed = 0;
            int x0 = Mathf.Max(1, Mathf.FloorToInt(center.x - radius)), x1 = Mathf.Min(d.width - 2, Mathf.CeilToInt(center.x + radius));
            int y0 = Mathf.Max(1, Mathf.FloorToInt(center.y - radius)), y1 = Mathf.Min(d.height - 2, Mathf.CeilToInt(center.y + radius));
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    if (d.tiles[x, y] == Tile.Floor) continue;
                    Vector2 c = CellToWorld(new GridPos(x, y));
                    if ((c - center).sqrMagnitude > radius * radius) continue;
                    var col = d.tiles[x, y] == Tile.Cover ? coverColor : wallColor;
                    d.tiles[x, y] = Tile.Floor;
                    changed++;
                    FxParticles.Burst(c, 4, col, speed: 5f, size: 0.28f, life: 0.8f, sprite: Prims.Square, spin: true);
                }
            if (changed == 0) return 0;

            d.wallRects = WallMerger.Merge(d);
            if (_wallVisuals != null) Destroy(_wallVisuals);
            if (_wallColliders != null) { _wallColliders.SetActive(false); Destroy(_wallColliders); }
            BuildWallVisuals(d);
            BuildWallColliders(d);
            return changed;
        }

        // ------------------------------------------------------------------ Координаты

        /// <summary>Центр клетки в мире. Клетка (0,0) — левый нижний угол арены, 1 клетка = 1 юнит.</summary>
        public static Vector2 CellToWorld(GridPos p) => new Vector2(p.x + 0.5f, p.y + 0.5f);
        public static GridPos WorldToCell(Vector2 w) => new GridPos(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y));

        // ------------------------------------------------------------------ Сборка

        void BuildExit(LevelData d)
        {
            GameObject exit;
            if (exitPrefab != null) exit = Instantiate(exitPrefab, CellToWorld(d.exit), Quaternion.identity, Root);
            else
            {
                exit = new GameObject("Fish");
                exit.transform.SetParent(Root, false);
                exit.transform.position = CellToWorld(d.exit);
                Prims.Child("Pad", exit.transform, Prims.Circle, Prims.WithAlpha(exitColor, 0.25f), Vector2.zero, Vector2.one * 1.9f, -8);
                Prims.Child("Glow", exit.transform, Prims.Glow, Prims.WithAlpha(exitColor, 0.6f), Vector2.zero, Vector2.one * 3.2f, -5);
                Prims.Child("Icon", exit.transform, Prims.Fish, exitColor, Vector2.zero, Vector2.one * 1.2f, 6);
            }
            if (exit.GetComponent<Collider2D>() == null) exit.AddComponent<CircleCollider2D>().radius = 0.6f;
            exit.GetComponent<Collider2D>().isTrigger = true;
            if (exit.GetComponent<ExitZone>() == null) exit.AddComponent<ExitZone>();
        }

        void BuildSecret(GridPos p)
        {
            GameObject go;
            if (secretFishPrefab != null) go = Instantiate(secretFishPrefab, CellToWorld(p), Quaternion.identity, Root);
            else
            {
                go = new GameObject("SecretFish");
                go.transform.SetParent(Root, false);
                go.transform.position = CellToWorld(p);
                Prims.Child("Glow", go.transform, Prims.Glow, Prims.WithAlpha(secretColor, 0.5f), Vector2.zero, Vector2.one * 1.8f, -5);
                Prims.Child("Icon", go.transform, Prims.Fish, secretColor, Vector2.zero, Vector2.one * 0.6f, 6);
            }
            if (go.GetComponent<Collider2D>() == null) go.AddComponent<CircleCollider2D>().radius = 0.6f;
            go.GetComponent<Collider2D>().isTrigger = true;
            var fish = go.GetComponent<SecretFish>();
            if (fish == null) fish = go.AddComponent<SecretFish>();
            fish.value = Game.Config.fishPerSecret;
        }

        void BuildFloor(LevelData d)
        {
            if (floorPrefab == null)
            {
                // одна текстура «пиксель = клетка»: шахматка + свой оттенок у каждой зоны
                var tex = new Texture2D(d.width, d.height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "FloorTex",
                };
                var px = new Color32[d.width * d.height];
                for (int y = 0; y < d.height; y++)
                    for (int x = 0; x < d.width; x++)
                    {
                        Color c = floorColor;
                        for (int i = 0; i < d.leaves.Count; i++)
                            if (d.leaves[i].Contains(new GridPos(x, y))) { c *= roomTints[i % roomTints.Length]; break; }
                        if (((x + y) & 1) == 0) c = Prims.Dark(c, 0.965f);
                        c.a = 1f;
                        px[y * d.width + x] = c;
                    }
                tex.SetPixels32(px);
                tex.Apply();
                var sprite = Sprite.Create(tex, new Rect(0, 0, d.width, d.height), Vector2.zero, 1f);
                var go = Prims.Create("Floor", Root, sprite, Color.white, Vector3.zero, Vector2.one, -10);
                go.transform.position = Vector3.zero;
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
            _wallVisuals = parent.gameObject;

            if (wallPrefab != null || coverPrefab != null)
            {
                for (int x = 0; x < d.width; x++)
                    for (int y = 0; y < d.height; y++)
                    {
                        var t = d.tiles[x, y];
                        if (t == Tile.Floor) continue;
                        var prefab = t == Tile.Cover && coverPrefab != null ? coverPrefab : wallPrefab;
                        Vector2 pos = CellToWorld(new GridPos(x, y));
                        if (prefab != null)
                        {
                            var go = Instantiate(prefab, pos, Quaternion.identity, parent);
                            // физика стен — только в общем коллайдере, иначе вернутся швы и кривые отскоки
                            foreach (var col in go.GetComponentsInChildren<Collider2D>()) Destroy(col);
                        }
                        else
                        {
                            Prims.Create(t == Tile.Cover ? "Cover" : "Wall", parent, Prims.Square,
                                         t == Tile.Cover ? coverColor : wallColor, pos, Vector2.one, 0);
                        }
                    }
                return;
            }

            // Примитивы «под 3D»: тень на полу → верх стены → тёмная передняя грань там, где под стеной пол.
            // Рисуем слитыми прямоугольниками — несколько сотен спрайтов вместо тысяч.
            foreach (var r in d.wallRects)
            {
                bool cover = d.tiles[r.x, r.y] == Tile.Cover;
                var size = new Vector2(r.w, r.h);
                var center = new Vector2(r.x + r.w / 2f, r.y + r.h / 2f);
                Prims.Create("Shadow", parent, Prims.Square, new Color(0f, 0f, 0f, 0.18f), center + new Vector2(0.14f, -0.2f), size, -9);
                Prims.Create(cover ? "Cover" : "Wall", parent, Prims.Square, cover ? coverColor : wallColor, center, size, 0);
            }
            const float face = 0.3f;
            for (int y = 1; y < d.height; y++)
                for (int x = 0; x < d.width; x++)
                {
                    if (!IsFaceCell(d, x, y)) continue;
                    var t = d.tiles[x, y];
                    int run = 1;
                    while (x + run < d.width && IsFaceCell(d, x + run, y) && d.tiles[x + run, y] == t) run++;
                    var col = Prims.Dark(t == Tile.Cover ? coverColor : wallColor, 0.68f);
                    Prims.Create("Face", parent, Prims.Square, col, new Vector2(x + run / 2f, y + face / 2f), new Vector2(run, face), 1);
                    // светлая кромка сверху грани — читается как ребро
                    Prims.Create("Edge", parent, Prims.Square, Prims.Light(t == Tile.Cover ? coverColor : wallColor, 0.18f),
                                 new Vector2(x + run / 2f, y + face + 0.03f), new Vector2(run, 0.06f), 1);
                    x += run - 1;
                }
        }

        static bool IsFaceCell(LevelData d, int x, int y) => d.tiles[x, y] != Tile.Floor && d.tiles[x, y - 1] == Tile.Floor;

        /// <summary>
        /// Стены уже слиты генератором в прямоугольники. Дополнительно объединяем их
        /// CompositeCollider2D — получается цельный контур без внутренних швов,
        /// поэтому снаряд не «ловит» стык двух коллайдеров и отскакивает правильно.
        /// </summary>
        void BuildWallColliders(LevelData d)
        {
            var go = new GameObject("WallColliders");
            go.transform.SetParent(Root, false);
            _wallColliders = go;
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

        GameObject Spawn(GameObject prefab, string name, Vector2 pos, Sprite sprite, Color color, Vector2 scale, int order)
        {
            if (prefab != null) return Instantiate(prefab, pos, Quaternion.identity, Root);
            return Prims.Create(name, Root, sprite, color, pos, scale, order);
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
