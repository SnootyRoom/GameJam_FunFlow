// Команды редактора CatTanks → UI.
//   «Собрать префабы UI»: собирает UI по теме (GameUIBuilder) в префабы Assets/Prefabs/UI —
//     экраны Hud, Banner, Pause, GameOver, MainMenu, Tutorial отдельными префабами и холст GameCanvas, в который они вложены;
//     заодно сохраняет пиксельные заготовки в PNG и подставляет GameCanvas в GameUI открытой сцены.
//   «Пересобрать один экран»: только выбранный экран, остальные не трогаются.
//
// РУЧНЫЕ ПРАВКИ НЕ ТЕРЯЮТСЯ. После сборки запоминается «отпечаток» каждого префаба (Assets/Prefabs/UI/.ui-bake.json —
// файл с точкой Unity не импортирует, но он попадает в git и общий для всей команды). При следующей сборке:
//   • экран не меняли руками — пересобирается молча;
//   • экран правили руками — сборщик спросит: перезаписать или оставить ручную версию
//     (оставленная встраивается в новый холст как есть).
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using CatTanks.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatTanks.EditorTools
{
    public static class UiPrefabBaker
    {
        const string CanvasName = "GameCanvas";
        static string ManifestPath => UiPaths.Folder + "/.ui-bake.json";

        /// <summary>Экраны UI: имя префаба, как достать его из холста и как подменить на готовый префаб.</summary>
        struct Screen
        {
            public string name;
            public Func<GameUIView, Component> get;
            public Action<GameUIView, GameObject> set;
            public string Path => $"{UiPaths.Folder}/{name}.prefab";
        }

        static readonly Screen[] Screens =
        {
            new Screen { name = "Hud", get = v => v.hud, set = (v, go) => v.hud = go.GetComponent<HudView>() },
            new Screen { name = "Banner", get = v => v.banner, set = (v, go) => v.banner = go.GetComponent<BannerView>() },
            new Screen { name = "Pause", get = v => v.pause, set = (v, go) => v.pause = go.GetComponent<PauseView>() },
            new Screen { name = "GameOver", get = v => v.gameOver, set = (v, go) => v.gameOver = go.GetComponent<GameOverView>() },
            new Screen { name = "MainMenu", get = v => v.menu, set = (v, go) => v.menu = go.GetComponent<MainMenuView>() },
            new Screen { name = "Tutorial", get = v => v.tutorial, set = (v, go) => v.tutorial = go.GetComponent<TutorialView>() },
        };

        // ------------------------------------------------------------------ Команды

        [MenuItem("CatTanks/UI/Собрать префабы UI")]
        static void Bake()
        {
            var manifest = Manifest.Load();
            var edited = new List<string>();
            foreach (var s in Screens) if (IsEdited(s.Path, manifest)) edited.Add(s.name);
            bool canvasEdited = IsEdited(UiPaths.CanvasPrefab, manifest);
            if (canvasEdited) edited.Add(CanvasName);

            bool overwriteEdited = false;
            if (edited.Count > 0)
            {
                int choice = EditorUtility.DisplayDialogComplex("Собрать префабы UI",
                    "Эти префабы правили руками после прошлой сборки:\n\n  " + string.Join(", ", edited) +
                    "\n\nНетронутые экраны пересоберутся в любом случае. Что сделать с изменёнными?",
                    "Оставить мои правки", "Отмена", "Перезаписать из темы");
                if (choice == 1) return;
                overwriteEdited = choice == 2;
            }

            var theme = PrepareTheme();
            Directory.CreateDirectory(UiPaths.Folder);
            var temp = new GameObject("UiBakeTemp");
            try
            {
                var view = GameUIBuilder.Build(temp.transform, theme);
                var kept = new List<string>();
                foreach (var s in Screens)
                {
                    if (!overwriteEdited && edited.Contains(s.name)) { KeepExisting(view, s); kept.Add(s.name); }
                    else SaveScreen(view, s, manifest);
                }

                GameUIView canvas;
                if (!overwriteEdited && canvasEdited)
                {
                    // холст правили руками — не трогаем; экраны в нём — ссылки на префабы, они уже обновлены
                    canvas = AssetDatabase.LoadAssetAtPath<GameObject>(UiPaths.CanvasPrefab).GetComponent<GameUIView>();
                    kept.Add(CanvasName);
                }
                else
                {
                    canvas = PrefabUtility.SaveAsPrefabAsset(view.gameObject, UiPaths.CanvasPrefab).GetComponent<GameUIView>();
                    manifest.Set(UiPaths.CanvasPrefab, Hash(UiPaths.CanvasPrefab));
                }
                manifest.Save();

                AssignToScene(canvas, theme);
                Debug.Log($"[UI] Префабы собраны в {UiPaths.Folder}." +
                          (kept.Count > 0 ? " Оставлены ручные версии: " + string.Join(", ", kept) + "." : ""), canvas);
                EditorGUIUtility.PingObject(canvas);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        [MenuItem("CatTanks/UI/Пересобрать один экран/HUD")] static void BakeHud() => BakeOne("Hud");
        [MenuItem("CatTanks/UI/Пересобрать один экран/Баннер")] static void BakeBanner() => BakeOne("Banner");
        [MenuItem("CatTanks/UI/Пересобрать один экран/Пауза")] static void BakePause() => BakeOne("Pause");
        [MenuItem("CatTanks/UI/Пересобрать один экран/Game Over")] static void BakeGameOver() => BakeOne("GameOver");
        [MenuItem("CatTanks/UI/Пересобрать один экран/Главное меню")] static void BakeMenu() => BakeOne("MainMenu");
        [MenuItem("CatTanks/UI/Пересобрать один экран/Обучение")] static void BakeTutorial() => BakeOne("Tutorial");

        /// <summary>Пересобрать из темы только один экран. Холст ссылается на префаб экрана — его трогать не нужно.</summary>
        static void BakeOne(string name)
        {
            var screen = Array.Find(Screens, s => s.name == name);
            if (!File.Exists(UiPaths.CanvasPrefab) || !File.Exists(screen.Path))
            {
                Debug.Log($"[UI] Префаба {name} ещё нет — собираю весь UI.");
                Bake();
                return;
            }
            var manifest = Manifest.Load();
            if (IsEdited(screen.Path, manifest) &&
                !EditorUtility.DisplayDialog("Пересобрать " + name,
                    $"{name} правили руками после прошлой сборки. Пересборка из темы перезапишет эти правки.\n\nПродолжить?",
                    "Перезаписать", "Отмена"))
                return;

            var theme = PrepareTheme();
            var temp = new GameObject("UiBakeTemp");
            try
            {
                var view = GameUIBuilder.Build(temp.transform, theme);
                SaveScreen(view, screen, manifest);
                manifest.Save();
                Debug.Log($"[UI] Экран {name} пересобран, остальные не тронуты.");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(screen.Path));
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        // ------------------------------------------------------------------ Экраны

        static UiTheme PrepareTheme()
        {
            var theme = FindOrCreateTheme();
            SaveGeneratedSprites(theme);
            return theme;
        }

        static void SaveScreen(GameUIView view, Screen s, Manifest manifest)
        {
            // после сохранения объект в иерархии становится экземпляром префаба → холст сохранится со вложенными префабами
            PrefabUtility.SaveAsPrefabAssetAndConnect(s.get(view).gameObject, s.Path, InteractionMode.AutomatedAction);
            manifest.Set(s.Path, Hash(s.Path));
        }

        /// <summary>Собранный из темы экран убираем, на его место — уже существующий (ручной) префаб.</summary>
        static void KeepExisting(GameUIView view, Screen s)
        {
            var built = s.get(view).transform;
            int index = built.GetSiblingIndex();
            var parent = built.parent;
            Object.DestroyImmediate(built.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(s.Path), parent);
            instance.transform.SetSiblingIndex(index);
            s.set(view, instance);
        }

        // ------------------------------------------------------------------ Отпечатки префабов

        /// <summary>Префаб есть, а его отпечаток не совпадает с записанным при сборке (или записи нет) — значит, правили руками.</summary>
        static bool IsEdited(string path, Manifest manifest)
        {
            if (!File.Exists(path)) return false;
            string recorded = manifest.Get(path);
            return recorded == null || recorded != Hash(path);
        }

        static string Hash(string path)
        {
            using (var md5 = MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }

        [Serializable]
        class Manifest
        {
            [Serializable] public class Entry { public string path; public string hash; }
            public List<Entry> entries = new List<Entry>();

            public static Manifest Load()
            {
                if (!File.Exists(ManifestPath)) return new Manifest();
                return JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) ?? new Manifest();
            }

            public void Save() => File.WriteAllText(ManifestPath, JsonUtility.ToJson(this, true));

            public string Get(string path) => entries.Find(e => e.path == path)?.hash;

            public void Set(string path, string hash)
            {
                var e = entries.Find(x => x.path == path);
                if (e == null) entries.Add(e = new Entry { path = path });
                e.hash = hash;
            }
        }

        static UiTheme FindOrCreateTheme()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(UiTheme));
            if (guids.Length > 0) return AssetDatabase.LoadAssetAtPath<UiTheme>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var theme = ScriptableObject.CreateInstance<UiTheme>();
            AssetDatabase.CreateAsset(theme, "Assets/Data/UiTheme.asset");
            return theme;
        }

        // ------------------------------------------------------------------ Пиксельные заготовки → PNG

        static void SaveGeneratedSprites(UiTheme t)
        {
            Directory.CreateDirectory(UiPaths.GeneratedArt);
            if (t.pixelPanel == null) t.pixelPanel = SavePng(UIFactory.PixelPanelSprite(), "pixel_panel", new Vector4(4, 4, 4, 4), FilterMode.Point);
            if (t.circle == null) t.circle = SavePng(Prims.Circle, "circle", Vector4.zero, FilterMode.Bilinear);
            if (t.ring == null) t.ring = SavePng(Prims.Ring, "ring", Vector4.zero, FilterMode.Bilinear);
            if (t.pixelArrow == null) t.pixelArrow = SavePng(UIFactory.PixelArrowSprite(), "pixel_arrow", Vector4.zero, FilterMode.Point);
            // палитру могли поменять в теме — плашку паузы перерисовываем всегда
            t.paperPanel = SavePng(UIFactory.PaperSprite(t.pauseInk, t.pausePaperLight, t.pausePaper, t.pausePaperShade,
                                                         new Color32(0, 0, 0, 110)), "paper_panel", new Vector4(4, 4, 4, 4), FilterMode.Point);

            // пустые поля темы заполняем по имени файла в Assets/Art/UI — достаточно положить картинку с нужным именем
            FillFromArt(ref t.startButton, "button_start");
            FillFromArt(ref t.tutorialButton, "button_tutorial");
            FillFromArt(ref t.settingsButton, "button_settings");
            FillFromArt(ref t.exitButton, "button_exit");
            FillFromArt(ref t.heartFull, "heart_full");
            FillFromArt(ref t.heartEmpty, "heart_empty");
            FillFromArt(ref t.pauseContinueButton, "pause_continue");
            FillFromArt(ref t.pauseRestartButton, "pause_restart");
            FillFromArt(ref t.pauseTutorialButton, "pause_tutorial");
            FillFromArt(ref t.pauseSettingsButton, "pause_settings");
            FillFromArt(ref t.pauseMainMenuButton, "pause_main_menu");
            FillFromArt(ref t.nextButton, "button_next");
            FillFromArt(ref t.goButton, "button_go");
            FillFromArt(ref t.skipButton, "button_skip");
            FillFromArt(ref t.tutorialFish, "finish_003", "Assets/Art/Environment");
            FillFromArt(ref t.ammoIcon, "snarad", "Assets/Art/Characters");
            // звезда из кода (pixel_star) — только запасной вариант: появилась картинка художника — берём её
            if (t.starIcon != null && AssetDatabase.GetAssetPath(t.starIcon).StartsWith(UiPaths.GeneratedArt)) t.starIcon = null;
            FillFromArt(ref t.starIcon, "star_active");
            FillFromArt(ref t.starIcon, "star");
            FillFromArt(ref t.starInactive, "star_inactive");
            FillFromArt(ref t.hpIcon, "hp_icon");
            FillFromArt(ref t.hpFrame, "hp_frame");
            FillFromArt(ref t.hpSegmentFull, "hp_segment_full");
            FillFromArt(ref t.hpSegmentEmpty, "hp_segment_empty");
            FillFromArt(ref t.hpSegmentLost, "hp_segment_lost");
            FillFromArt(ref t.bossHpFrame, "boss_hp_frame");
            FillFromArt(ref t.bossHpSegmentFull, "boss_hp_segment_full");
            FillFromArt(ref t.bossHpSegmentEmpty, "boss_hp_segment_empty");
            if (t.starIcon == null) t.starIcon = SavePng(UIFactory.PixelStarSprite(), "pixel_star", Vector4.zero, FilterMode.Point);
            foreach (var s in new[] { t.startButton, t.tutorialButton, t.settingsButton, t.exitButton,
                                      t.heartFull, t.heartEmpty, t.pauseContinueButton, t.pauseRestartButton, t.pauseTutorialButton,
                                      t.pauseSettingsButton, t.pauseMainMenuButton, t.nextButton, t.goButton, t.skipButton, t.ammoIcon,
                                      t.starIcon, t.starInactive, t.hpIcon, t.hpFrame, t.hpSegmentFull, t.hpSegmentEmpty,
                                      t.hpSegmentLost, t.bossHpFrame, t.bossHpSegmentFull, t.bossHpSegmentEmpty })
                MakeCrisp(s);

            FillWorldUiArt();

            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Полоски HP над врагами — в FxLibrary (они в мире, а не в UI), но картинки лежат рядом с UI.</summary>
        static void FillWorldUiArt()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CatTanks.FxLibrary)))
            {
                var lib = AssetDatabase.LoadAssetAtPath<CatTanks.FxLibrary>(AssetDatabase.GUIDToAssetPath(guid));
                FillFromArt(ref lib.enemyHpFrame, "enemy_hp_frame");
                FillFromArt(ref lib.enemyHpFill, "enemy_hp_fill");
                MakeCrisp(lib.enemyHpFrame);
                MakeCrisp(lib.enemyHpFill);
                EditorUtility.SetDirty(lib);
            }
        }

        /// <summary>Пустое поле темы → спрайт из {folder}/{file}.png, если такая картинка есть.</summary>
        static void FillFromArt(ref Sprite field, string file, string folder = "Assets/Art/UI")
        {
            if (field != null) return;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath($"{folder}/{file}.png"))
                if (a is Sprite s) { field = s; return; }
        }

        /// <summary>Пиксель-арт масштабируется в UI — без сглаживания и сжатия, иначе края блоков мылятся.</summary>
        static void MakeCrisp(Sprite sprite)
        {
            if (sprite == null) return;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter;
            if (importer == null) return;
            if (importer.filterMode == FilterMode.Point && importer.textureCompression == TextureImporterCompression.Uncompressed) return;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        static Sprite SavePng(Sprite source, string name, Vector4 border, FilterMode filter)
        {
            string path = $"{UiPaths.GeneratedArt}/{name}.png";
            File.WriteAllBytes(path, source.texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = source.pixelsPerUnit;
            importer.spriteBorder = border;
            importer.filterMode = filter;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------ Сцена

        static void AssignToScene(GameUIView canvas, UiTheme theme)
        {
#if UNITY_2023_1_OR_NEWER
            var all = Object.FindObjectsByType<GameUI>(FindObjectsInactive.Include);
#else
            var all = Object.FindObjectsOfType<GameUI>(true);
#endif
            foreach (var ui in all)
            {
                Undo.RecordObject(ui, "Assign UI prefab");
                ui.uiPrefab = canvas;
                ui.fallbackTheme = theme;
                EditorUtility.SetDirty(ui);
                EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            }
        }
    }
}
