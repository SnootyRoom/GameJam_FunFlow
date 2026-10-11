// Генератор уровня: BSP-арена → проходы → A и Б → укрытия → проверка → враги.
// Не зависит от UnityEngine: вход LevelSettings + сид + сложность, выход LevelData.
using System;
using System.Collections.Generic;

namespace CatTanks.Level
{
    public static class BspLevelGenerator
    {
        /// <summary>
        /// Генерирует уровень. seed = 0 — случайный. difficulty 1..5 влияет на число врагов.
        /// Если попытка не прошла проверку, пробует следующий сид.
        /// </summary>
        public static LevelData Generate(LevelSettings s, int seed, int difficulty)
        {
            int baseSeed = seed != 0 ? seed : Environment.TickCount & 0x7FFFFFFF;
            for (int attempt = 0; attempt < Math.Max(1, s.maxAttempts); attempt++)
            {
                var data = new Builder(s, baseSeed + attempt * 7919, difficulty).TryBuild();
                if (data != null) return data;
            }
            throw new InvalidOperationException(
                "Не удалось сгенерировать уровень. Увеличь размер арены или уменьши minLeafSize/doorWidth.");
        }

        // Шаблоны укрытий (смещения клеток). Повороты L-формы добавляются автоматически.
        static readonly GridPos[][] CoverShapes = BuildShapes();

        static GridPos[][] BuildShapes()
        {
            var list = new List<GridPos[]>
            {
                new[] { P(0, 0) },                                     // колонна
                new[] { P(0, 0), P(1, 0), P(0, 1), P(1, 1) },          // блок 2×2
                new[] { P(0, 0), P(1, 0), P(2, 0) },                   // отрезок по горизонтали
                new[] { P(0, 0), P(0, 1), P(0, 2) },                   // отрезок по вертикали
            };
            var l = new[] { P(0, 0), P(1, 0), P(2, 0), P(0, 1), P(0, 2) }; // Г-образная стенка
            for (int r = 0; r < 4; r++)
            {
                list.Add(l);
                var next = new GridPos[l.Length];
                for (int i = 0; i < l.Length; i++) next[i] = P(-l[i].y, l[i].x); // поворот на 90°
                l = next;
            }
            return list.ToArray();
        }

        static GridPos P(int x, int y) => new GridPos(x, y);

        struct Edge
        {
            public int a, b;
            public bool verticalWall;  // true: стена — столбец x = line, проход по y
            public int line, lo, hi;   // hi не включительно
        }

        class Builder
        {
            readonly LevelSettings s;
            readonly Random rng;
            readonly LevelData d;
            readonly int W, H;
            readonly int difficulty;
            bool[,] keepClear;

            public Builder(LevelSettings settings, int seed, int difficulty)
            {
                s = settings;
                this.difficulty = difficulty;
                rng = new Random(seed);
                W = Math.Max(settings.width, 2 * settings.minLeafSize + 3);
                H = Math.Max(settings.height, 2 * settings.minLeafSize + 3);
                d = new LevelData { width = W, height = H, seed = seed, tiles = new Tile[W, H] };
            }

            public LevelData TryBuild()
            {
                // 1. Внешняя рамка
                for (int x = 0; x < W; x++)
                    for (int y = 0; y < H; y++)
                        d.tiles[x, y] = (x == 0 || y == 0 || x == W - 1 || y == H - 1) ? Tile.Wall : Tile.Floor;

                // 2. BSP: делим внутреннюю часть, линия деления становится стеной толщиной 1
                Split(new GridRect(1, 1, W - 2, H - 2), 0);
                if (d.leaves.Count < 2) return null;

                // 3. Проходы: остовное дерево + несколько петель
                var edges = FindEdges();
                if (!CarveDoors(edges)) return null;

                // 4. A — в ближней к углу зоне, Б — в самой дальней по пути
                int startLeaf = 0;
                for (int i = 1; i < d.leaves.Count; i++)
                {
                    var c = d.leaves[i].Center; var b = d.leaves[startLeaf].Center;
                    if (c.x + c.y < b.x + b.y) startLeaf = i;
                }
                var sl = d.leaves[startLeaf];
                d.start = new GridPos(sl.x + Math.Min(2, sl.w / 2), sl.y + Math.Min(2, sl.h / 2));

                var dist = Bfs(d.start);
                int exitLeaf = -1, best = -1;
                for (int i = 0; i < d.leaves.Count; i++)
                {
                    if (i == startLeaf) continue;
                    var c = d.leaves[i].Center;
                    if (dist[c.x, c.y] > best) { best = dist[c.x, c.y]; exitLeaf = i; }
                }
                if (exitLeaf < 0 || best < 0) return null;
                d.exit = d.leaves[exitLeaf].Center;

                // 5. Укрытия
                BuildKeepClearMask();
                PlaceCover();

                // 6. Проверка: Б достижима, нет замкнутых карманов, путь достаточно длинный
                d.distFromStart = Bfs(d.start);
                int exitDist = d.distFromStart[d.exit.x, d.exit.y];
                if (exitDist < 0) return null;
                if (exitDist < (W + H) / 3) return null;
                if (CountReachable(d.distFromStart) != CountFloor()) return null;

                d.mainPath = new GridPathfinder(d).FindPath(d.start, d.exit, W * H * 2);
                if (d.mainPath == null) return null;

                // 7. Враги
                PlaceEnemies(startLeaf, exitLeaf, exitDist);
                return d;
            }

            // ---------------------------------------------------------------- BSP

            void Split(GridRect r, int depth)
            {
                int min = s.minLeafSize;
                bool canV = r.w >= 2 * min + 1;
                bool canH = r.h >= 2 * min + 1;
                bool stopEarly = depth >= 2 && rng.NextDouble() < 0.15; // немного разнообразия размеров
                if (depth >= s.maxDepth || (!canV && !canH) || stopEarly)
                {
                    d.leaves.Add(r);
                    return;
                }

                bool vertical;
                if (canV && !canH) vertical = true;
                else if (canH && !canV) vertical = false;
                else if (r.w > r.h * 1.25) vertical = true;
                else if (r.h > r.w * 1.25) vertical = false;
                else vertical = rng.Next(2) == 0;

                if (vertical)
                {
                    int sx = rng.Next(r.x + min, r.xMax - min); // столбец стены
                    for (int y = r.y; y < r.yMax; y++) d.tiles[sx, y] = Tile.Wall;
                    Split(new GridRect(r.x, r.y, sx - r.x, r.h), depth + 1);
                    Split(new GridRect(sx + 1, r.y, r.xMax - sx - 1, r.h), depth + 1);
                }
                else
                {
                    int sy = rng.Next(r.y + min, r.yMax - min); // строка стены
                    for (int x = r.x; x < r.xMax; x++) d.tiles[x, sy] = Tile.Wall;
                    Split(new GridRect(r.x, r.y, r.w, sy - r.y), depth + 1);
                    Split(new GridRect(r.x, sy + 1, r.w, r.yMax - sy - 1), depth + 1);
                }
            }

            // ---------------------------------------------------------------- Проходы

            List<Edge> FindEdges()
            {
                var edges = new List<Edge>();
                var L = d.leaves;
                for (int i = 0; i < L.Count; i++)
                    for (int j = i + 1; j < L.Count; j++)
                    {
                        GridRect a = L[i], b = L[j];
                        // соседи слева-справа: между ними один столбец стены
                        if (a.xMax + 1 == b.x || b.xMax + 1 == a.x)
                        {
                            int lo = Math.Max(a.y, b.y), hi = Math.Min(a.yMax, b.yMax);
                            if (hi - lo >= s.doorWidth)
                                edges.Add(new Edge { a = i, b = j, verticalWall = true, line = a.xMax + 1 == b.x ? a.xMax : b.xMax, lo = lo, hi = hi });
                        }
                        // соседи снизу-сверху
                        else if (a.yMax + 1 == b.y || b.yMax + 1 == a.y)
                        {
                            int lo = Math.Max(a.x, b.x), hi = Math.Min(a.xMax, b.xMax);
                            if (hi - lo >= s.doorWidth)
                                edges.Add(new Edge { a = i, b = j, verticalWall = false, line = a.yMax + 1 == b.y ? a.yMax : b.yMax, lo = lo, hi = hi });
                        }
                    }
                return edges;
            }

            bool CarveDoors(List<Edge> edges)
            {
                // перемешиваем и строим дерево (Краскал), остальные рёбра — кандидаты в петли
                for (int i = edges.Count - 1; i > 0; i--) { int k = rng.Next(i + 1); var tmp = edges[i]; edges[i] = edges[k]; edges[k] = tmp; }
                var parent = new int[d.leaves.Count];
                for (int i = 0; i < parent.Length; i++) parent[i] = i;

                var spare = new List<Edge>();
                int joined = 0;
                foreach (var e in edges)
                {
                    int ra = FindRoot(parent, e.a), rb = FindRoot(parent, e.b);
                    if (ra != rb) { parent[ra] = rb; joined++; Carve(e); }
                    else spare.Add(e);
                }
                if (joined != d.leaves.Count - 1) return false; // граф зон несвязный

                for (int i = 0; i < s.extraLoops && i < spare.Count; i++) Carve(spare[i]);
                return true;
            }

            static int FindRoot(int[] parent, int v)
            {
                while (parent[v] != v) v = parent[v] = parent[parent[v]];
                return v;
            }

            void Carve(Edge e)
            {
                int from, to;
                if (rng.NextDouble() < s.openWallChance) { from = e.lo; to = e.hi; } // стену убираем целиком
                else
                {
                    from = rng.Next(e.lo, e.hi - s.doorWidth + 1);
                    to = from + s.doorWidth;
                }
                for (int t = from; t < to; t++)
                {
                    var p = e.verticalWall ? new GridPos(e.line, t) : new GridPos(t, e.line);
                    d.tiles[p.x, p.y] = Tile.Floor;
                    d.doorCells.Add(p);
                }
            }

            // ---------------------------------------------------------------- Укрытия

            void BuildKeepClearMask()
            {
                keepClear = new bool[W, H];
                foreach (var p in d.doorCells) MarkClear(p, s.doorKeepClear);
                MarkClear(d.start, s.startExitClearRadius);
                MarkClear(d.exit, s.startExitClearRadius);
            }

            void MarkClear(GridPos c, int r)
            {
                for (int x = c.x - r; x <= c.x + r; x++)
                    for (int y = c.y - r; y <= c.y + r; y++)
                        if (d.InBounds(x, y)) keepClear[x, y] = true;
            }

            void PlaceCover()
            {
                int totalFloor = CountFloor();
                int clear = s.coverClearance;
                foreach (var leaf in d.leaves)
                {
                    int want = rng.Next(s.minCoverPerLeaf, s.maxCoverPerLeaf + 1);
                    if (leaf.w * leaf.h < 80) want = Math.Min(want, 1);
                    int placed = 0;
                    for (int attempt = 0; attempt < 40 && placed < want; attempt++)
                    {
                        var shape = CoverShapes[rng.Next(CoverShapes.Length)];
                        var origin = new GridPos(rng.Next(leaf.x, leaf.xMax), rng.Next(leaf.y, leaf.yMax));
                        if (!CanPlace(shape, origin, leaf, clear)) continue;

                        foreach (var o in shape) d.tiles[origin.x + o.x, origin.y + o.y] = Tile.Cover;
                        // укрытие не должно отрезать часть арены
                        if (CountReachable(Bfs(d.start)) != totalFloor - shape.Length)
                        {
                            foreach (var o in shape) d.tiles[origin.x + o.x, origin.y + o.y] = Tile.Floor;
                            continue;
                        }
                        totalFloor -= shape.Length;
                        placed++;
                    }
                }
            }

            bool CanPlace(GridPos[] shape, GridPos origin, GridRect leaf, int clear)
            {
                foreach (var o in shape)
                {
                    int x = origin.x + o.x, y = origin.y + o.y;
                    if (x < leaf.x + clear || x >= leaf.xMax - clear || y < leaf.y + clear || y >= leaf.yMax - clear) return false;
                    if (keepClear[x, y]) return false;
                    // вокруг укрытия должно быть свободно (кроме клеток самого укрытия)
                    for (int nx = x - clear; nx <= x + clear; nx++)
                        for (int ny = y - clear; ny <= y + clear; ny++)
                        {
                            if (!d.InBounds(nx, ny)) return false;
                            if (d.tiles[nx, ny] != Tile.Floor && !ShapeContains(shape, origin, nx, ny)) return false;
                        }
                }
                return true;
            }

            static bool ShapeContains(GridPos[] shape, GridPos origin, int x, int y)
            {
                foreach (var o in shape) if (origin.x + o.x == x && origin.y + o.y == y) return true;
                return false;
            }

            // ---------------------------------------------------------------- Враги

            void PlaceEnemies(int startLeaf, int exitLeaf, int exitDist)
            {
                var dist = d.distFromStart;
                int total = s.baseEnemyCount + s.enemiesPerDifficulty * Math.Max(0, difficulty - 1);

                // зона-передышка: на маршруте, ближе всего к середине пути
                if (s.restLeaf && d.leaves.Count >= 4)
                {
                    float bestScore = float.MaxValue;
                    for (int i = 0; i < d.leaves.Count; i++)
                    {
                        if (i == startLeaf || i == exitLeaf) continue;
                        var c = d.leaves[i].Center;
                        if (d.tiles[c.x, c.y] != Tile.Floor || dist[c.x, c.y] < 0) continue;
                        float score = Math.Abs(dist[c.x, c.y] / (float)exitDist - 0.5f);
                        if (score < bestScore) { bestScore = score; d.restLeafIndex = i; }
                    }
                }

                var candidates = new List<GridPos>();
                var doorNear = new bool[W, H];
                foreach (var p in d.doorCells)
                    for (int x = p.x - 1; x <= p.x + 1; x++)
                        for (int y = p.y - 1; y <= p.y + 1; y++)
                            if (d.InBounds(x, y)) doorNear[x, y] = true;

                for (int x = 1; x < W - 1; x++)
                    for (int y = 1; y < H - 1; y++)
                    {
                        if (d.tiles[x, y] != Tile.Floor || dist[x, y] < s.minEnemyDistanceFromStart) continue;
                        if (doorNear[x, y]) continue;                               // не перекрываем проходы
                        var p = new GridPos(x, y);
                        if (Cheb(p, d.exit) <= s.startExitClearRadius) continue;   // не стоим на выходе
                        if (d.restLeafIndex >= 0 && d.leaves[d.restLeafIndex].Contains(p)) continue;
                        if (HasLineOfSight(d.start, p)) continue;                   // не видим игрока на старте
                        candidates.Add(p);
                    }

                // краб-страж рядом с Б: ему нужна площадка 2×2
                if (s.spawnCrabGuard && difficulty >= 2)
                {
                    var crabSpots = new List<GridPos>();
                    foreach (var p in candidates)
                    {
                        int de = Cheb(p, d.exit);
                        if (de < 3 || de > 6) continue;
                        if (d.IsWalkable(p.x + 1, p.y) && d.IsWalkable(p.x, p.y + 1) && d.IsWalkable(p.x + 1, p.y + 1))
                            crabSpots.Add(p);
                    }
                    if (crabSpots.Count > 0)
                    {
                        var c = crabSpots[rng.Next(crabSpots.Count)];
                        d.enemies.Add(new EnemySpawn(c, EnemyType.Crab));
                        total--;
                    }
                }

                for (int n = 0; n < total; n++)
                {
                    // доля стрелков растёт к концу уровня
                    bool shooter = rng.NextDouble() < 0.3 + 0.4 * (n / (float)Math.Max(1, total - 1));
                    var pick = WeightedPick(candidates, dist, exitDist, shooter);
                    if (pick == null) break;
                    d.enemies.Add(new EnemySpawn(pick.Value, shooter ? EnemyType.Shooter : EnemyType.Crawler));
                }
            }

            GridPos? WeightedPick(List<GridPos> candidates, int[,] dist, int exitDist, bool nearCover)
            {
                // проход 0: стрелок рядом с укрытием; 1: рядом с любой стеной; 2: где угодно
                for (int pass = nearCover ? 0 : 2; pass < 3; pass++)
                {
                    double sum = 0;
                    var pool = new List<KeyValuePair<GridPos, double>>();
                    foreach (var p in candidates)
                    {
                        if (!FarFromEnemies(p)) continue;
                        if (pass == 0 && !TileNear(p, 2, true)) continue;
                        if (pass == 1 && !TileNear(p, 1, false)) continue;
                        double t = Math.Min(1.0, dist[p.x, p.y] / (double)exitDist);
                        double w = 0.2 + t * t; // чем ближе к Б, тем вероятнее враг
                        pool.Add(new KeyValuePair<GridPos, double>(p, w));
                        sum += w;
                    }
                    if (pool.Count == 0) continue; // ослабляем требование и пробуем ещё раз
                    double r = rng.NextDouble() * sum;
                    foreach (var kv in pool) { r -= kv.Value; if (r <= 0) return kv.Key; }
                    return pool[pool.Count - 1].Key;
                }
                return null;
            }

            bool FarFromEnemies(GridPos p)
            {
                foreach (var e in d.enemies)
                {
                    int r = e.type == EnemyType.Crab ? s.minEnemySpacing + 1 : s.minEnemySpacing;
                    if (Cheb(p, e.pos) < r) return false;
                }
                return true;
            }

            bool TileNear(GridPos p, int r, bool coverOnly)
            {
                for (int x = p.x - r; x <= p.x + r; x++)
                    for (int y = p.y - r; y <= p.y + r; y++)
                    {
                        if (!d.InBounds(x, y)) continue;
                        if (coverOnly ? d.tiles[x, y] == Tile.Cover : d.tiles[x, y] != Tile.Floor) return true;
                    }
                return false;
            }

            bool HasLineOfSight(GridPos a, GridPos b)
            {
                double ax = a.x + 0.5, ay = a.y + 0.5, bx = b.x + 0.5, by = b.y + 0.5;
                double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                int steps = (int)(len / 0.2) + 1;
                for (int i = 1; i < steps; i++)
                {
                    double t = i / (double)steps;
                    int x = (int)Math.Floor(ax + (bx - ax) * t), y = (int)Math.Floor(ay + (by - ay) * t);
                    if (d.IsSolid(x, y)) return false;
                }
                return true;
            }

            static int Cheb(GridPos a, GridPos b) => Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));

            // ---------------------------------------------------------------- Утилиты

            int[,] Bfs(GridPos from)
            {
                var dist = new int[W, H];
                for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) dist[x, y] = -1;
                var q = new Queue<GridPos>();
                dist[from.x, from.y] = 0;
                q.Enqueue(from);
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    int nd = dist[c.x, c.y] + 1;
                    Visit(dist, q, c.x + 1, c.y, nd); Visit(dist, q, c.x - 1, c.y, nd);
                    Visit(dist, q, c.x, c.y + 1, nd); Visit(dist, q, c.x, c.y - 1, nd);
                }
                return dist;
            }

            void Visit(int[,] dist, Queue<GridPos> q, int x, int y, int nd)
            {
                if (!d.IsWalkable(x, y) || dist[x, y] >= 0) return;
                dist[x, y] = nd;
                q.Enqueue(new GridPos(x, y));
            }

            int CountReachable(int[,] dist)
            {
                int n = 0;
                for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (dist[x, y] >= 0) n++;
                return n;
            }

            int CountFloor()
            {
                int n = 0;
                for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (d.tiles[x, y] == Tile.Floor) n++;
                return n;
            }
        }
    }
}
