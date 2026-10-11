// Строит СЦЕНУ уровня по LevelData: пол, стены, укрытия, общий коллайдер, A и Б.
// Не знает про игрока, врагов, номер уровня и UI — этим занимаются EntitySpawner и GameFlow.
using System.Collections.Generic;
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public class LevelBuilder : MonoBehaviour
    {
        /// <summary>
        /// Внешний вид одной поверхности (пол, стены или укрытия). Используется первое заданное:
        /// префаб → спрайт → материал → просто цвет.
        /// </summary>
        [System.Serializable]
        public class SurfaceLook
        {
            [Tooltip("Префаб на каждую клетку. Только картинка, без коллайдера")]
            public GameObject prefab;
            [Tooltip("Спрайт, который повторяется плиткой — одна картинка на клетку. Импорт: Mesh Type = Full Rect, Wrap Mode = Repeat")]
            public Sprite sprite;
            [Tooltip("Материал (.mat). Его текстура повторяется по 1 разу на клетку (см. tilesPerCell). Wrap Mode текстуры = Repeat")]
            public Material material;
            [Tooltip("Сколько повторов текстуры (спрайта или материала) на одну клетку")]
            public float tilesPerCell = 1f;
            [Tooltip("Оттенок спрайта или префаба. Белый = оригинальные цвета художника")]
            public Color tint = Color.white;
            [Tooltip("Цвет заглушки, когда ни префаб, ни спрайт, ни материал не заданы")]
            public Color color = Color.white;
        }

        [Tooltip("Подгонять спрайты и префабы ровно под клетку 1×1, даже если у картинки другой Pixels Per Unit")]
        public bool fitToCell = true;

        [Header("Внешний вид")]
        public SurfaceLook floor = new SurfaceLook { color = new Color(0.86f, 0.84f, 0.79f) };
        public SurfaceLook walls = new SurfaceLook { color = new Color(0.27f, 0.28f, 0.31f) };
        public SurfaceLook cover = new SurfaceLook { color = new Color(0.55f, 0.40f, 0.24f) };

        [Header("Точки A и Б (пусто = примитивы)")]
        public GameObject startPrefab;
        public GameObject exitPrefab;
        public Color startColor = new Color(0.2f, 0.7f, 0.3f, 0.5f);
        public Color exitColor = new Color(1f, 0.8f, 0.1f, 0.7f);

        [Header("Отладка")]
        public bool drawGizmos = true;

        public Transform Root { get; private set; }
        public ExitZone Exit { get; private set; }
        public LevelData Data { get; private set; }

        readonly List<Mesh> _meshes = new List<Mesh>();

        // ------------------------------------------------------------------ API

        /// <summary>Строит уровень и возвращает его корневой объект. Предыдущий уровень удаляется.</summary>
        public Transform Build(LevelData d)
        {
            Clear();
            Data = d;
            Root = new GameObject("Level").transform;
            Root.SetParent(transform, false);

            BuildSurface("Floor", floor, -10, new List<GridRect> { new GridRect(0, 0, d.width, d.height) },
                         (x, y) => d.tiles[x, y] == Tile.Floor);
            BuildSurface("Walls", walls, 0, GridMerge.Merge(d.width, d.height, (x, y) => d.tiles[x, y] == Tile.Wall),
                         (x, y) => d.tiles[x, y] == Tile.Wall);
            BuildSurface("Cover", cover, 1, GridMerge.Merge(d.width, d.height, (x, y) => d.tiles[x, y] == Tile.Cover),
                         (x, y) => d.tiles[x, y] == Tile.Cover);
            BuildWallColliders(d);

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
            if (Root != null) Destroy(Root.gameObject);
            foreach (var m in _meshes) if (m != null) Destroy(m);
            _meshes.Clear();
            Root = null;
            Exit = null;
            Data = null;
        }

        // ------------------------------------------------------------------ Поверхности

        /// <summary>
        /// Префаб — по объекту на клетку (cellFilter). Спрайт, материал и цвет — один объект на прямоугольник,
        /// текстура в нём повторяется плиткой. Так объектов в десятки раз меньше.
        /// </summary>
        void BuildSurface(string name, SurfaceLook look, int order, List<GridRect> rects, System.Func<int, int, bool> cellFilter)
        {
            var parent = new GameObject(name).transform;
            parent.SetParent(Root, false);

            if (look.prefab != null)
            {
                foreach (var r in rects)
                    for (int x = r.x; x < r.xMax; x++)
                        for (int y = r.y; y < r.yMax; y++)
                        {
                            if (!cellFilter(x, y)) continue;
                            var go = Instantiate(look.prefab, LevelGrid.CellToWorld(new GridPos(x, y)), Quaternion.identity, parent);
                            // физика стен — только в общем коллайдере, иначе вернутся швы и кривые отскоки
                            foreach (var col in go.GetComponentsInChildren<Collider2D>()) Destroy(col);
                            PreparePrefabInstance(go, look, order);
                        }
                return;
            }

            foreach (var r in rects)
            {
                Vector3 center = new Vector3(r.x + r.w / 2f, r.y + r.h / 2f, 0f);
                if (look.sprite != null)
                {
                    var go = new GameObject(name + "Tile");
                    go.transform.SetParent(parent, false);
                    go.transform.position = center;
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = look.sprite;
                    sr.drawMode = SpriteDrawMode.Tiled;
                    sr.tileMode = SpriteTileMode.Continuous;
                    // Плитка в режиме Tiled равна размеру спрайта в юнитах (64 px при PPU 100 = 0.64).
                    // Масштабируем объект так, чтобы одна плитка = 1 / tilesPerCell клетки при любом PPU.
                    Vector2 tile = look.sprite.bounds.size;
                    Vector2 scale = fitToCell && tile.x > 0f && tile.y > 0f
                        ? new Vector2(1f / (tile.x * look.tilesPerCell), 1f / (tile.y * look.tilesPerCell))
                        : Vector2.one;
                    go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
                    sr.size = new Vector2(r.w / scale.x, r.h / scale.y);
                    sr.color = look.tint;
                    sr.sortingOrder = order;
                    if (look.material != null) sr.sharedMaterial = look.material; // например, материал Sprite-Lit для 2D-света
                }
                else if (look.material != null)
                {
                    var go = new GameObject(name + "Quad");
                    go.transform.SetParent(parent, false);
                    go.transform.position = center;
                    go.AddComponent<MeshFilter>().sharedMesh = MakeQuad(r.w, r.h, look.tilesPerCell);
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = look.material;
                    mr.sortingOrder = order;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
                else
                {
                    Prims.Create(name + "Rect", parent, Prims.Square, look.color, center, new Vector2(r.w, r.h), order);
                }
            }
        }

        /// <summary>
        /// Подгоняет префаб клетки: размер ровно 1×1 (если fitToCell), порядок отрисовки слоя, оттенок.
        /// Без этого спрайт 64 px при стандартном PPU 100 выходит 0.64 клетки и между плитками видны щели.
        /// </summary>
        void PreparePrefabInstance(GameObject go, SurfaceLook look, int order)
        {
            var renderers = go.GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length == 0) return;

            if (fitToCell)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                if (b.size.x > 0.001f && b.size.y > 0.001f)
                {
                    Vector3 s = go.transform.localScale;
                    go.transform.localScale = new Vector3(s.x / b.size.x, s.y / b.size.y, s.z);
                }
            }

            foreach (var sr in renderers)
            {
                sr.sortingOrder += order; // пол всегда под стенами, укрытия над стенами
                if (look.tint != Color.white) sr.color *= look.tint;
            }
        }

        /// <summary>Квад w×h юнитов, UV растянуты так, что текстура повторяется tilesPerCell раз на клетку.</summary>
        Mesh MakeQuad(int w, int h, float tilesPerCell)
        {
            float hw = w / 2f, hh = h / 2f, u = w * tilesPerCell, v = h * tilesPerCell;
            var m = new Mesh { name = "LevelQuad" };
            m.vertices = new[] { new Vector3(-hw, -hh), new Vector3(-hw, hh), new Vector3(hw, hh), new Vector3(hw, -hh) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, v), new Vector2(u, v), new Vector2(u, 0) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            _meshes.Add(m);
            return m;
        }

        // ------------------------------------------------------------------ Коллайдеры

        /// <summary>
        /// Стены уже слиты генератором в прямоугольники. CompositeCollider2D объединяет их в цельный контур
        /// без внутренних швов, поэтому снаряд не «ловит» стык двух коллайдеров и отскакивает правильно.
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

        GameObject Marker(GameObject prefab, string name, Vector2 pos, Color color)
        {
            if (prefab != null) return Instantiate(prefab, pos, Quaternion.identity, Root);
            return Prims.Create(name, Root, Prims.Square, color, pos, Vector2.one * 1.6f, -5);
        }

        // ------------------------------------------------------------------ Гизмо

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
