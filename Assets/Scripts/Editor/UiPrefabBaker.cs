// Команда редактора CatTanks → UI → Собрать префабы UI.
// 1) Сохраняет сгенерированные кодом пиксельные заготовки (подложка, стрелка, круг…) в PNG — префабы
//    не могут ссылаться на картинки, созданные в памяти.
// 2) Собирает UI по теме (GameUIBuilder) и сохраняет: Hud, Banner, Pause, MainMenu — отдельные префабы,
//    GameCanvas — холст, в который они вложены.
// 3) Подставляет GameCanvas в GameUI открытой сцены.
// Пересборка ПЕРЕЗАПИСЫВАЕТ ручные правки в этих префабах — команда спросит подтверждение.
using System.IO;
using CatTanks.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatTanks.EditorTools
{
    public static class UiPrefabBaker
    {
        [MenuItem("CatTanks/UI/Собрать префабы UI")]
        static void Bake()
        {
            if (File.Exists(UiPaths.CanvasPrefab) &&
                !EditorUtility.DisplayDialog("Собрать префабы UI",
                    "Префабы UI уже есть. Пересборка из темы перезапишет все ручные правки в них.\n\nПродолжить?",
                    "Пересобрать", "Отмена"))
                return;

            var theme = FindOrCreateTheme();
            SaveGeneratedSprites(theme);

            Directory.CreateDirectory(UiPaths.Folder);
            var temp = new GameObject("UiBakeTemp");
            try
            {
                var view = GameUIBuilder.Build(temp.transform, theme);

                // экраны — отдельными префабами, чтобы разные люди могли править их без конфликтов
                SaveScreen(view.hud.gameObject, "Hud");
                SaveScreen(view.banner.gameObject, "Banner");
                SaveScreen(view.pause.gameObject, "Pause");
                SaveScreen(view.menu.gameObject, "MainMenu");

                var canvas = PrefabUtility.SaveAsPrefabAsset(view.gameObject, UiPaths.CanvasPrefab);
                AssignToScene(canvas.GetComponent<GameUIView>(), theme);
                Debug.Log($"[UI] Префабы собраны в {UiPaths.Folder}. Правь вёрстку прямо в них.", canvas);
                EditorGUIUtility.PingObject(canvas);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        static void SaveScreen(GameObject screen, string name)
        {
            // после сохранения объект в иерархии становится экземпляром префаба → холст сохранится со вложенными префабами
            PrefabUtility.SaveAsPrefabAssetAndConnect(screen, $"{UiPaths.Folder}/{name}.prefab", InteractionMode.AutomatedAction);
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
            if (t.triangle == null) t.triangle = SavePng(Prims.Triangle, "triangle", Vector4.zero, FilterMode.Bilinear);
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
            foreach (var s in new[] { t.startButton, t.tutorialButton, t.settingsButton, t.exitButton,
                                      t.heartFull, t.heartEmpty, t.pauseContinueButton, t.pauseRestartButton, t.pauseTutorialButton,
                                      t.pauseSettingsButton, t.pauseMainMenuButton })
                MakeCrisp(s);

            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Пустое поле темы → спрайт из Assets/Art/UI/{file}.png, если такая картинка есть.</summary>
        static void FillFromArt(ref Sprite field, string file)
        {
            if (field != null) return;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath($"Assets/Art/UI/{file}.png"))
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
