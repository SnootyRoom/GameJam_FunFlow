// Всё, что враг знает о мире. Один экземпляр на уровень, общий для всех врагов.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public class EnemyContext
    {
        public readonly PlayerTank Player;
        public readonly LevelData Level;            // сетка, стены, A/Б, зоны (leaves), проходы
        public readonly GridPathfinder Pathfinder;  // A* по сетке
        public readonly int LevelNumber;
        public readonly AiParams Ai;                // «ум» ИИ на этом уровне (см. DifficultyProgression)

        public EnemyContext(PlayerTank player, LevelData level, GridPathfinder pathfinder, int levelNumber, AiParams ai)
        {
            Player = player;
            Level = level;
            Pathfinder = pathfinder;
            LevelNumber = levelNumber;
            Ai = ai;
        }

        public bool PlayerAlive => Player != null && Player.isActiveAndEnabled && !Player.Health.IsDead;
        public Vector2 PlayerPosition => Player != null ? (Vector2)Player.transform.position : Vector2.zero;
        public Transform PlayerTransform => Player != null ? Player.transform : null;

        /// <summary>Путь по клеткам до точки в мире (или null). Каждая точка — центр клетки в мире.</summary>
        public System.Collections.Generic.List<GridPos> FindPath(Vector2 from, Vector2 to, int maxNodes = 4000)
            => Pathfinder.FindPath(LevelBuilder.WorldToCell(from), LevelBuilder.WorldToCell(to), maxNodes);
    }
}
