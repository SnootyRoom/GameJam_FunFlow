// Базовые типы генератора уровней.
// Этот файл НЕ зависит от UnityEngine — ядро можно тестировать вне Unity.
using System;
using System.Collections.Generic;

namespace CatTanks.Level
{
    /// <summary>Тип клетки карты.</summary>
    public enum Tile : byte
    {
        Floor = 0,  // пол, по нему ездят
        Wall = 1,   // внешняя стена или стена между зонами
        Cover = 2,  // укрытие внутри зоны (физически это тоже стена)
    }

    /// <summary>Где генератор ставит врага.</summary>
    public enum SpawnPlacement
    {
        Anywhere,   // где угодно (с перевесом ближе к выходу)
        NearCover,  // рядом с укрытием — для стрелков
        GuardExit,  // страж в 3–6 клетках от выхода, ставится первым
    }

    /// <summary>
    /// Правило появления врага. Генератор не знает про конкретных врагов — только про правила;
    /// сами правила берутся из EnemyDefinition (Unity-слой), id = имя ассета врага.
    /// </summary>
    [Serializable]
    public class EnemySpawnRule
    {
        public string id;
        public SpawnPlacement placement = SpawnPlacement.Anywhere;
        public float weight = 1f;          // относительная частота среди обычных врагов
        public int minDifficulty = 1;      // с какой сложности появляется
        public int maxPerLevel = 0;        // 0 — без ограничения (для стражей 0 = один)
        public int footprint = 1;          // сколько клеток занимает по стороне

        /// <summary>Набор по умолчанию — если правила не переданы (тесты, предпросмотр без спавнера).</summary>
        public static List<EnemySpawnRule> Defaults() => new List<EnemySpawnRule>
        {
            new EnemySpawnRule { id = "Enemy_Crawler" },
            new EnemySpawnRule { id = "Enemy_Shooter", placement = SpawnPlacement.NearCover },
            new EnemySpawnRule { id = "Enemy_Crab", placement = SpawnPlacement.GuardExit, minDifficulty = 2, maxPerLevel = 1, footprint = 2 },
        };
    }

    /// <summary>Координата клетки на сетке.</summary>
    [Serializable]
    public struct GridPos : IEquatable<GridPos>
    {
        public int x, y;
        public GridPos(int x, int y) { this.x = x; this.y = y; }

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.x + b.x, a.y + b.y);
        public static bool operator ==(GridPos a, GridPos b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(GridPos a, GridPos b) => !(a == b);
        public bool Equals(GridPos o) => x == o.x && y == o.y;
        public override bool Equals(object obj) => obj is GridPos o && Equals(o);
        public override int GetHashCode() => (x * 73856093) ^ (y * 19349663);
        public override string ToString() => $"({x},{y})";
    }

    /// <summary>Прямоугольник клеток: [x, x+w) × [y, y+h).</summary>
    [Serializable]
    public struct GridRect
    {
        public int x, y, w, h;
        public GridRect(int x, int y, int w, int h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        public int xMax => x + w;   // не включительно
        public int yMax => y + h;   // не включительно
        public GridPos Center => new GridPos(x + w / 2, y + h / 2);
        public bool Contains(GridPos p) => p.x >= x && p.x < xMax && p.y >= y && p.y < yMax;
        public override string ToString() => $"[{x},{y} {w}x{h}]";
    }

    [Serializable]
    public struct EnemySpawn
    {
        public GridPos pos;         // для больших врагов — левая нижняя клетка площадки
        public string id;           // id правила = имя ассета врага
        public int footprint;
        public EnemySpawn(GridPos p, string id, int footprint) { pos = p; this.id = id; this.footprint = footprint; }
    }

    /// <summary>
    /// Настройки генерации. Все поля публичные, чтобы Unity показывала их в инспекторе.
    /// Размеры — в клетках (1 клетка = 1 юнит = 64 px).
    /// </summary>
    [Serializable]
    public class LevelSettings
    {
        public int seed = 0;               // сид первого уровня для отладки; 0 = случайный (генератор сид получает параметром)

        // --- Арена и BSP ---
        public int width = 40;
        public int height = 28;
        public int minLeafSize = 8;        // минимальный размер зоны (внутренняя часть)
        public int maxDepth = 4;           // глубина деления: 3 → до 8 зон, 4 → до 16

        // --- Проходы ---
        public int doorWidth = 3;          // ширина прохода между зонами, клеток
        public int extraLoops = 2;         // лишние проходы сверх дерева = выбор маршрута
        public float openWallChance = 0.2f;// шанс убрать стену между зонами целиком

        // --- Укрытия ---
        public int minCoverPerLeaf = 1;
        public int maxCoverPerLeaf = 3;
        public int coverClearance = 1;     // минимум свободных клеток между укрытием и стенами
        public int doorKeepClear = 2;      // радиус «не ставить укрытия» у проходов

        // --- Точки A и Б ---
        public int startExitClearRadius = 2;

        // --- Враги ---
        public int baseEnemyCount = 4;
        public int enemiesPerDifficulty = 2;   // + столько врагов за каждую ступень сложности выше первой
        public int minEnemyDistanceFromStart = 10; // в шагах по полу
        public int minEnemySpacing = 3;
        public bool spawnGuards = true;    // ставить стражей у точки Б (враги с правилом GuardExit)
        public bool restLeaf = true;       // одна зона на маршруте без врагов

        public int maxAttempts = 30;       // попыток перегенерации, если уровень не прошёл проверку
    }

    /// <summary>Результат генерации — всё, что нужно для сборки сцены и для ИИ.</summary>
    public class LevelData
    {
        public int width, height, seed;
        public Tile[,] tiles;
        public GridPos start, exit;
        public List<GridRect> leaves = new List<GridRect>();
        public List<GridPos> doorCells = new List<GridPos>();
        public List<EnemySpawn> enemies = new List<EnemySpawn>();
        public List<GridPos> mainPath = new List<GridPos>();     // кратчайший путь A → Б
        public int[,] distFromStart;                            // BFS-расстояние от A, -1 = недостижимо
        public int restLeafIndex = -1;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
        public bool IsWalkable(int x, int y) => InBounds(x, y) && tiles[x, y] == Tile.Floor;
        public bool IsWalkable(GridPos p) => IsWalkable(p.x, p.y);
        public bool IsSolid(int x, int y) => !InBounds(x, y) || tiles[x, y] != Tile.Floor;
    }
}
