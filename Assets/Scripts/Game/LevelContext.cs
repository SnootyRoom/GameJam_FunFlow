// Всё, что нужно объектам на уровне. Создаётся GameFlow на каждый уровень и передаётся через Init.
using System.Collections.Generic;
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public class LevelContext
    {
        public LevelData Data;              // сетка, A, Б, главный путь, расстояния
        public GridPathfinder Pathfinder;   // A* по сетке
        public Transform Root;              // родитель всего на уровне; удаляется при смене уровня
        public int LevelNumber;
        public int Difficulty;

        public GameObject Player;
        public Health PlayerHealth;
        public Transform Exit;
        public readonly List<GameObject> Enemies = new List<GameObject>();

        public bool PlayerAlive => Player != null && Player.activeInHierarchy;

        public int AliveEnemyCount()
        {
            Enemies.RemoveAll(e => e == null);
            return Enemies.Count;
        }
    }

    /// <summary>
    /// Реализуй в любом скрипте, который живёт на уровне (игрок, враг, турель, пикап).
    /// EntitySpawner вызовет Init сразу после создания объекта и до первого Update.
    /// </summary>
    public interface ILevelActor
    {
        void Init(LevelContext ctx);
    }

    /// <summary>Перевод между клетками сетки и мировыми координатами. 1 клетка = 1 юнит.</summary>
    public static class LevelGrid
    {
        /// <summary>Центр клетки. Клетка (0,0) — левый нижний угол арены.</summary>
        public static Vector2 CellToWorld(GridPos p) => new Vector2(p.x + 0.5f, p.y + 0.5f);

        public static GridPos WorldToCell(Vector2 w) => new GridPos(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y));
    }
}
