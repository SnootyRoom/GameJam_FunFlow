// Строит уровень на стандартном Unity Tilemap:
//   Grid
//    ├─ Floor  (Tilemap)                       — пол, без физики
//    ├─ Walls  (Tilemap + TilemapCollider2D + CompositeCollider2D) — стены
//    └─ Cover  (Tilemap + TilemapCollider2D + CompositeCollider2D) — укрытия
// Картинка и физика берутся из одних и тех же тайлов — источник правды один (LevelData → Tilemap).
// Игрок, враги и снаряды сюда не входят: их создаёт EntitySpawner внутри Root.
using System.Collections.Generic;
using CatTanks.Level;
using UnityEngine;
using UnityEngine.Tilemaps;
using CellType = CatTanks.Level.Tile;          // наш тип клетки: Floor / Wall / Cover
using UTile = UnityEngine.Tilemaps.Tile;       // тайл Unity

namespace CatTanks
{
    public class LevelBuilder : MonoBehaviour
    {
        /// <summary>Чем рисовать один слой. Используется первое заданное: tile → sprite → color.</summary>
        [System.Serializable]
        public class TileLayer
        {
            [Tooltip("Готовый тайл-ассет: Tile, Rule Tile, Animated Tile. Для стен поставь у ассета Collider Type = Grid")]
            public TileBase tile;
            [Tooltip("Или просто спрайт — тайл создастся автоматически и подгонится под клетку")]
            public Sprite sprite;
            [Tooltip("Оттенок спрайта (белый = оригинал) или цвет заглушки, если нет ни тайла, ни спрайта")]
            public Color color = Color.white;
            [Tooltip("Необязательно: материал для TilemapRenderer (например, Sprite-Lit для 2D-света)")]
            public Material material;
        }

        [Header("Тайлы")]
        public TileLayer floor = new TileLayer { color = new Color(0.86f, 0.84f, 0.79f) };
        public TileLayer walls = new TileLayer { color = new Color(0.27f, 0.28f, 0.31f) };
        public TileLayer cover = new TileLayer { color = new Color(0.55f, 0.40f, 0.24f) };

        [Header("Tilemap в сцене (пусто = создадутся сами при запуске)")]
        [Tooltip("Создать их в сцене можно через меню компонента: ⋮ → «Создать Grid и Tilemap в сцене»")]
        public Tilemap floorMap;
        public Tilemap wallMap;
        public Tilemap coverMap;

        [Header("Точки A и Б (пусто = примитивы)")]
        public GameObject startPrefab;
        public GameObject exitPrefab;
        public Color startColor = new Color(0.2f, 0.7f, 0.3f, 0.5f);
        public Color exitColor = new Color(1f, 0.8f, 0.1f, 0.7f);

        [Header("Отладка")]
        public bool drawGizmos = true;

        /// <summary>Контейнер объектов уровня (A, Б, игрок, враги, снаряды). Пересоздаётся на каждый уровень.</summary>
        public Transform Root { get; private set; }
        public ExitZone Exit { get; private set; }
        public LevelData Data { get; private set; }

        readonly List<UTile> _runtimeTiles = new List<UTile>();
        bool _warnedPpu;

        const int FloorOrder = -10, WallOrder = 0, CoverOrder = 1;

        // ------------------------------------------------------------------ API

        /// <summary>Заполняет Tilemap по данным уровня и создаёт точки A и Б. Возвращает Root для объектов уровня.</summary>
        public Transform Build(LevelData d)
        {
            Clear();
            Data = d;
            EnsureTilemaps();
            FillTiles(d);

            // Root — строго в (0,0,0) и без родителя, чтобы координаты объектов совпадали с сеткой
            Root = new GameObject("LevelObjects").transform;
            Root.position = Vector3.zero;

            Marker(startPrefab, "Start", LevelGrid.CellToWorld(d.start), startColor);
            var exit = Marker(exitPrefab, "Exit", LevelGrid.CellToWorld(d.exit), exitColor);
            if (exit.GetComponent<Collider2D>() == null) exit.AddComponent<BoxCollider2D>().size = Vector2.one * 0.8f;
            exit.GetComponent<Collider2D>().isTrigger = true;
            Exit = exit.GetComponent<ExitZone>();
            if (Exit == null) Exit = exit.AddComponent<ExitZone>();

            return Root;
        }

        public void Clear()
        {
            if (Root != null) DestroySafe(Root.gameObject);
            Root = null;
            Exit = null;
            Data = null;
            if (floorMap != null) floorMap.ClearAllTiles();
            if (wallMap != null) wallMap.ClearAllTiles();
            if (coverMap != null) coverMap.ClearAllTiles();
            foreach (var t in _runtimeTiles) if (t != null) DestroySafe(t);
            _runtimeTiles.Clear();
        }

        // ------------------------------------------------------------------ Tilemap

        void FillTiles(LevelData d)
        {
            var floorTile = ResolveTile(floor, UTile.ColliderType.None);
            var wallTile = ResolveTile(walls, UTile.ColliderType.Grid);
            var coverTile = ResolveTile(cover, UTile.ColliderType.Grid);

            var floorPos = new List<Vector3Int>();
            var wallPos = new List<Vector3Int>();
            var coverPos = new List<Vector3Int>();
            for (int x = 0; x < d.width; x++)
                for (int y = 0; y < d.height; y++)
                {
                    var p = new Vector3Int(x, y, 0);
                    floorPos.Add(p); // пол под всем, чтобы под стенами не было дыр
                    if (d.tiles[x, y] == CellType.Wall) wallPos.Add(p);
                    else if (d.tiles[x, y] == CellType.Cover) coverPos.Add(p);
                }

            SetAll(floorMap, floorPos, floorTile);
            SetAll(wallMap, wallPos, wallTile);
            SetAll(coverMap, coverPos, coverTile);
        }

        static void SetAll(Tilemap map, List<Vector3Int> positions, TileBase tile)
        {
            var tiles = new TileBase[positions.Count];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
            map.SetTiles(positions.ToArray(), tiles); // одним вызовом — быстро и коллайдер пересобирается один раз
        }

        /// <summary>Тайл-ассет как есть; спрайт или цвет — превращаем в тайл на лету.</summary>
        TileBase ResolveTile(TileLayer layer, UTile.ColliderType collider)
        {
            if (layer.tile != null)
            {
                WarnIfWrongSize(layer.tile);
                return layer.tile;
            }

            var t = ScriptableObject.CreateInstance<UTile>();
            t.name = "RuntimeTile";
            t.hideFlags = HideFlags.DontSave;
            t.sprite = layer.sprite != null ? layer.sprite : Prims.Square;
            t.color = layer.color;
            t.colliderType = collider;     // Grid = коллайдер ровно на всю клетку, отскоки точные
            t.flags = TileFlags.LockAll;

            // Спрайт 64 px при PPU 100 занимает 0.64 клетки — масштабируем тайл ровно до 1×1
            Vector2 size = t.sprite.bounds.size;
            if (size.x > 0.001f && size.y > 0.001f)
                t.transform = Matrix4x4.Scale(new Vector3(1f / size.x, 1f / size.y, 1f));

            _runtimeTiles.Add(t);
            return t;
        }

        void WarnIfWrongSize(TileBase tb)
        {
            var t = tb as UTile;
            if (_warnedPpu || t == null || t.sprite == null) return;
            Vector2 s = t.sprite.bounds.size;
            if (Mathf.Abs(s.x - 1f) > 0.01f || Mathf.Abs(s.y - 1f) > 0.01f)
            {
                _warnedPpu = true;
                Debug.LogWarning($"[LevelBuilder] Спрайт тайла «{t.name}» занимает {s.x:0.##}×{s.y:0.##} клетки вместо 1×1. " +
                                 $"Поставь у картинки Pixels Per Unit = ширине картинки в пикселях (для 64×64 — 64).", t);
            }
        }

        /// <summary>Если Tilemap не назначены — создаём Grid и три слоя сами.</summary>
        void EnsureTilemaps()
        {
            if (floorMap != null && wallMap != null && coverMap != null)
            {
                SetupLayer(floorMap, floor, FloorOrder, false);
                SetupLayer(wallMap, walls, WallOrder, true);
                SetupLayer(coverMap, cover, CoverOrder, true);
                return;
            }
            CreateGrid();
        }

        void CreateGrid()
        {
            var grid = new GameObject("LevelGrid", typeof(Grid));
            grid.transform.position = Vector3.zero;
            grid.GetComponent<Grid>().cellSize = Vector3.one;
            floorMap = CreateLayer(grid.transform, "Floor", floor, FloorOrder, false);
            wallMap = CreateLayer(grid.transform, "Walls", walls, WallOrder, true);
            coverMap = CreateLayer(grid.transform, "Cover", cover, CoverOrder, true);
        }

        Tilemap CreateLayer(Transform grid, string name, TileLayer layer, int order, bool solid)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(grid, false);
            var map = go.GetComponent<Tilemap>();
            SetupLayer(map, layer, order, solid);
            return map;
        }

        /// <summary>Порядок отрисовки, материал и физика слоя. Недостающие компоненты добавляются.</summary>
        static void SetupLayer(Tilemap map, TileLayer layer, int order, bool solid)
        {
            map.transform.localPosition = Vector3.zero;
            var r = map.GetComponent<TilemapRenderer>();
            if (r == null) r = map.gameObject.AddComponent<TilemapRenderer>();
            r.sortingOrder = order;
            if (layer.material != null) r.sharedMaterial = layer.material;
            if (!solid) return;

            // Стандартная связка Unity: TilemapCollider2D собирает коллайдер из тайлов,
            // CompositeCollider2D сливает его в цельный контур без швов — снаряды отскакивают ровно.
            var rb = map.GetComponent<Rigidbody2D>();
            if (rb == null) rb = map.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var composite = map.GetComponent<CompositeCollider2D>();
            if (composite == null) composite = map.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

            var tc = map.GetComponent<TilemapCollider2D>();
            if (tc == null) tc = map.gameObject.AddComponent<TilemapCollider2D>();
#if UNITY_2023_1_OR_NEWER
            tc.compositeOperation = Collider2D.CompositeOperation.Merge;
#else
            tc.usedByComposite = true;
#endif
        }

        GameObject Marker(GameObject prefab, string name, Vector2 pos, Color color)
        {
            if (prefab != null) return Instantiate(prefab, pos, Quaternion.identity, Root);
            return Prims.Create(name, Root, Prims.Square, color, pos, Vector2.one * 1.6f, -5);
        }

        static void DestroySafe(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        // ------------------------------------------------------------------ Редактор

        /// <summary>Создаёт Grid с тремя Tilemap в сцене и привязывает их к этому компоненту.</summary>
        [ContextMenu("Создать Grid и Tilemap в сцене")]
        void CreateGridInScene()
        {
            if (floorMap != null && wallMap != null && coverMap != null)
            {
                Debug.Log("[LevelBuilder] Tilemap уже назначены.");
                return;
            }
            CreateGrid();
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(floorMap.transform.parent.gameObject, "Create Level Grid");
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>Генерирует уровень прямо в редакторе, без запуска игры — посмотреть, как лягут тайлы.</summary>
        [ContextMenu("Предпросмотр уровня в редакторе")]
        void PreviewInEditor()
        {
            var flow = GetComponent<GameFlow>();
            var config = flow != null ? flow.config : null;
            var settings = config != null ? config.generation : new LevelSettings();
            int difficulty = config != null ? config.startDifficulty : 1;
            Clear();
            Data = BspLevelGenerator.Generate(settings, settings.seed, difficulty);
            EnsureTilemaps();
            FillTiles(Data);
            Debug.Log($"[LevelBuilder] Предпросмотр: сид {Data.seed}. Перед запуском можно не очищать — уровень пересоберётся.");
        }

        [ContextMenu("Очистить предпросмотр")]
        void ClearPreview() { Clear(); }

        void OnDrawGizmos()
        {
            if (!drawGizmos || Data == null) return;
            Gizmos.color = new Color(0.2f, 0.6f, 1f);
            for (int i = 1; i < Data.mainPath.Count; i++)
                Gizmos.DrawLine(LevelGrid.CellToWorld(Data.mainPath[i - 1]), LevelGrid.CellToWorld(Data.mainPath[i]));
            for (int i = 0; i < Data.leaves.Count; i++)
            {
                var l = Data.leaves[i];
                Gizmos.color = i == Data.restLeafIndex ? Color.green : new Color(1f, 1f, 1f, 0.25f);
                Gizmos.DrawWireCube(new Vector3(l.x + l.w / 2f, l.y + l.h / 2f), new Vector3(l.w, l.h));
            }
        }
    }
}
