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
        public string exitBlockedText = "Squash all the bugs first!";

        GameUIView _ui;
        PlayerUltimate _ult;
        System.Action _afterTutorial;

        const string SoundPref = "sound_on";
        const string TutorialSeenPref = "tutorial_seen";
        const float SoundVolume = 0.7f;

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
            Sfx.MasterVolume = PlayerPrefs.GetInt(SoundPref, 1) == 1 ? SoundVolume : 0f;
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
            if (_ui.tutorial != null) _ui.tutorial.Closed += HandleTutorialClosed;
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
            BindUltimate(null);
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
            On(m.play, Play);
            On(m.tutorial, () => OpenTutorial(null));
            On(m.settings, () => m.ShowPage(MainMenuView.Page.Settings));
            On(m.quit, flow.Quit);
            On(m.back, () => m.ShowPage(MainMenuView.Page.Main));
            On(m.fullscreen, ToggleFullscreen);
            On(m.sound, ToggleSound);

            var p = _ui.pause;
            On(p.resume, () => flow.Pause(false));
            On(p.restart, flow.RestartLevel);
            On(p.tutorial, () => OpenTutorial(null));
            On(p.settings, () => p.ShowPage(PauseView.Page.Settings));
            On(p.mainMenu, flow.GoToMenu);
            On(p.fullscreen, ToggleFullscreen);
            On(p.sound, ToggleSound);
            On(p.settingsBack, () => p.ShowPage(PauseView.Page.Main));

            RefreshSettingsLabels(Screen.fullScreen);
        }

        /// <summary>Кнопки может не быть в префабе, собранном старой версией сборщика, — тогда просто пропускаем.</summary>
        static void On(PixelButton button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onActivated.AddListener(action);
        }

        // ------------------------------------------------------------------ Обучение

        /// <summary>Play: при первом запуске сначала обучение, игра начнётся по GO / SKIP.</summary>
        void Play()
        {
            if (_ui.tutorial != null && PlayerPrefs.GetInt(TutorialSeenPref, 0) == 0) OpenTutorial(flow.StartNewGame);
            else flow.StartNewGame();
        }

        /// <summary>Карточки поверх меню или паузы; их колонку на это время прячем. after — что сделать после закрытия.</summary>
        void OpenTutorial(System.Action after)
        {
            if (_ui.tutorial == null) { after?.Invoke(); return; } // префаб собран старой версией сборщика
            _afterTutorial = after;
            _ui.menu.mainGroup.SetActive(false);
            _ui.pause.gameObject.SetActive(false);
            _ui.tutorial.Open();
        }

        void HandleTutorialClosed()
        {
            PlayerPrefs.SetInt(TutorialSeenPref, 1);
            var after = _afterTutorial;
            _afterTutorial = null;
            HandleState(flow.State); // вернуть меню или паузу как было
            after?.Invoke();
        }

        /// <summary>Закрыть без последствий (игра сама ушла из меню / паузы).</summary>
        void HideTutorial()
        {
            if (_ui.tutorial == null || !_ui.tutorial.IsOpen) return;
            _afterTutorial = null;
            _ui.tutorial.gameObject.SetActive(false);
        }

        // Настройки одни на главное меню и паузу — подписи обновляем в обоих местах.
        void ToggleFullscreen()
        {
            bool full = !Screen.fullScreen; // Screen.fullScreen обновится только в следующем кадре — запоминаем сами
            Screen.fullScreen = full;
            RefreshSettingsLabels(full);
        }

        void ToggleSound()
        {
            bool on = Sfx.MasterVolume <= 0.001f;
            Sfx.MasterVolume = on ? SoundVolume : 0f;
            PlayerPrefs.SetInt(SoundPref, on ? 1 : 0);
            RefreshSettingsLabels(Screen.fullScreen);
        }

        void RefreshSettingsLabels(bool fullscreen)
        {
            bool sound = Sfx.MasterVolume > 0.001f;
            _ui.menu.SetFullscreenLabel(fullscreen);
            _ui.menu.SetSoundLabel(sound);
            _ui.pause.SetFullscreenLabel(fullscreen);
            _ui.pause.SetSoundLabel(sound);
        }

        // ------------------------------------------------------------------ Реакция на игру

        void HandleState(GameState s)
        {
            _ui.menu.gameObject.SetActive(s == GameState.Menu);
            if (s == GameState.Menu) _ui.menu.ShowPage(MainMenuView.Page.Main); // в меню всегда с главной колонки
            _ui.pause.gameObject.SetActive(s == GameState.Paused);
            if (s == GameState.Paused) _ui.pause.ShowPage(PauseView.Page.Main); // пауза всегда открывается с главной
            if (s != GameState.Menu && s != GameState.Paused) HideTutorial(); // Esc на паузе — обучение закрывается вместе с ней
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
            BindUltimate(ctx.Player != null ? ctx.Player.GetComponent<PlayerUltimate>() : null);
        }

        void HandleLevelCompleted(LevelContext ctx) => _ui.banner.ShowLevelComplete(ctx.LevelNumber);
        void HandlePlayerKilled(LevelContext ctx) => _ui.banner.ShowDeath();
        void HandleExitBlocked() => _ui.hud.ShowToast(exitBlockedText, 1.5f);

        // ------------------------------------------------------------------ Ульта

        /// <summary>Танк создаётся заново на каждом уровне — переподписываемся на его ульту.</summary>
        void BindUltimate(PlayerUltimate ult)
        {
            if (_ult != null) _ult.Changed -= RefreshUltimate;
            _ult = ult;
            if (_ult != null) _ult.Changed += RefreshUltimate;
            RefreshUltimate();
        }

        void RefreshUltimate()
        {
            if (_ui == null) return;
            if (_ult == null) { _ui.hud.SetUltimate(false, 0, 0, 0f, false); return; }
            _ui.hud.SetUltimate(true, _ult.Charges, _ult.settings.maxCharges, _ult.Progress01, _ult.CanActivate);
        }

#if UNITY_EDITOR
        // Пустые ссылки подставляем сами: префаб холста и тему из проекта.
        void OnValidate()
        {
            if (uiPrefab == null)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UiPaths.CanvasPrefab);
                if (prefab != null) { uiPrefab = prefab.GetComponent<GameUIView>(); UnityEditor.EditorUtility.SetDirty(this); }
            }
            if (EditorAssetLookup.FillIfEmpty(ref fallbackTheme)) UnityEditor.EditorUtility.SetDirty(this);
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
