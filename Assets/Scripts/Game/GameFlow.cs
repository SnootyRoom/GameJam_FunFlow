// Ход игры: меню → уровень → победа/смерть → следующий уровень. Пауза, сложность, события для UI.
// Синглтона нет: UI получает ссылку через инспектор, игрок и враги — LevelContext через Init.
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
        [Header("Генерация")]
        [Tooltip("Пресет генерации. Пусто в редакторе = подставится первый LevelConfig из проекта")]
        public LevelConfig config;

        [Header("Эффекты")]
        [Tooltip("Картинки эффектов: выстрел, рикошет, попадание, смерть врага. Пусто в редакторе = подставится из проекта")]
        public FxLibrary fx;

        [Header("Правила")]
        [Tooltip("Если в сцене нет UI с меню, игра начнётся сразу")]
        public bool autoStart = true;
        public bool requireAllEnemiesDead = false;   // выход открывается только после зачистки
        [Tooltip("Снаряды ранят своих (враги — врагов, игрок — себя после рикошета)")]
        public bool friendlyFire = true;
        [Tooltip("Снаряды сбивают друг друга, как в Wii Tanks: можно подставить свою пулю под вражескую")]
        public bool bulletsCollide = true;
        public float levelCompleteDelay = 1.5f;      // сколько показывать «Уровень пройден»
        public float restartDelay = 1.5f;            // через сколько перезапуск после смерти
        [Tooltip("Сколько дольше висит «Уровень пройден», чтобы звёзды успели вылететь")]
        public float starsShowTime = 1.2f;
        public bool debugKeys = true;                // R — новый уровень, N — пропустить

        public GameState State { get; private set; } = GameState.Menu;
        public LevelContext Current { get; private set; }
        public int LevelNumber { get; private set; }
        public int Difficulty { get; private set; }
        /// <summary>Звёзд собрано за текущий забег (с нажатия Play).</summary>
        public int TotalStars { get; private set; }
        /// <summary>Esc не ставит паузу (например, пока поверх арены открыто обучение).</summary>
        public bool PauseBlocked { get; set; }

        // ----- события (подписывается UI, звук, эффекты) -----
        public event Action<GameState> OnStateChanged;
        public event Action<LevelContext> OnLevelStarted;     // уровень собран, игрок и враги созданы
        public event Action<LevelContext> OnLevelCompleted;   // игрок доехал до Б
        public event Action<LevelContext> OnPlayerKilled;
        public event Action<int, int> OnPlayerHpChanged;      // (сейчас, максимум)
        public event Action<GameObject> OnEnemyKilled;
        public event Action<int> OnEnemyCountChanged;         // сколько врагов живо
        public event Action OnExitBlocked;                    // въехал в Б, но враги ещё живы

        LevelBuilder _builder;
        EntitySpawner _spawner;
        FxPlayer _fx;
        SfxPlayer _sfx;

        LevelSettings Settings => config.generation;

        void Awake()
        {
            _builder = GetComponent<LevelBuilder>();
            _spawner = GetComponent<EntitySpawner>();
            if (config == null)
            {
                Debug.LogWarning("[GameFlow] Не назначен LevelConfig — беру настройки по умолчанию.", this);
                config = ScriptableObject.CreateInstance<LevelConfig>();
            }
            // без библиотеки эффекты всё равно будут — простыми кружками, чтобы попадания было видно
            _fx = new FxPlayer(fx != null ? fx : ScriptableObject.CreateInstance<FxLibrary>());
            _fx.Subscribe();
            OnEnemyKilled += _fx.OnEnemyKilled;
            _sfx = new SfxPlayer(this);
            _sfx.Subscribe();
            // стоп-кадр и тряска; добавь HitFeel на этот объект вручную, чтобы настраивать числа
            var feel = GetComponent<HitFeel>();
            if (feel == null) feel = gameObject.AddComponent<HitFeel>();
            feel.Bind(this);
        }

        void OnDestroy()
        {
            GameTime.ClearAll();
            _fx?.Unsubscribe(); // события снаряда статические — обязательно отписываемся
            _sfx?.Unsubscribe();
        }

        void Start()
        {
            if (autoStart) StartNewGame();
            else SetState(GameState.Menu);
        }

        void Update()
        {
            if (GameInput.PausePressed() && !PauseBlocked)
            {
                if (State == GameState.Playing) Pause(true);
                else if (State == GameState.Paused) Pause(false);
            }

            if (debugKeys && State == GameState.Playing)
            {
                if (GameInput.RegeneratePressed()) LoadLevel(0);
                if (GameInput.NextLevelPressed()) CompleteLevel();
                if (GameInput.DebugUltimatePressed() && Current != null && Current.Player != null)
                {
                    var ult = Current.Player.GetComponent<PlayerUltimate>();
                    if (ult != null) ult.DebugAddCharge();
                }
            }
        }

        // ------------------------------------------------------------------ Управление игрой

        public void StartNewGame()
        {
            LevelNumber = 1;
            TotalStars = 0;
            Difficulty = config.startDifficulty;
            LoadLevel(Settings.seed); // фиксированный сид из конфига (0 = случайный)
        }

        public void NextLevel()
        {
            LevelNumber++;
            Difficulty = Mathf.Min(Difficulty + 1, config.maxDifficulty);
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
            GameTime.Paused = paused;
            SetState(paused ? GameState.Paused : GameState.Playing);
        }

        public void GoToMenu()
        {
            Teardown();
            GameTime.ClearAll();
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
            Current.Stars?.Complete();
            TotalStars += Current.Stars != null ? Current.Stars.EarnedCount : 0;
            SetState(GameState.LevelComplete);
            GameTime.Set(this, 0f); // замираем, пока висит надпись
            OnLevelCompleted?.Invoke(Current);
            StartCoroutine(AfterRealtime(levelCompleteDelay + (Current.Stars != null ? starsShowTime : 0f), NextLevel));
        }

        // ------------------------------------------------------------------ Загрузка уровня

        /// <summary>seed = 0 — случайный уровень.</summary>
        void LoadLevel(int seed)
        {
            Teardown();
            GameTime.ClearAll();
            Health.FriendlyFire = friendlyFire;
            Bullet.BulletsCollide = bulletsCollide;

            var data = BspLevelGenerator.Generate(Settings, seed, Difficulty, _spawner.SpawnRules());
            var root = _builder.Build(data);
            var ctx = new LevelContext
            {
                Data = data,
                Pathfinder = new GridPathfinder(data),
                Builder = _builder,
                Root = root,
                LevelNumber = LevelNumber,
                Difficulty = Difficulty,
                Exit = _builder.Exit.transform,
            };
            Current = ctx;
            _fx.BindLevel(ctx); // до спавна — чтобы повесить полоски HP на врагов по мере появления
            _spawner.SpawnAll(ctx);
            int pathCells = data.mainPath != null ? data.mainPath.Count : 0;
            ctx.Stars = new LevelStars(ctx, config.stars, pathCells * config.parSecondsPerCell + config.parBonusSeconds);

            // ctx живёт ровно один уровень, поэтому от его событий отписываться не нужно
            ctx.EnemyKilled += e => OnEnemyKilled?.Invoke(e);
            ctx.EnemyCountChanged += n => OnEnemyCountChanged?.Invoke(n);

            _builder.Exit.PlayerEntered += CompleteLevel;
            if (ctx.PlayerHealth != null)
            {
                ctx.PlayerHealth.OnHpChanged += HandlePlayerHpChanged;
                ctx.PlayerHealth.OnDeath += HandlePlayerDeath;
            }

            SetupCamera(ctx);
            Debug.Log($"[GameFlow] Уровень {LevelNumber}: сид {data.seed}, сложность {Difficulty}, " +
                      $"врагов {data.enemies.Count}");

            SetState(GameState.Playing);
            OnLevelStarted?.Invoke(ctx);
            if (ctx.PlayerHealth != null) OnPlayerHpChanged?.Invoke(ctx.PlayerHealth.Hp, ctx.PlayerHealth.maxHp);
            OnEnemyCountChanged?.Invoke(ctx.AliveEnemyCount());
        }

        void Teardown()
        {
            StopAllCoroutines();
            if (Current != null)
            {
                if (_builder.Exit != null) _builder.Exit.PlayerEntered -= CompleteLevel;
                if (Current.PlayerHealth != null)
                {
                    Current.PlayerHealth.OnHpChanged -= HandlePlayerHpChanged;
                    Current.PlayerHealth.OnDeath -= HandlePlayerDeath;
                }
            }
            _builder.Clear();
            Current = null;
        }

        void HandlePlayerHpChanged(int hp, int max) => OnPlayerHpChanged?.Invoke(hp, max);

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

#if UNITY_EDITOR
        // Пустые ссылки на ассеты-настройки подставляем сами — первый подходящий ассет в проекте.
        void OnValidate()
        {
            bool changed = EditorAssetLookup.FillIfEmpty(ref config);
            changed |= EditorAssetLookup.FillIfEmpty(ref fx);
            if (changed) UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
