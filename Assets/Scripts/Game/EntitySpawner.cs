// Создаёт игрока и врагов по данным уровня и передаёт им LevelContext через ILevelActor.Init.
// Враги описаны ассетами EnemyDefinition: из них же берутся правила появления для генератора (SpawnRules).
// Нет префаба = враг собирается из спрайта или примитивов (Placeholders).
using System.Collections.Generic;
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public class EntitySpawner : MonoBehaviour
    {
        [Tooltip("Пусто = танк из примитивов")]
        public GameObject playerPrefab;

        [Tooltip("Все враги игры. Пустой список в редакторе заполнится всеми ассетами EnemyDefinition проекта")]
        public EnemyDefinition[] enemies;

        /// <summary>Правила появления для генератора — по одному на каждого врага, который ставится на уровни.</summary>
        public List<EnemySpawnRule> SpawnRules()
        {
            var rules = new List<EnemySpawnRule>();
            if (enemies != null)
                foreach (var d in enemies)
                    if (d != null && d.spawnOnLevels) rules.Add(d.ToSpawnRule());
            return rules;
        }

        /// <summary>Создаёт игрока в точке A и всех врагов из ctx.Data.enemies.</summary>
        public void SpawnAll(LevelContext ctx)
        {
            SpawnPlayer(ctx);
            foreach (var e in ctx.Data.enemies) SpawnEnemy(ctx, e.id, e.pos);
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

        /// <summary>Враг по id (= имени ассета EnemyDefinition).</summary>
        public GameObject SpawnEnemy(LevelContext ctx, string id, GridPos cell)
        {
            var def = Find(id);
            if (def == null)
            {
                Debug.LogError($"[EntitySpawner] Нет EnemyDefinition «{id}» — добавь ассет в список enemies.", this);
                return null;
            }
            return SpawnEnemy(ctx, def, cell);
        }

        /// <summary>
        /// Создаёт врага в клетке cell. Можно вызывать и во время игры (подкрепление, спавн из гнезда).
        /// Для больших врагов (footprint > 1) cell — левая нижняя клетка площадки.
        /// </summary>
        public GameObject SpawnEnemy(LevelContext ctx, EnemyDefinition def, GridPos cell)
        {
            Vector2 pos = LevelGrid.CellToWorld(cell) + def.FootprintOffset;
            var go = def.prefab != null
                ? Instantiate(def.prefab, pos, Quaternion.identity, ctx.Root)
                : Placeholders.Enemy(def, ctx.Root, pos);
            def.ApplyTo(go);
            var tag = go.GetComponent<EnemyTag>();
            if (tag == null) tag = go.AddComponent<EnemyTag>();
            tag.definition = def;

            ctx.RegisterEnemy(go);
            InitActors(go, ctx);
            return go;
        }

        EnemyDefinition Find(string id)
        {
            if (enemies != null)
                foreach (var d in enemies)
                    if (d != null && d.name == id) return d;
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
            var found = EditorAssetLookup.FindAll<EnemyDefinition>();
            if (found.Length == 0) return;
            enemies = found;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
