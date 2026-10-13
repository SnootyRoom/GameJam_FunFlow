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
            go.AddComponent<AimPreview>();
            go.AddComponent<TrackTrail>();
            go.AddComponent<PlayerUltimate>();
            go.AddComponent<PlayerAudio>();
            return go;
        }

        /// <summary>Враг по описанию. Параметры (hp, скорость…) потом проставит EnemyDefinition.ApplyTo.</summary>
        public static GameObject Enemy(EnemyDefinition def, Transform parent, Vector2 pos)
        {
            var go = def.sprite != null ? SpriteEnemy(def, parent, pos) : PrimitiveEnemy(def, parent, pos);
            // параметры этих компонентов проставит ApplyTo
            if (def.bossStages != null && def.bossStages.Length > 0) go.AddComponent<BossStages>();
            if (def.shoots && !def.moves && def.takesCover) go.AddComponent<ShooterTactics>(); // даёт стрелку тело, чтобы ходить
            if (def.dodgeChance > 0f && (def.moves || def.takesCover)) go.AddComponent<EnemyEvasion>(); // уворачиваться может только тот, кто ходит
            return go;
        }

        /// <summary>
        /// Враг из спрайта художника. Кто ходит (ползун, краб) — разворачивается по ходу движения вместе с телом;
        /// кто только стреляет (стрелок) — спрайт сидит на «башне» и поворачивается туда, куда целится.
        /// </summary>
        static GameObject SpriteEnemy(EnemyDefinition def, Transform parent, Vector2 pos)
        {
            bool walks = def.moves;
            bool shoots = def.shoots;

            var go = new GameObject(def.name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            Transform turret = null;
            if (shoots)
            {
                turret = new GameObject("Turret").transform;
                turret.SetParent(go.transform, false);
            }

            // картинка поворачивается так, чтобы «вперёд» смотрело вдоль +X — как у всей логики движения и прицела
            var visualParent = walks ? go.transform : turret;
            var art = new GameObject("Sprite");
            art.transform.SetParent(visualParent, false);
            art.transform.localRotation = Quaternion.Euler(0, 0, -def.spriteFacing);
            var sr = art.AddComponent<SpriteRenderer>();
            sr.sprite = def.sprite;
            sr.sortingOrder = 8;
            Vector2 s = def.sprite.bounds.size;
            float k = def.size / Mathf.Max(0.01f, Mathf.Max(s.x, s.y));
            art.transform.localScale = new Vector3(k, k, 1f);

            go.AddComponent<CircleCollider2D>().radius = def.size * 0.45f;
            go.AddComponent<Health>(); // после спрайта — вспышка урона запоминает его цвет в Awake
            if (walks) go.AddComponent<EnemyCrawler>();
            if (shoots)
            {
                var sh = go.AddComponent<EnemyShooter>();
                sh.turret = walks ? null : turret; // у краба тело уже крутится при ходьбе
                sh.bodyRadius = def.size * 0.55f;
            }
            return go;
        }

        static GameObject PrimitiveEnemy(EnemyDefinition def, Transform parent, Vector2 pos)
        {
            float size = def.size;
            var go = Prims.Create(def.name, parent, Prims.Square, def.color, pos, Vector2.one * size, 8);
            Prims.Child("Screen", go.transform, Prims.Square, ScreenColor, Vector2.zero, new Vector2(0.6f, 0.75f), 9);

            bool walks = def.moves;
            bool shoots = def.shoots;

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
                if (walks)   // ходит и стреляет (краб) — клешня; только стреляет — жало с «камерой»
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
