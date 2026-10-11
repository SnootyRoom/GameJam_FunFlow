// Создаёт игрока и врагов по данным уровня и передаёт им LevelContext через ILevelActor.Init.
// Префабы — от разработчиков игрока и ИИ. Пустое поле = тестовый примитив.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public class EntitySpawner : MonoBehaviour
    {
        [Header("Префабы (пусто = примитивы)")]
        public GameObject playerPrefab;
        public GameObject crawlerPrefab;
        public GameObject shooterPrefab;
        public GameObject crabPrefab;

        [Header("Цвета примитивов")]
        public Color crawlerColor = new Color(0.85f, 0.25f, 0.25f);
        public Color shooterColor = new Color(0.6f, 0.25f, 0.8f);
        public Color crabColor = new Color(0.95f, 0.55f, 0.1f);

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
                : PlayerTank.CreatePrimitive(ctx.Root, pos);
            go.tag = "Player";

            ctx.Player = go;
            ctx.PlayerHealth = go.GetComponentInChildren<Health>();
            if (ctx.PlayerHealth == null || !ctx.PlayerHealth.isPlayer)
                Debug.LogError("[EntitySpawner] У игрока должен быть Health с галочкой isPlayer.", go);

            InitActors(go, ctx);
            return go;
        }

        /// <summary>
        /// Создаёт врага в клетке cell. Можно вызывать и во время игры (подкрепление, спавн из гнезда).
        /// Краб занимает 2×2 клетки: cell — его левая нижняя клетка.
        /// </summary>
        public GameObject SpawnEnemy(LevelContext ctx, EnemyType type, GridPos cell)
        {
            Vector2 pos = LevelGrid.CellToWorld(cell);
            GameObject go;
            switch (type)
            {
                case EnemyType.Crawler:
                    go = crawlerPrefab != null ? Instantiate(crawlerPrefab, pos, Quaternion.identity, ctx.Root)
                                               : MakeCrawler(ctx.Root, pos, 0.7f, 1, 2.2f, crawlerColor);
                    break;
                case EnemyType.Shooter:
                    go = shooterPrefab != null ? Instantiate(shooterPrefab, pos, Quaternion.identity, ctx.Root)
                                               : MakeShooter(ctx.Root, pos);
                    break;
                default:
                    pos += new Vector2(0.5f, 0.5f); // центр площадки 2×2
                    go = crabPrefab != null ? Instantiate(crabPrefab, pos, Quaternion.identity, ctx.Root)
                                            : MakeCrab(ctx.Root, pos);
                    break;
            }
            ctx.Enemies.Add(go);
            InitActors(go, ctx);
            return go;
        }

        static void InitActors(GameObject go, LevelContext ctx)
        {
            foreach (var actor in go.GetComponentsInChildren<ILevelActor>(true)) actor.Init(ctx);
        }

        // ------------------------------------------------------------------ Примитивы-заглушки

        GameObject MakeCrawler(Transform root, Vector2 pos, float size, int hp, float speed, Color color)
        {
            var go = Prims.Create("Crawler", root, Prims.Square, color, pos, Vector2.one * size, 8);
            Prims.Child("Screen", go.transform, Prims.Square, new Color(0.1f, 0.15f, 0.25f), Vector2.zero, new Vector2(0.6f, 0.75f), 9);
            for (int i = -1; i <= 1; i++) // лапки
            {
                Prims.Child("LegL", go.transform, Prims.Square, Prims.Dark(color, 0.7f), new Vector2(i * 0.3f, 0.55f), new Vector2(0.1f, 0.3f), 7);
                Prims.Child("LegR", go.transform, Prims.Square, Prims.Dark(color, 0.7f), new Vector2(i * 0.3f, -0.55f), new Vector2(0.1f, 0.3f), 7);
            }
            go.AddComponent<BoxCollider2D>().size = Vector2.one * 0.9f;
            var h = go.AddComponent<Health>(); h.maxHp = hp; h.invulnerableAfterHit = 0.05f; h.ResetHp();
            var c = go.AddComponent<EnemyCrawler>(); c.speed = speed;
            return go;
        }

        GameObject MakeShooter(Transform root, Vector2 pos)
        {
            var go = Prims.Create("Shooter", root, Prims.Square, shooterColor, pos, Vector2.one * 0.8f, 8);
            Prims.Child("Screen", go.transform, Prims.Square, new Color(0.1f, 0.15f, 0.25f), Vector2.zero, new Vector2(0.6f, 0.75f), 9);
            var turret = new GameObject("Turret").transform;
            turret.SetParent(go.transform, false);
            turret.localScale = new Vector3(1f / 0.8f, 1f / 0.8f, 1f);
            Prims.Child("Stinger", turret, Prims.Square, Prims.Dark(shooterColor, 0.6f), new Vector2(0.4f, 0), new Vector2(0.4f, 0.14f), 10);
            Prims.Child("Camera", turret, Prims.Circle, new Color(0.9f, 0.9f, 1f), new Vector2(0.6f, 0), Vector2.one * 0.16f, 11);
            go.AddComponent<BoxCollider2D>().size = Vector2.one * 0.95f;
            var h = go.AddComponent<Health>(); h.maxHp = 2; h.invulnerableAfterHit = 0.05f; h.ResetHp();
            var s = go.AddComponent<EnemyShooter>(); s.turret = turret; s.bodyRadius = 0.45f;
            return go;
        }

        GameObject MakeCrab(Transform root, Vector2 pos)
        {
            var go = MakeCrawler(root, pos, 1.7f, 5, 1.0f, crabColor);
            go.name = "Crab";
            var crawler = go.GetComponent<EnemyCrawler>();
            crawler.aggroRange = 6f;   // страж: не уходит далеко от Б, пока игрок не подъедет
            crawler.hearRange = 3f;
            var turret = new GameObject("Turret").transform;
            turret.SetParent(go.transform, false);
            turret.localScale = new Vector3(1f / 1.7f, 1f / 1.7f, 1f);
            Prims.Child("Claw", turret, Prims.Square, Prims.Dark(crabColor, 0.6f), new Vector2(1.0f, 0), new Vector2(0.5f, 0.25f), 10);
            var s = go.AddComponent<EnemyShooter>();
            s.turret = turret; s.bodyRadius = 0.9f; s.fireInterval = 1.6f; s.maxBounces = 2;
            go.GetComponent<Health>().CacheRenderers(); // клешня добавлена после Health
            return go;
        }
    }
}
