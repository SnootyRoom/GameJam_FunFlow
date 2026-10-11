// Всё, что нужно объектам на уровне. Создаётся GameFlow на каждый уровень и передаётся через Init.
using System;
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
        public readonly List<GameObject> Enemies = new List<GameObject>();   // только живые

        public event Action<GameObject> EnemyKilled;      // враг умер (объект ещё существует до конца кадра)
        public event Action<int> EnemyCountChanged;       // сколько живых — для счётчика, открытия выхода и т.п.

        public bool PlayerAlive => Player != null && Player.activeInHierarchy;

        public int AliveEnemyCount()
        {
            Enemies.RemoveAll(e => e == null); // на случай, если врага удалили не через Health
            return Enemies.Count;
        }

        /// <summary>Вызывает EntitySpawner. Враг уходит из списка, когда его Health сообщит о смерти.</summary>
        public void RegisterEnemy(GameObject enemy)
        {
            Enemies.Add(enemy);
            var hp = enemy.GetComponentInChildren<Health>();
            if (hp != null) hp.OnDeath += () => HandleEnemyDeath(enemy);
            EnemyCountChanged?.Invoke(Enemies.Count);
        }

        void HandleEnemyDeath(GameObject enemy)
        {
            if (!Enemies.Remove(enemy)) return;
            EnemyKilled?.Invoke(enemy);
            EnemyCountChanged?.Invoke(Enemies.Count);
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
