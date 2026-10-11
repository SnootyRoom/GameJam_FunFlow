// Ход игры: меню → уровень → победа/смерть → следующий уровень. Пауза, сложность, события для UI.
// Единственный «глобальный» объект. Игроку и врагам он не нужен — они получают LevelContext через Init.
using System;
using System.Collections;
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public enum GameState { Menu, Playing, Paused, LevelComplete, PlayerDead }

    [RequireComponent(typeof(LevelBuilder), typeof(EntitySpawner))]
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        [Header("Генерация")]
        public LevelSettings settings = new LevelSettings();
        [Range(1, 5)] public int startDifficulty = 1;

        [Header("Правила")]
        [Tooltip("Если в сцене нет UI с меню, игра начнётся сразу")]
        public bool autoStart = true;
        public bool requireAllEnemiesDead = false;   // выход открывается только после зачистки
        public float levelCompleteDelay = 1.5f;      // сколько показывать «Уровень пройден»
        public float restartDelay = 1.5f;            // через сколько перезапуск после смерти
        public bool debugKeys = true;                // R — новый уровень, N — пропустить

        public GameState State { get; private set; } = GameState.Menu;
        public LevelContext Current { get; private set; }
        public int LevelNumber { get; private set; }

        // ----- события (подписывается UI, звук, эффекты) -----
        public event Action<GameState> OnStateChanged;
        public event Action<LevelContext> OnLevelStarted;     // уровень собран, игрок и враги созданы
        public event Action<LevelContext> OnLevelCompleted;   // игрок доехал до Б
        public event Action<LevelContext> OnPlayerKilled;
        public event Action<int, int> OnPlayerHpChanged;      // (сейчас, максимум)
        public event Action OnExitBlocked;                    // въехал в Б, но враги ещё живы

        LevelBuilder _builder;
        EntitySpawner _spawner;
        int _fixedSeed;

        void Awake()
        {
            Instance = this;
            _builder = GetComponent<LevelBuilder>();
            _spawner = GetComponent<EntitySpawner>();
            _fixedSeed = settings.seed;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Start()
        {
            if (autoStart) StartNewGame();
            else SetState(GameState.Menu);
        }

        void Update()
        {
            if (GameInput.PausePressed())
            {
                if (State == GameState.Playing) Pause(true);
                else if (State == GameState.Paused) Pause(false);
            }

            if (debugKeys && State == GameState.Playing)
            {
                if (GameInput.RegeneratePressed()) LoadLevel(0);
                if (GameInput.NextLevelPressed()) CompleteLevel();
            }
        }

        // ------------------------------------------------------------------ Управление игрой

        public void StartNewGame()
        {
            LevelNumber = 1;
            settings.difficulty = startDifficulty;
            LoadLevel(_fixedSeed);
        }

        public void NextLevel()
        {
            LevelNumber++;
            settings.difficulty = Mathf.Min(settings.difficulty + 1, 5);
            LoadLevel(0);
        }

        /// <summary>Этот же уровень заново (тот же сид).</summary>
        public void RestartLevel()
        {
            LoadLevel(Current != null ? Current.Data.seed : 0);
        }

        public void Pause(bool paused)
        {
            if (paused && State != GameState.Playing) return;
            if (!paused && State != GameState.Paused) return;
            Time.timeScale = paused ? 0f : 1f;
            SetState(paused ? GameState.Paused : GameState.Playing);
        }

        public void GoToMenu()
        {
            Teardown();
            Time.timeScale = 1f;
            SetState(GameState.Menu);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Засчитать уровень (вызывается выходом; можно и вручную).</summary>
        public void CompleteLevel()
        {
            if (State != GameState.Playing || Current == null) return;
            if (requireAllEnemiesDead && Current.AliveEnemyCount() > 0)
            {
                OnExitBlocked?.Invoke();
                return;
            }
            SetState(GameState.LevelComplete);
            Time.timeScale = 0f; // замираем, пока висит надпись
            OnLevelCompleted?.Invoke(Current);
            StartCoroutine(AfterRealtime(levelCompleteDelay, NextLevel));
        }

        // ------------------------------------------------------------------ Загрузка уровня

        /// <summary>seed = 0 — случайный уровень.</summary>
        void LoadLevel(int seed)
        {
            Teardown();
            Time.timeScale = 1f;

            settings.seed = seed;
            var data = BspLevelGenerator.Generate(settings);
            settings.seed = _fixedSeed;

            var root = _builder.Build(data);
            var ctx = new LevelContext
            {
                Data = data,
                Pathfinder = new GridPathfinder(data),
                Root = root,
                LevelNumber = LevelNumber,
                Difficulty = settings.difficulty,
                Exit = _builder.Exit.transform,
            };
            Current = ctx;
            _spawner.SpawnAll(ctx);

            _builder.Exit.PlayerEntered += CompleteLevel;
            if (ctx.PlayerHealth != null)
            {
                ctx.PlayerHealth.OnDamaged += HandlePlayerDamaged;
                ctx.PlayerHealth.OnDeath += HandlePlayerDeath;
            }

            SetupCamera(ctx);
            Debug.Log($"[GameFlow] Уровень {LevelNumber}: сид {data.seed}, сложность {settings.difficulty}, " +
                      $"врагов {data.enemies.Count}");

            SetState(GameState.Playing);
            OnLevelStarted?.Invoke(ctx);
            if (ctx.PlayerHealth != null) OnPlayerHpChanged?.Invoke(ctx.PlayerHealth.Hp, ctx.PlayerHealth.maxHp);
        }

        void Teardown()
        {
            StopAllCoroutines();
            if (Current != null)
            {
                if (_builder.Exit != null) _builder.Exit.PlayerEntered -= CompleteLevel;
                if (Current.PlayerHealth != null)
                {
                    Current.PlayerHealth.OnDamaged -= HandlePlayerDamaged;
                    Current.PlayerHealth.OnDeath -= HandlePlayerDeath;
                }
            }
            _builder.Clear();
            Current = null;
        }

        void HandlePlayerDamaged(int hp)
        {
            if (Current != null && Current.PlayerHealth != null) OnPlayerHpChanged?.Invoke(hp, Current.PlayerHealth.maxHp);
        }

        void HandlePlayerDeath()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.PlayerDead);
            OnPlayerKilled?.Invoke(Current);
            StartCoroutine(AfterRealtime(restartDelay, RestartLevel));
        }

        void SetupCamera(LevelContext ctx)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.Init(ctx);
        }

        void SetState(GameState s)
        {
            State = s;
            OnStateChanged?.Invoke(s);
        }

        static IEnumerator AfterRealtime(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }
    }
}
