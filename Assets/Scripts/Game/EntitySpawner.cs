// Создаёт игрока и врагов по данным уровня и передаёт им LevelContext через ILevelActor.Init.
// Префаб и параметры каждого врага — в ассетах EnemyDefinition. Нет префаба = заглушка из Placeholders.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public class EntitySpawner : MonoBehaviour
    {
        [Tooltip("Пусто = танк из примитивов")]
        public GameObject playerPrefab;

        [Tooltip("По одному описанию на каждый EnemyType. Пустой список в редакторе заполнится всеми ассетами проекта")]
        public EnemyDefinition[] enemies;

        /// <summary>Создаёт игрока в точке A и всех врагов из ctx.Data.enemies.</summary>
        public void SpawnAll(LevelContext ctx)
        {
            SpawnPlayer(ctx);
            foreach (var e in ctx.Data.enemies) SpawnEnemy(ctx, e.type, e.pos);
        }

        public GameObject SpawnPlayer(LevelContext ctx)
        {
            Vector2 pos = LevelGrid.CellToWorld(ctx.Data.start);
            var go = playerPrefab != null
                ? Instantiate(playerPrefab, pos, Quaternion.identity, ctx.Root)
                : Placeholders.Player(ctx.Root, pos);
            go.tag = "Player";

            ctx.Player = go;
            ctx.PlayerHealth = go.GetComponentInChildren<Health>();
            if (ctx.PlayerHealth == null || !ctx.PlayerHealth.IsPlayer)
                Debug.LogError("[EntitySpawner] У игрока должен быть Health с team = Player.", go);

            InitActors(go, ctx);
            return go;
        }

        /// <summary>
        /// Создаёт врага в клетке cell. Можно вызывать и во время игры (подкрепление, спавн из гнезда).
        /// Для больших врагов (footprint > 1) cell — левая нижняя клетка площадки.
        /// </summary>
        public GameObject SpawnEnemy(LevelContext ctx, EnemyType type, GridPos cell)
        {
            var def = Find(type);
            if (def == null)
            {
                Debug.LogError($"[EntitySpawner] Нет EnemyDefinition для {type} — добавь ассет в список enemies.", this);
                return null;
            }

            Vector2 pos = LevelGrid.CellToWorld(cell) + def.FootprintOffset;
            var go = def.prefab != null
                ? Instantiate(def.prefab, pos, Quaternion.identity, ctx.Root)
                : Placeholders.Enemy(def, ctx.Root, pos);
            def.ApplyTo(go);

            ctx.RegisterEnemy(go);
            InitActors(go, ctx);
            return go;
        }

        EnemyDefinition Find(EnemyType type)
        {
            if (enemies != null)
                foreach (var d in enemies)
                    if (d != null && d.type == type) return d;
            return null;
        }

        static void InitActors(GameObject go, LevelContext ctx)
        {
            foreach (var actor in go.GetComponentsInChildren<ILevelActor>(true)) actor.Init(ctx);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (enemies != null && enemies.Length > 0) return;
            var guids = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(EnemyDefinition));
            if (guids.Length == 0) return;
            enemies = new EnemyDefinition[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                enemies[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]));
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
