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
        System.Action _afterTutorial;
        bool _tutorialFirstRun;               // обучение открыто по первому START
        bool _titleDone;

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
            GameSettings.Apply();
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
            flow.OnExitBlocked += HandleExitBlocked;
            if (_ui.tutorial != null) _ui.tutorial.Closed += HandleTutorialClosed;
            if (_ui.menu.title != null) _ui.menu.title.Finished += HandleTitleFinished;
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
            flow.OnExitBlocked -= HandleExitBlocked;
            if (_ui.hud != null) _ui.hud.Bind(flow, null); // виджеты отписываются от танка и уровня
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

        /// <summary>
        /// START. При самом первом запуске уровень загружается и сразу замирает, а поверх арены открывается
        /// обучение; по GO время идёт. Дальше START просто запускает игру (обучение — по кнопке TUTORIAL).
        /// </summary>
        void Play()
        {
            flow.StartNewGame();
            if (_ui.tutorial != null && !GameSettings.TutorialSeen) OpenTutorialInGame();
        }

        /// <summary>Обучение поверх только что загруженной арены: время стоит, Esc не работает, виден обычный курсор.</summary>
        void OpenTutorialInGame()
        {
            _tutorialFirstRun = true;
            _afterTutorial = null;
            GameTime.Set(this, 0f);
            flow.PauseBlocked = true;
            Cursor.visible = true;
            _ui.tutorial.Open();
        }

        /// <summary>Карточки поверх меню или паузы; их колонку на это время прячем. after — что сделать после закрытия.</summary>
        void OpenTutorial(System.Action after)
        {
            if (_ui.tutorial == null) { after?.Invoke(); return; } // префаб собран старой версией сборщика
            _afterTutorial = after;
            _tutorialFirstRun = false;
            _ui.menu.mainGroup.SetActive(false);
            _ui.pause.gameObject.SetActive(false);
            _ui.tutorial.Open();
        }

        void HandleTutorialClosed()
        {
            if (_tutorialFirstRun) GameSettings.TutorialSeen = true; // отметку ставит только показ перед первой игрой
            ReleaseTutorialHold();
            var after = _afterTutorial;
            _afterTutorial = null;
            HandleState(flow.State); // вернуть меню, паузу или бой как было
            after?.Invoke();
        }

        /// <summary>Закрыть без последствий (игра сама ушла из меню / паузы).</summary>
        void HideTutorial()
        {
            if (_ui.tutorial == null || !_ui.tutorial.IsOpen) return;
            _afterTutorial = null;
            ReleaseTutorialHold();
            _ui.tutorial.gameObject.SetActive(false);
        }

        /// <summary>Снять «стоп» с арены, если обучение было открыто поверх неё.</summary>
        void ReleaseTutorialHold()
        {
            if (!_tutorialFirstRun) return;
            _tutorialFirstRun = false;
            GameTime.Clear(this);
            flow.PauseBlocked = false;
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
            GameSettings.SoundOn = !GameSettings.SoundOn;
            RefreshSettingsLabels(Screen.fullScreen);
        }

        void RefreshSettingsLabels(bool fullscreen)
        {
            bool sound = GameSettings.SoundOn;
            _ui.menu.SetFullscreenLabel(fullscreen);
            _ui.menu.SetSoundLabel(sound);
            _ui.pause.SetFullscreenLabel(fullscreen);
            _ui.pause.SetSoundLabel(sound);
        }

        // ------------------------------------------------------------------ Реакция на игру

        void HandleState(GameState s)
        {
            _ui.menu.gameObject.SetActive(s == GameState.Menu);
            if (s == GameState.Menu) EnterMenu();
            _ui.pause.gameObject.SetActive(s == GameState.Paused);
            if (s == GameState.Paused) _ui.pause.ShowPage(PauseView.Page.Main); // пауза всегда открывается с главной
            if (s != GameState.Menu && s != GameState.Paused) HideTutorial(); // Esc на паузе — обучение закрывается вместе с ней
            _ui.hud.gameObject.SetActive(s != GameState.Menu);
            _ui.banner.gameObject.SetActive(s == GameState.LevelComplete || s == GameState.PlayerDead);
            Cursor.visible = s != GameState.Playing; // в бою вместо курсора — прицел (CrosshairWidget)
        }

        /// <summary>При запуске — стартовый экран, дальше всегда главная колонка (кнопки выезжают).</summary>
        void EnterMenu()
        {
            var title = _ui.menu.title;
            if (!_titleDone && title != null)
            {
                _ui.menu.ShowPage(MainMenuView.Page.Title);
                title.Show();
                return;
            }
            _ui.menu.ShowPage(MainMenuView.Page.Main);
            _ui.menu.PlayIntro();
        }

        void HandleTitleFinished()
        {
            _titleDone = true;
            if (flow.State == GameState.Menu) EnterMenu();
        }

        // HUD сам следит за танком, ультой, звёздами и т.д. — ему нужен только новый уровень
        void HandleLevelStarted(LevelContext ctx) => _ui.hud.Bind(flow, ctx);

        void HandleLevelCompleted(LevelContext ctx)
        {
            var stars = ctx.Stars;
            string[] labels = null;
            if (stars != null)
            {
                labels = new string[stars.Rules.Length];
                for (int i = 0; i < labels.Length; i++) labels[i] = LevelStars.Label(stars.Rules[i]);
            }
            _ui.banner.ShowLevelComplete(ctx.LevelNumber, stars?.Result, labels);
        }
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
