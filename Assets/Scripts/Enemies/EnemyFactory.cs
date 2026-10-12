// Создание врага по точке спавна от генератора: префаб из EnemyDefinition или сборка из примитивов.
// Новый тип врага без префаба → добавь case в BuildPrimitive.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public static class EnemyFactory
    {
        public static Enemy Spawn(EnemyCatalog catalog, EnemySpawn spawn, EnemyContext ctx, Transform parent, bool elite = false)
        {
            var def = catalog.Get(spawn.type);
            if (def == null)
            {
                Debug.LogWarning($"[EnemyFactory] Нет EnemyDefinition для {spawn.type} в каталоге — пропускаю.");
                return null;
            }

            // враг на N×N клетках стоит в центре своей площадки
            Vector2 pos = LevelBuilder.CellToWorld(spawn.pos) + Vector2.one * ((spawn.footprint - 1) * 0.5f);
            float size = def.size * (elite ? Enemy.EliteSize : 1f);
            var go = def.prefab != null
                ? Object.Instantiate(def.prefab, pos, Quaternion.identity, parent)
                : BuildPrimitive(def, pos, parent, size);

            if (go.GetComponent<Health>() == null) go.AddComponent<Health>();
            var enemy = go.GetComponent<Enemy>();
            if (enemy == null) enemy = go.AddComponent<Enemy>();
            if (elite) AddEliteMarks(go.transform, size);
            enemy.Init(def, ctx, elite);
            return enemy;
        }

        // ------------------------------------------------------------------ Заглушки из примитивов

        static GameObject BuildPrimitive(EnemyDefinition def, Vector2 pos, Transform parent, float size)
        {
            switch (def.type)
            {
                case EnemyType.Crawler: return MakeCrawler(def, pos, parent, size);
                case EnemyType.Runner: return MakeRunner(def, pos, parent, size);
                case EnemyType.Brute: return MakeBrute(def, pos, parent, size);
                case EnemyType.Shooter: return MakeShooter(def, pos, parent, size);
                case EnemyType.Gunner: return MakeGunner(def, pos, parent, size);
                case EnemyType.Sniper: return MakeSniper(def, pos, parent, size);
                case EnemyType.Crab: return MakeBoss(def, pos, parent, size);
                default: return MakeCrawler(def, pos, parent, size);
            }
        }

        static GameObject Body(EnemyDefinition def, Vector2 pos, Transform parent, float size)
        {
            var go = Prims.Create(def.displayName, parent, Prims.Square, def.color, pos, Vector2.one * size, 8);
            Prims.Child("Screen", go.transform, Prims.Square, new Color(0.1f, 0.15f, 0.25f), Vector2.zero, new Vector2(0.6f, 0.75f), 9);
            Prims.Child("Glare", go.transform, Prims.Square, new Color(1f, 1f, 1f, 0.18f), new Vector2(-0.12f, 0.18f), new Vector2(0.18f, 0.3f), 9);
            Prims.Shadow(go.transform, size * 1.25f);
            go.AddComponent<Health>();
            go.AddComponent<Enemy>();
            return go;
        }

        /// <summary>
        /// Круглый коллайдер, который всегда пролезает в проход: для тела в 1 клетку — радиус 0.44,
        /// для 2×2 — 0.9 (в мировых единицах), независимо от размера картинки. Круг не цепляется за углы.
        /// </summary>
        static void BodyCollider(GameObject go, float size, int footprint)
        {
            var col = go.AddComponent<CircleCollider2D>();
            float worldR = Mathf.Min(size * 0.48f, footprint >= 2 ? 0.9f : 0.44f);
            col.radius = worldR / Mathf.Max(0.01f, size);
        }

        static Transform Turret(Transform body, float bodySize)
        {
            var turret = new GameObject("Turret").transform;
            turret.SetParent(body, false);
            turret.localScale = new Vector3(1f / bodySize, 1f / bodySize, 1f);
            return turret;
        }

        static void Legs(GameObject go, Color c, float length = 0.3f, float width = 0.1f)
        {
            for (int i = -1; i <= 1; i++)
            {
                Prims.Child("LegL", go.transform, Prims.Square, c, new Vector2(i * 0.3f, 0.5f + length / 2f), new Vector2(width, length), 7);
                Prims.Child("LegR", go.transform, Prims.Square, c, new Vector2(i * 0.3f, -0.5f - length / 2f), new Vector2(width, length), 7);
            }
        }

        static GameObject MakeCrawler(EnemyDefinition def, Vector2 pos, Transform parent, float size)
        {
            var go = Body(def, pos, parent, size);
            Legs(go, Prims.Dark(def.color, 0.7f));
            BodyCollider(go, size, 1);
            go.AddComponent<ChaseBehaviour>();
            go.AddComponent<ContactDamage>();
            return go;
        }

        static GameObject MakeRunner(EnemyDefinition def, Vector2 pos, Transform parent, float size)
        {
            var go = Body(def, pos, parent, size);
            Legs(go, Prims.Dark(def.color, 0.7f), 0.45f, 0.08f);  // длинные тонкие лапки
            Prims.Child("Antenna", go.transform, Prims.Square, Prims.Dark(def.color, 0.6f), new Vector2(0.55f, 0f), new Vector2(0.3f, 0.06f), 7);
            BodyCollider(go, size, 1);
            var chase = go.AddComponent<ChaseBehaviour>();
            chase.repathInterval = 0.25f;
            chase.hearRange = 5f;
            go.AddComponent<ContactDamage>().cooldown = 0.7f;
            return go;
        }

        static GameObject MakeBrute(EnemyDefinition def, Vector2 pos, Transform parent, float size)
        {
            var go = Body(def, pos, parent, size);
            Legs(go, Prims.Dark(def.color, 0.55f), 0.25f, 0.16f);
            // бронепластины по бокам
            Prims.Child("ArmorL", go.transform, Prims.Square, Prims.Dark(def.color, 0.6f), new Vector2(0f, 0.43f), new Vector2(0.9f, 0.16f), 9);
            Prims.Child("ArmorR", go.transform, Prims.Square, Prims.Dark(def.color, 0.6f), new Vector2(0f, -0.43f), new Vector2(0.9f, 0.16f), 9);
            Prims.Child("Ram", go.transform, Prims.Square, Prims.Dark(def.color, 0.45f), new Vector2(0.5f, 0f), new Vector2(0.14f, 0.8f), 9);
            BodyCollider(go, size, 1);
            var chase = go.AddComponent<ChaseBehaviour>();
            chase.aggroRange = 7f;
            go.AddComponent<ContactDamage>().cooldown = 1.2f;
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null) rb.mass = 6f; // громилу не растолкать
            return go;
        }

        static GameObject ShooterBase(EnemyDefinition def, Vector2 pos, Transform parent, float size, out Transform turret)
        {
            var go = Body(def, pos, parent, size);
            turret = Turret(go.transform, size);
            BodyCollider(go, size, 1);
            var t = go.AddComponent<TurretBehaviour>();
            t.turret = turret;
            t.bodyRadius = size * 0.55f;
            return go;
        }

        static GameObject MakeShooter(EnemyDefinition def, Vector2 pos, Transform parent, float size)
        {
            var go = ShooterBase(def, pos, parent, size, out var turret);
            Prims.Child("Stinger", turret, Prims.Square, Prims.Dark(def.color, 0.6f), new Vector2(0.4f, 0), new Vector2(0.4f, 0.14f), 10);
            Prims.Child("Camera", turret, Prims.Circle, new Color(0.9f, 0.9f, 1f), new Vector2(0.6f, 0), Vector2.one * 0.16f, 11);
            return go;
        }

        static GameObject MakeGunner(EnemyDefinition def, Vector2 pos, Transform parent, float size)
        {
            var go = ShooterBase(def, pos, parent, size, out var turret);
            Prims.Child("BarrelA", turret, Prims.Square, Prims.Dark(def.color, 0.55f), new Vector2(0.42f, 0.08f), new Vector2(0.45f, 0.08f), 10);
            Prims.Child("BarrelB", turret, Prims.Square, Prims.Dark(def.color, 0.55f), new Vector2(0.42f, -0.08f), new Vector2(0.45f, 0.08f), 10);
            Prims.Child("Drum", turret, Prims.Circle, Prims.Dark(def.color, 0.4f), new Vector2(0.15f, 0f), Vector2.one * 0.26f, 10);
            return go;
        }

        static GameObject MakeSniper(EnemyDefinition def, Vector2 pos, Transform parent, float size)
        {
            var go = ShooterBase(def, pos, parent, size, out var turret);
            Prims.Child("Barrel", turret, Prims.Square, Prims.Dark(def.color, 0.5f), new Vector2(0.55f, 0), new Vector2(0.75f, 0.07f), 10);
            Prims.Child("Scope", turret, Prims.Circle, new Color(0.95f, 0.3f, 0.3f), new Vector2(0.25f, 0.12f), Vector2.one * 0.14f, 11);
            return go;
        }

        /// <summary>Босс-краб: 2×2 клетки, клешни, вращающееся «ядро» для карусели. Атаки — BossBehaviour.</summary>
        static GameObject MakeBoss(EnemyDefinition def, Vector2 pos, Transform parent, float size)
        {
            var go = Body(def, pos, parent, size);
            Legs(go, Prims.Dark(def.color, 0.6f), 0.28f, 0.12f);
            Prims.Child("ClawL", go.transform, Prims.Circle, Prims.Dark(def.color, 0.75f), new Vector2(0.55f, 0.42f), new Vector2(0.38f, 0.28f), 9);
            Prims.Child("ClawR", go.transform, Prims.Circle, Prims.Dark(def.color, 0.75f), new Vector2(0.55f, -0.42f), new Vector2(0.38f, 0.28f), 9);
            BodyCollider(go, size, 2);
            var chase = go.AddComponent<ChaseBehaviour>();
            chase.aggroRange = 11f;
            chase.hearRange = 7f;
            chase.stopDistance = 5.5f;     // держит дистанцию и обстреливает
            chase.repathInterval = 0.5f;
            go.AddComponent<ContactDamage>().cooldown = 0.9f;
            var turret = Turret(go.transform, size);
            Prims.Child("Core", turret, Prims.Circle, new Color(1f, 0.85f, 0.3f), Vector2.zero, Vector2.one * 0.5f, 11);
            for (int i = 0; i < 4; i++)
            {
                var spoke = Prims.Child("Gun", turret, Prims.Square, Prims.Dark(def.color, 0.5f), Vector2.zero, new Vector2(0.55f, 0.12f), 10);
                spoke.transform.localRotation = Quaternion.Euler(0, 0, i * 90f);
                spoke.transform.localPosition = spoke.transform.right * 0.45f;
            }
            var boss = go.AddComponent<BossBehaviour>();
            boss.turret = turret;
            boss.bodyRadius = size * 0.55f;
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null) rb.mass = 20f;
            return go;
        }

        /// <summary>Элита: золотая корона над головой (не крутится вместе с врагом) и золотой ободок.</summary>
        static void AddEliteMarks(Transform body, float size)
        {
            var gold = new Color(1f, 0.82f, 0.25f);
            var rim = Prims.Child("EliteRim", body, Prims.Square, gold, Vector2.zero, Vector2.one * 1.14f, 7);
            rim.AddComponent<NoFlash>();
            var crown = new GameObject("EliteCrown").transform;
            crown.SetParent(body, false);
            var keep = crown.gameObject.AddComponent<KeepWorldOffset>();
            keep.offset = new Vector2(0f, size * 0.7f + 0.22f);
            keep.worldScale = Vector2.one;
            for (int i = -1; i <= 1; i++)
            {
                var spike = Prims.Child("Spike", crown, Prims.Triangle, gold, new Vector2(i * 0.13f, i == 0 ? 0.04f : 0f), new Vector2(0.16f, 0.2f), 30);
                spike.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                spike.AddComponent<NoFlash>();
            }
            var band = Prims.Child("Band", crown, Prims.Square, gold, new Vector2(0f, -0.09f), new Vector2(0.42f, 0.07f), 30);
            band.AddComponent<NoFlash>();
        }
    }
}
