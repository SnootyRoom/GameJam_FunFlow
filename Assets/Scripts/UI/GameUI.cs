// Контроллер интерфейса: создаёт холст из префаба GameCanvas, назначает кнопкам действия и
// переключает экраны по состоянию игры. Вёрстки здесь нет — она в префабах Assets/Prefabs/UI
// (собираются командой CatTanks → UI → Собрать префабы UI, дальше правятся руками).
using UnityEngine;

namespace CatTanks.UI
{
    public class GameUI : MonoBehaviour
    {
        [Header("Связи")]
        public GameFlow flow;                 // пусто = найдётся сам
        public bool showMainMenu = true;      // false — игра стартует сразу, меню только по паузе

        [Header("Префаб")]
        [Tooltip("Assets/Prefabs/UI/GameCanvas. Пусто в редакторе = подставится сам")]
        public GameUIView uiPrefab;
        [Tooltip("Если префаба нет — UI соберётся из этой темы прямо в игре")]
        public UiTheme fallbackTheme;

        [Header("Тексты")]
        public string exitBlockedText = "Deal with all the phones first!";

        GameUIView _ui;

        // ------------------------------------------------------------------ Жизненный цикл

        void Awake()
        {
            if (flow == null)
            {
#if UNITY_2023_1_OR_NEWER
                flow = FindAnyObjectByType<GameFlow>();
#else
                flow = FindObjectOfType<GameFlow>();
#endif
            }
            if (flow == null) { Debug.LogError("[GameUI] В сцене нет GameFlow."); enabled = false; return; }
            if (showMainMenu) flow.autoStart = false; // ждём кнопку «Play»

            UIFactory.EnsureEventSystem();
            if (uiPrefab != null) _ui = Instantiate(uiPrefab, transform);
            else
            {
                Debug.LogWarning("[GameUI] Нет префаба GameCanvas — собираю UI из темы. " +
                                 "Создай префабы: CatTanks → UI → Собрать префабы UI.", this);
                _ui = GameUIBuilder.Build(transform, fallbackTheme != null ? fallbackTheme : ScriptableObject.CreateInstance<UiTheme>());
            }
            WireButtons();

            // подписываемся в Awake: GameFlow может стартовать раньше нашего Start
            flow.OnStateChanged += HandleState;
            flow.OnLevelStarted += HandleLevelStarted;
            flow.OnLevelCompleted += HandleLevelCompleted;
            flow.OnPlayerKilled += HandlePlayerKilled;
            flow.OnPlayerHpChanged += _ui.hud.SetHearts;
            flow.OnExitBlocked += HandleExitBlocked;
            flow.OnEnemyCountChanged += _ui.hud.SetEnemies;
        }

        void Start()
        {
            HandleState(flow.State);
            if (flow.Current != null) HandleLevelStarted(flow.Current);
        }

        void OnDestroy()
        {
            Cursor.visible = true;
            if (flow == null || _ui == null) return;
            flow.OnStateChanged -= HandleState;
            flow.OnLevelStarted -= HandleLevelStarted;
            flow.OnLevelCompleted -= HandleLevelCompleted;
            flow.OnPlayerKilled -= HandlePlayerKilled;
            flow.OnPlayerHpChanged -= _ui.hud.SetHearts;
            flow.OnExitBlocked -= HandleExitBlocked;
            flow.OnEnemyCountChanged -= _ui.hud.SetEnemies;
        }

        void Update()
        {
            if (flow == null) return;
            if (_ui.hud.gameObject.activeSelf)
                _ui.hud.UpdateExitArrow(Camera.main, flow.Current != null ? flow.Current.Exit : null);
            _ui.hud.UpdateCrosshair(GameInput.MouseScreen());
        }

        // ------------------------------------------------------------------ Кнопки

        void WireButtons()
        {
            var m = _ui.menu;
            m.play.onActivated.AddListener(flow.StartNewGame);
            m.settings.onActivated.AddListener(() => m.ShowSettings(true));
            m.quit.onActivated.AddListener(flow.Quit);
            m.back.onActivated.AddListener(() => m.ShowSettings(false));
            m.fullscreen.onActivated.AddListener(() =>
            {
                bool full = !Screen.fullScreen; // Screen.fullScreen обновится только в следующем кадре — запоминаем сами
                Screen.fullScreen = full;
                m.SetFullscreenLabel(full);
            });
            m.SetFullscreenLabel(Screen.fullScreen);

            var p = _ui.pause;
            p.resume.onActivated.AddListener(() => flow.Pause(false));
            p.restart.onActivated.AddListener(flow.RestartLevel);
            p.mainMenu.onActivated.AddListener(flow.GoToMenu);
        }

        // ------------------------------------------------------------------ Реакция на игру

        void HandleState(GameState s)
        {
            _ui.menu.gameObject.SetActive(s == GameState.Menu);
            if (s == GameState.Menu) _ui.menu.ShowSettings(false); // в меню всегда с главной колонки
            _ui.pause.gameObject.SetActive(s == GameState.Paused);
            _ui.hud.gameObject.SetActive(s != GameState.Menu);
            _ui.banner.gameObject.SetActive(s == GameState.LevelComplete || s == GameState.PlayerDead);
            bool playing = s == GameState.Playing;
            _ui.hud.SetCrosshairVisible(playing);
            Cursor.visible = !playing; // в бою вместо курсора — прицел
        }

        void HandleLevelStarted(LevelContext ctx)
        {
            _ui.hud.SetLevel(ctx.LevelNumber);
            _ui.hud.SetEnemies(ctx.AliveEnemyCount());
            if (ctx.PlayerHealth != null) _ui.hud.SetHearts(ctx.PlayerHealth.Hp, ctx.PlayerHealth.maxHp);
        }

        void HandleLevelCompleted(LevelContext ctx) => _ui.banner.ShowLevelComplete(ctx.LevelNumber);
        void HandlePlayerKilled(LevelContext ctx) => _ui.banner.ShowDeath();
        void HandleExitBlocked() => _ui.hud.ShowToast(exitBlockedText, 1.5f);

#if UNITY_EDITOR
        // Пустые ссылки подставляем сами: префаб холста и тему из проекта.
        void OnValidate()
        {
            if (uiPrefab == null)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UiPaths.CanvasPrefab);
                if (prefab != null) { uiPrefab = prefab.GetComponent<GameUIView>(); UnityEditor.EditorUtility.SetDirty(this); }
            }
            if (fallbackTheme == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("t:" + nameof(UiTheme));
                if (guids.Length > 0)
                {
                    fallbackTheme = UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }
        }
#endif
    }

    /// <summary>Где лежат префабы UI. Общие для сборщика и контроллера.</summary>
    public static class UiPaths
    {
        public const string Folder = "Assets/Prefabs/UI";
        public const string CanvasPrefab = Folder + "/GameCanvas.prefab";
        public const string GeneratedArt = "Assets/Art/UI/Generated";
    }
}
