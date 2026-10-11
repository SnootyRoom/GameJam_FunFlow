// Заглушки из примитивов для игрока и врагов, пока нет префабов от художника.
// Когда все префабы назначены, этот файл можно удалить вместе с ветками «prefab == null» в EntitySpawner.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public static class Placeholders
    {
        static readonly Color ScreenColor = new Color(0.1f, 0.15f, 0.25f);

        /// <summary>Танк: корпус, гусеницы, башня с «котом».</summary>
        public static GameObject Player(Transform parent, Vector3 pos)
        {
            var go = Prims.Create("PlayerTank", parent, Prims.Square, new Color(0.30f, 0.55f, 0.30f), pos, Vector2.one * 0.8f, 10);
            Prims.Child("TrackL", go.transform, Prims.Square, new Color(0.15f, 0.2f, 0.15f), new Vector2(0, 0.45f), new Vector2(1.05f, 0.18f), 11);
            Prims.Child("TrackR", go.transform, Prims.Square, new Color(0.15f, 0.2f, 0.15f), new Vector2(0, -0.45f), new Vector2(1.05f, 0.18f), 11);

            var turret = Turret(go.transform, 0.8f);
            Prims.Child("Barrel", turret, Prims.Square, new Color(0.2f, 0.35f, 0.2f), new Vector2(0.35f, 0), new Vector2(0.5f, 0.14f), 12);
            Prims.Child("Cat", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), Vector2.zero, Vector2.one * 0.42f, 13);
            Prims.Child("EarL", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), new Vector2(-0.12f, 0.17f), Vector2.one * 0.14f, 13);
            Prims.Child("EarR", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), new Vector2(-0.12f, -0.17f), Vector2.one * 0.14f, 13);

            go.AddComponent<CircleCollider2D>().radius = 0.5f; // ×0.8 масштаба = 0.4 юнита, пролезает в проход в 1 клетку
            var hp = go.AddComponent<Health>();
            hp.team = Team.Player;
            hp.maxHp = 3;
            hp.ResetHp();
            go.AddComponent<PlayerTank>().turret = turret;
            return go;
        }

        /// <summary>Враг по описанию. Параметры (hp, скорость…) потом проставит EnemyDefinition.ApplyTo.</summary>
        public static GameObject Enemy(EnemyDefinition def, Transform parent, Vector2 pos)
        {
            float size = def.size;
            var go = Prims.Create(def.type.ToString(), parent, Prims.Square, def.color, pos, Vector2.one * size, 8);
            Prims.Child("Screen", go.transform, Prims.Square, ScreenColor, Vector2.zero, new Vector2(0.6f, 0.75f), 9);

            bool walks = def.type != EnemyType.Shooter;
            bool shoots = def.type != EnemyType.Crawler;

            if (walks)
                for (int i = -1; i <= 1; i++) // лапки
                {
                    Prims.Child("LegL", go.transform, Prims.Square, Prims.Dark(def.color, 0.7f), new Vector2(i * 0.3f, 0.55f), new Vector2(0.1f, 0.3f), 7);
                    Prims.Child("LegR", go.transform, Prims.Square, Prims.Dark(def.color, 0.7f), new Vector2(i * 0.3f, -0.55f), new Vector2(0.1f, 0.3f), 7);
                }

            Transform turret = null;
            if (shoots)
            {
                turret = Turret(go.transform, size);
                if (def.type == EnemyType.Crab)
                    Prims.Child("Claw", turret, Prims.Square, Prims.Dark(def.color, 0.6f), new Vector2(1.0f, 0), new Vector2(0.5f, 0.25f), 10);
                else
                {
                    Prims.Child("Stinger", turret, Prims.Square, Prims.Dark(def.color, 0.6f), new Vector2(0.4f, 0), new Vector2(0.4f, 0.14f), 10);
                    Prims.Child("Camera", turret, Prims.Circle, new Color(0.9f, 0.9f, 1f), new Vector2(0.6f, 0), Vector2.one * 0.16f, 11);
                }
            }

            go.AddComponent<BoxCollider2D>().size = Vector2.one * (walks ? 0.9f : 0.95f);
            go.AddComponent<Health>(); // после всех спрайтов — вспышка урона запоминает их цвета в Awake
            if (walks) go.AddComponent<EnemyCrawler>();
            if (shoots)
            {
                var s = go.AddComponent<EnemyShooter>();
                s.turret = turret;
                s.bodyRadius = size * 0.55f;
            }
            return go;
        }

        // башня — отдельный объект без масштаба корпуса, чтобы вращалась независимо
        static Transform Turret(Transform body, float bodyScale)
        {
            var t = new GameObject("Turret").transform;
            t.SetParent(body, false);
            t.localScale = new Vector3(1f / bodyScale, 1f / bodyScale, 1f);
            return t;
        }
    }
}
