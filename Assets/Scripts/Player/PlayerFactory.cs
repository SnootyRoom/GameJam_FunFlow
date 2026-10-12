// Создание игрока: из префаба (GameConfig.playerPrefab) или из примитивов.
// Недостающие модули добавляются автоматически, так что в префабе достаточно спрайтов + коллайдера + дочернего "Turret".
using UnityEngine;

namespace CatTanks
{
    public static class PlayerFactory
    {
        public static PlayerTank Create(GameConfig cfg, Transform parent, Vector2 pos, PlayerStats stats,
                                        TankModuleDefinition hull, TankModuleDefinition cannon, SkinDefinition skin, bool tankControls)
        {
            var go = cfg.playerPrefab != null
                ? Object.Instantiate(cfg.playerPrefab, pos, Quaternion.identity, parent)
                : CreatePrimitive(parent, pos, hull, cannon);
            go.tag = "Player";

            var tank = Ensure<PlayerTank>(go);
            Ensure<PlayerMovement>(go);
            Ensure<PlayerWeapon>(go);
            Ensure<PlayerUltimate>(go);
            Ensure<TrajectoryPreview>(go);
            Ensure<PlayerVisuals>(go);
            Ensure<PlayerAudio>(go);
            tank.Init(stats, cfg, tankControls, hull, cannon);

            if (skin != null) ApplySkin(go, skin);
            return tank;
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        /// <summary>Танк из примитивов: корпус (размер — от модуля корпуса), гусеницы, башня с «котом» и стволом пушки.</summary>
        public static GameObject CreatePrimitive(Transform parent, Vector3 pos, TankModuleDefinition hull = null, TankModuleDefinition cannon = null)
        {
            float size = hull != null ? hull.hullScale : 0.8f;
            float barrelLen = cannon != null ? cannon.barrelLength : 0.5f;
            float barrelW = cannon != null ? cannon.barrelWidth : 0.14f;
            var go = Prims.Create("PlayerTank", parent, Prims.Square, new Color(0.30f, 0.55f, 0.30f), pos, Vector2.one * size, 10);
            Prims.Shadow(go.transform, size * 1.45f);
            Prims.Child("TrackL", go.transform, Prims.Square, new Color(0.15f, 0.2f, 0.15f), new Vector2(0, 0.45f), new Vector2(1.05f, 0.18f), 11);
            Prims.Child("TrackR", go.transform, Prims.Square, new Color(0.15f, 0.2f, 0.15f), new Vector2(0, -0.45f), new Vector2(1.05f, 0.18f), 11);

            // башня — отдельный объект без масштаба корпуса, чтобы вращалась независимо
            var turret = new GameObject("Turret").transform;
            turret.SetParent(go.transform, false);
            turret.localScale = new Vector3(1f / size, 1f / size, 1f);
            Prims.Child("Barrel", turret, Prims.Square, new Color(0.2f, 0.35f, 0.2f), new Vector2(0.1f + barrelLen / 2f, 0), new Vector2(barrelLen, barrelW), 12);
            if (cannon != null && cannon.barrelWidth >= 0.2f) // тяжёлое орудие — с дульным тормозом
                Prims.Child("Muzzle", turret, Prims.Square, new Color(0.15f, 0.25f, 0.15f), new Vector2(0.1f + barrelLen, 0), new Vector2(0.1f, barrelW * 1.4f), 12);
            Prims.Child("Cat", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), Vector2.zero, Vector2.one * 0.42f, 13);
            Prims.Child("EarL", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), new Vector2(-0.12f, 0.17f), Vector2.one * 0.14f, 13);
            Prims.Child("EarR", turret, Prims.Circle, new Color(1f, 0.65f, 0.25f), new Vector2(-0.12f, -0.17f), Vector2.one * 0.14f, 13);

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f; // × масштаб корпуса (0.68–0.95) — танк пролезает в проход шириной 1 клетка
            go.AddComponent<Health>();
            return go;
        }

        /// <summary>Перекрасить/переодеть кота. Ищет детей по именам из CreatePrimitive — в префабе назови так же.</summary>
        public static void ApplySkin(GameObject go, SkinDefinition skin)
        {
            var root = go.GetComponent<SpriteRenderer>();
            if (root != null) { root.color = skin.bodyColor; if (skin.bodySprite != null) root.sprite = skin.bodySprite; }
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                switch (sr.gameObject.name)
                {
                    case "TrackL": case "TrackR": sr.color = skin.trackColor; break;
                    case "Barrel": sr.color = Prims.Dark(skin.bodyColor, 0.65f); break;
                    case "Muzzle": sr.color = Prims.Dark(skin.bodyColor, 0.5f); break;
                    case "Cat":
                        sr.color = skin.catColor;
                        if (skin.catSprite != null) sr.sprite = skin.catSprite;
                        break;
                    case "EarL": case "EarR": sr.color = skin.catColor; break;
                }
            }
            var hp = go.GetComponent<Health>();
            if (hp != null) hp.CacheRenderers();
        }
    }
}
