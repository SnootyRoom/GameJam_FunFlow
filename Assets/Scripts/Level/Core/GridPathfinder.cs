// A* по сетке уровня. Не зависит от UnityEngine.
using System;
using System.Collections.Generic;

namespace CatTanks.Level
{
    public class GridPathfinder
    {
        readonly LevelData _level;
        readonly int _w, _h;
        readonly int[] _g;
        readonly int[] _parent;
        readonly int[] _stamp;   // «версия» поиска, чтобы не чистить массивы между вызовами
        readonly bool[] _closed;
        int _search;

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly int[] COST = { 10, 10, 10, 10, 14, 14, 14, 14 };

        public GridPathfinder(LevelData level)
        {
            _level = level;
            _w = level.width;
            _h = level.height;
            int n = _w * _h;
            _g = new int[n];
            _parent = new int[n];
            _stamp = new int[n];
            _closed = new bool[n];
        }

        /// <summary>
        /// Кратчайший путь от from до to (включая обе клетки) или null.
        /// Диагонали разрешены, но без «срезания углов» — танк не застрянет на угле стены.
        /// </summary>
        public List<GridPos> FindPath(GridPos from, GridPos to, int maxNodes = 4000)
        {
            if (!_level.IsWalkable(from) || !_level.IsWalkable(to)) return null;
            if (from == to) return new List<GridPos> { from };

            _search++;
            int start = from.y * _w + from.x;
            int goal = to.y * _w + to.x;

            var open = new MinHeap();
            Touch(start);
            _g[start] = 0;
            _parent[start] = -1;
            open.Push(Heuristic(from, to), start);

            int expanded = 0;
            while (open.Count > 0)
            {
                int cur = open.Pop();
                if (_closed[cur]) continue;
                _closed[cur] = true;
                if (cur == goal) return Reconstruct(goal);
                if (++expanded > maxNodes) return null;

                int cx = cur % _w, cy = cur / _w;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DX[d], ny = cy + DY[d];
                    if (!_level.IsWalkable(nx, ny)) continue;
                    if (d >= 4 && (!_level.IsWalkable(cx + DX[d], cy) || !_level.IsWalkable(cx, cy + DY[d])))
                        continue; // не срезаем угол

                    int ni = ny * _w + nx;
                    Touch(ni);
                    if (_closed[ni]) continue;
                    int ng = _g[cur] + COST[d];
                    if (ng < _g[ni])
                    {
                        _g[ni] = ng;
                        _parent[ni] = cur;
                        open.Push(ng + Heuristic(new GridPos(nx, ny), to), ni);
                    }
                }
            }
            return null;
        }

        void Touch(int i)
        {
            if (_stamp[i] == _search) return;
            _stamp[i] = _search;
            _g[i] = int.MaxValue;
            _closed[i] = false;
        }

        static int Heuristic(GridPos a, GridPos b)
        {
            int dx = Math.Abs(a.x - b.x), dy = Math.Abs(a.y - b.y);
            return 10 * (dx + dy) - 6 * Math.Min(dx, dy); // октильная эвристика
        }

        List<GridPos> Reconstruct(int goal)
        {
            var path = new List<GridPos>();
            for (int i = goal; i != -1; i = _parent[i]) path.Add(new GridPos(i % _w, i / _w));
            path.Reverse();
            return path;
        }

        /// <summary>Простая двоичная куча (приоритет, значение).</summary>
        class MinHeap
        {
            readonly List<long> _items = new List<long>();
            public int Count => _items.Count;

            public void Push(int priority, int value)
            {
                _items.Add(((long)priority << 32) | (uint)value);
                int i = _items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (_items[p] <= _items[i]) break;
                    long tmp = _items[p]; _items[p] = _items[i]; _items[i] = tmp;
                    i = p;
                }
            }

            public int Pop()
            {
                long top = _items[0];
                int last = _items.Count - 1;
                _items[0] = _items[last];
                _items.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < _items.Count && _items[l] < _items[m]) m = l;
                    if (r < _items.Count && _items[r] < _items[m]) m = r;
                    if (m == i) break;
                    long tmp = _items[m]; _items[m] = _items[i]; _items[i] = tmp;
                    i = m;
                }
                return (int)(top & 0xFFFFFFFF);
            }
        }
    }
}
