// Игровой цикл: титульный экран → уровень → победа/поражение → прокачка → следующий уровень.
// Единственный, кто решает, когда грузить уровень и когда игра закончилась.
// LevelBuilder строит геометрию, PlayerFactory/EnemyFactory создают персонажей — а GameManager их связывает.
using System;
using System.Collections;
using UnityEngine;

namespace CatTanks
{
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        [Tooltip("Пусто = Assets/Resources/GameConfig.asset или настройки из кода")]
        public GameConfig config;
        public float fadeDuration = 0.25f;

        public GameConfig Config { get; private set; }
        public GameState State { get; private set; } = GameState.Loading;
        public RunState Run { get; private set; }
        public SaveData Save { get; private set; }
        public AiParams CurrentAi { get; private set; }
        public LoseReason LastLoseReason { get; private set; }

        public float LevelTime { get; private set; }
        public float TimeLimit { get; private set; }             // 0 = без лимита
        public float TimeLeft => TimeLimit > 0 ? Mathf.Max(0, TimeLimit - LevelTime) : float.PositiveInfinity;
        public int SecretsOnLevel { get; private set; }
        public int LevelEnemyCount { get; private set; }
        /// <summary>Время на «быструю» звезду: растёт с числом врагов на уровне.</summary>
        public float ParTime => 25f + 3.5f * LevelEnemyCount;

        /// <summary>Затемнение экрана при переходах (0..1). UI просто рисует чёрный слой с этой прозрачностью.</summary>
        public float Fade { get; private set; }
        public bool InTransition { get; private set; }
        public int StateChangedFrame { get; private set; }

        void Awake()
        {
            if (Game.Manager != null && Game.Manager != this) { Destroy(gameObject); return; }
            Game.Manager = this;
            Config = config != null ? config : GameConfig.Default;
            Save = SaveSystem.Load();
            Sfx.MasterVolume = Save.sfxVolume;
            CameraFollow.ShakeEnabled = Save.screenShake;
        }

        void OnDestroy()
        {
            if (Game.Manager == this) Game.Manager = null;
            Time.timeScale = 1f;
        }

        void OnEnable()
        {
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.EnemyKilled += OnEnemyKilled;
        }

        void OnDisable()
        {
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.EnemyKilled -= OnEnemyKilled;
        }

        void Start()
        {
            Run = NewRun();
            LoadLevel(newSeed: true, toMenu: Config.showTitleScreen);
        }

        // при сворачивании окна — пауза (в редакторе не мешаем переключаться между окнами)
        void OnApplicationFocus(bool focus)
        {
#if !UNITY_EDITOR
            if (!focus && State == GameState.Playing) SetPaused(true);
#endif
        }

        // ------------------------------------------------------------------ Забег и уровни

        /// <summary>Новый забег с первого уровня, прокачка сбрасывается.</summary>
        public void StartRun()
        {
            // с титульного экрана уровень 1 уже собран и висит на фоне — просто «оживляем» его
            if (State == GameState.Menu && Run != null && Run.levelNumber == 1)
            {
                EnterPlaying();
                return;
            }
            Transition(() => { Run = NewRun(); LoadLevel(newSeed: true); });
        }

        RunState NewRun() => new RunState { bestScoreAtStart = Save != null ? Save.bestScore : 0 };

        public void NextLevel()
        {
            if (State != GameState.LevelWon) return;
            Transition(() => { Run.levelNumber++; LoadLevel(newSeed: true); });
        }

        /// <summary>Тот же уровень с тем же сидом.</summary>
        public void RestartLevel() => Transition(() => LoadLevel(newSeed: false));

        public void GoToMenu() => Transition(() => { StoreRecords(); Run = NewRun(); LoadLevel(newSeed: true, toMenu: true); });

        void Transition(Action load)
        {
            if (InTransition) return;
            StartCoroutine(TransitionRoutine(load));
        }

        IEnumerator TransitionRoutine(Action load)
        {
            InTransition = true;
            for (float t = Fade; t < 1f; t += Time.unscaledDeltaTime / fadeDuration) { Fade = t; yield return null; }
            Fade = 1f;
            load();
            yield return null;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / fadeDuration) { Fade = t; yield return null; }
            Fade = 0f;
            InTransition = false;
        }

        void LoadLevel(bool newSeed, bool toMenu = false)
        {
            SetState(GameState.Loading);
            GameTime.ClearAll();

            var level = LevelBuilder.EnsureExists();
            var settings = Config.Difficulty.BuildSettings(Run.levelNumber, Config.Enemies);
            settings.seed = newSeed ? 0 : Run.levelSeed;
            level.Build(settings);
            Run.levelSeed = level.Data.seed;
            Run.levelKills = 0;
            SecretsOnLevel = level.Data.secrets.Count;

            LevelTime = 0f;
            TimeLimit = Config.Difficulty.GetTimeLimit(Run.levelNumber);
            CurrentAi = Config.Difficulty.GetAi(Run.levelNumber);

            var hull = SelectedHull;
            var cannon = SelectedCannon;
            var stats = PlayerStats.Build(Config.basePlayerStats, hull, cannon, Run, Config.Upgrades);
            var player = PlayerFactory.Create(Config, level.Root, level.PlayerSpawn, stats, hull, cannon,
                                              Config.FindSkin(Save.selectedSkin), Save.tankControls);

            var ctx = new EnemyContext(player, level.Data, level.Pathfinder, Run.levelNumber, CurrentAi);
            float eliteChance = Config.Difficulty.GetEliteChance(Run.levelNumber);
            foreach (var spawn in level.Data.enemies)
            {
                var def = Config.Enemies.Get(spawn.type);
                bool elite = def != null && !def.isGuard && UnityEngine.Random.value < eliteChance;
                EnemyFactory.Spawn(Config.Enemies, spawn, ctx, level.Root, elite);
            }

            LevelEnemyCount = EnemyRegistry.Count;
            CameraFollow.SnapMain();
            Debug.Log($"[Game] Уровень {Run.levelNumber}: сид {Run.levelSeed}, врагов {EnemyRegistry.Count}, " +
                      $"секретов {SecretsOnLevel}, лимит времени {(TimeLimit > 0 ? TimeLimit + " c" : "нет")}");

            if (toMenu) SetState(GameState.Menu);
            else EnterPlaying();
        }

        void EnterPlaying()
        {
            SetState(GameState.Playing);
            GameTime.Apply();
            GameEvents.RaiseLevelStarted(Run.levelNumber);
        }

        // ------------------------------------------------------------------ Победа / поражение

        /// <summary>Кот добрался до рыбки. Вызывает ExitZone.</summary>
        public void WinLevel()
        {
            if (State != GameState.Playing) return;

            // звёзды: ★ пройти, ★ зачистить арену, ★ уложиться в контрольное время
            Run.lastCleared = EnemyRegistry.Count == 0;
            Run.lastFast = LevelTime <= ParTime;
            Run.lastStars = 1 + (Run.lastCleared ? 1 : 0) + (Run.lastFast ? 1 : 0);
            Run.lastStarFish = Run.lastStars - 1;
            Run.lastClearBonus = 500 + 100 * Run.levelNumber;
            Run.lastTimeBonus = Mathf.Max(0, Mathf.RoundToInt((ParTime - LevelTime) * 20f));
            AddScore(Run.lastClearBonus + Run.lastTimeBonus + 250 * (Run.lastStars - 1));
            Run.fish += Config.fishPerLevel + Run.lastStarFish;
            Run.levelsCompleted++;
            if (Run.levelNumber + 1 > Save.bestLevel) Save.bestLevel = Run.levelNumber + 1;
            if (Run.levelNumber == 1) Save.tutorialDone = true;
            SaveSystem.Store(Save);
            SetState(GameState.LevelWon);
            GameEvents.RaiseLevelWon(Run.levelNumber);
        }

        public void LoseLevel(LoseReason reason)
        {
            if (State != GameState.Playing) return;
            LastLoseReason = reason;
            StoreRecords();
            SetState(GameState.LevelLost);
            GameEvents.RaiseLevelLost(reason);
        }

        void OnPlayerDied() => LoseLevel(LoseReason.Killed);

        void OnEnemyKilled(Enemy e, DamageInfo info)
        {
            if (!info.ByPlayer) return;
            Run.totalKills++;
            Run.levelKills++;
            Save.totalKills++;
        }

        // ------------------------------------------------------------------ Очки, рыбки, рекорды

        public void AddScore(int amount)
        {
            if (amount <= 0 || Run == null) return;
            Run.score += amount;
            if (!Run.newRecordAnnounced && Run.bestScoreAtStart > 0 && Run.score > Run.bestScoreAtStart)
            {
                Run.newRecordAnnounced = true;
                GameEvents.RaiseNewRecord();
            }
        }

        public void AddFish(int amount)
        {
            if (amount > 0 && Run != null) Run.fish += amount;
        }

        public bool IsNewRecord => Run != null && Run.score > Run.bestScoreAtStart && Run.score > 0;

        void StoreRecords()
        {
            if (Run == null || Save == null) return;
            if (Run.score > Save.bestScore) Save.bestScore = Run.score;
            if (Run.bestCombo > Save.bestCombo) Save.bestCombo = Run.bestCombo;
            SaveSystem.Store(Save);
        }

        public void CollectSecret(SecretFish fish)
        {
            Run.fish += fish.value;
            Run.secretsFound++;
            GameEvents.RaiseSecretCollected(fish);
        }

        // ------------------------------------------------------------------ Прокачка, скины, настройки

        public bool CanBuy(UpgradeDefinition u)
        {
            int lvl = Run.GetUpgradeLevel(u.id);
            return lvl < u.maxLevel && Run.fish >= u.CostForLevel(lvl);
        }

        public bool TryBuyUpgrade(UpgradeDefinition u)
        {
            if (!CanBuy(u)) return false;
            int lvl = Run.GetUpgradeLevel(u.id);
            Run.fish -= u.CostForLevel(lvl);
            Run.upgradeLevels[u.id] = lvl + 1;
            Sfx.Play(SfxId.Purchase, 0.8f, 0f);
            return true; // применится при загрузке следующего уровня (PlayerStats.Build)
        }

        public bool IsSkinUnlocked(SkinDefinition s) => s != null && Save.bestLevel >= s.unlockAtLevel;

        // ------------------------------------------------------------------ Гараж

        public TankModuleDefinition SelectedHull => Config.FindModule(ModuleSlot.Hull, Save.selectedHull);
        public TankModuleDefinition SelectedCannon => Config.FindModule(ModuleSlot.Cannon, Save.selectedCannon);
        public bool IsModuleUnlocked(TankModuleDefinition m) => m != null && Save.bestLevel >= m.unlockAtLevel;

        /// <summary>Выбрать модуль. В меню применяется сразу (уровень на фоне пересобирается), в игре — со следующего уровня.</summary>
        public void SelectModule(TankModuleDefinition m)
        {
            if (!IsModuleUnlocked(m)) return;
            if (m.slot == ModuleSlot.Hull) Save.selectedHull = m.id; else Save.selectedCannon = m.id;
            SaveSystem.Store(Save);
            if (State == GameState.Menu) LoadLevel(newSeed: false, toMenu: true);
        }

        public void SelectSkin(SkinDefinition s)
        {
            if (!IsSkinUnlocked(s)) return;
            Save.selectedSkin = s.id;
            SaveSystem.Store(Save);
            if (Game.Player != null) PlayerFactory.ApplySkin(Game.Player.gameObject, s);
        }

        public void SetSfxVolume(float v)
        {
            Save.sfxVolume = Mathf.Clamp01(v);
            Sfx.MasterVolume = Save.sfxVolume;
            SaveSystem.Store(Save);
        }

        public void SetTankControls(bool on)
        {
            Save.tankControls = on;
            SaveSystem.Store(Save);
            if (Game.Player != null && Game.Player.Movement != null) Game.Player.Movement.tankControls = on;
        }

        public void SetScreenShake(bool on)
        {
            Save.screenShake = on;
            CameraFollow.ShakeEnabled = on;
            SaveSystem.Store(Save);
        }

        // ------------------------------------------------------------------ Отладка

        /// <summary>
        /// Витрина врагов: каждый тип из каталога (обычный + элитный) на свободных клетках вокруг кота.
        /// Меню редактора CatTanks → Debug → Spawn All Enemy Types (только в Play Mode).
        /// </summary>
        public int DebugSpawnShowcase()
        {
            var level = Game.Level;
            if (level == null || level.Data == null || Game.Player == null) return 0;
            var ctx = new EnemyContext(Game.Player, level.Data, level.Pathfinder, Run.levelNumber, CurrentAi);
            var center = LevelBuilder.WorldToCell(Game.Player.transform.position);
            var used = new System.Collections.Generic.HashSet<CatTanks.Level.GridPos>();
            // свободные клетки по возрастанию расстояния от кота (не ближе 3)
            var cells = new System.Collections.Generic.List<CatTanks.Level.GridPos>();
            for (int x = 1; x < level.Data.width - 1; x++)
                for (int y = 1; y < level.Data.height - 1; y++)
                {
                    var c = new CatTanks.Level.GridPos(x, y);
                    int dx = x - center.x, dy = y - center.y;
                    if (level.Data.IsWalkable(c) && dx * dx + dy * dy >= 9) cells.Add(c);
                }
            cells.Sort((p, q) => ((p.x - center.x) * (p.x - center.x) + (p.y - center.y) * (p.y - center.y))
                                 .CompareTo((q.x - center.x) * (q.x - center.x) + (q.y - center.y) * (q.y - center.y)));
            int spawned = 0;
            foreach (var def in Config.Enemies.definitions)
                for (int e = 0; e < 2; e++)
                {
                    int fp = Mathf.Max(1, def.footprint);
                    foreach (var c in cells)
                    {
                        bool ok = true;
                        for (int dx = -1; dx <= fp && ok; dx++)
                            for (int dy = -1; dy <= fp && ok; dy++)
                            {
                                var q = new CatTanks.Level.GridPos(c.x + dx, c.y + dy);
                                if (!level.Data.IsWalkable(q) || used.Contains(q)) ok = false;
                            }
                        if (!ok) continue;
                        for (int dx = -1; dx <= fp; dx++)
                            for (int dy = -1; dy <= fp; dy++) used.Add(new CatTanks.Level.GridPos(c.x + dx, c.y + dy));
                        EnemyFactory.Spawn(Config.Enemies, new CatTanks.Level.EnemySpawn(c, def.type, fp), ctx, level.Root, elite: e == 1);
                        spawned++;
                        break;
                    }
                }
            Debug.Log($"[Game] Витрина врагов: заспавнено {spawned}");
            return spawned;
        }

        // ------------------------------------------------------------------ Состояния и ввод

        public void SetPaused(bool paused)
        {
            if (paused && State == GameState.Playing) SetState(GameState.Paused);
            else if (!paused && State == GameState.Paused) SetState(GameState.Playing);
            GameTime.Apply();
        }

        void SetState(GameState s)
        {
            if (State == s) return;
            State = s;
            StateChangedFrame = Time.frameCount;
            GameEvents.RaiseStateChanged(s);
        }

        void Update()
        {
            if (State == GameState.Playing)
            {
                LevelTime += Time.deltaTime;
                if (TimeLimit > 0 && LevelTime >= TimeLimit) LoseLevel(LoseReason.TimeOut);
            }

            if (InTransition || Game.ModalOpen) return;

            if (GameInput.PausePressed())
            {
                if (State == GameState.Playing) SetPaused(true);
                else if (State == GameState.Paused) SetPaused(false);
            }

            switch (State)
            {
                case GameState.Menu:
                    if (GameInput.ConfirmPressed() || GameInput.FireKeyPressed()) StartRun();
                    break;
                case GameState.LevelWon:
                    if (GameInput.ConfirmPressed()) NextLevel();
                    break;
                case GameState.LevelLost:
                    if (GameInput.ConfirmPressed() || GameInput.RestartPressed()) RestartLevel();
                    break;
            }

            if (Config.debugKeys && State == GameState.Playing)
            {
                if (GameInput.DebugNextLevelPressed()) { WinLevel(); NextLevel(); }
                else if (GameInput.DebugRegeneratePressed()) Transition(() => LoadLevel(newSeed: true));
                else if (GameInput.DebugUltPressed() && Game.Player != null && Game.Player.Ultimate != null) Game.Player.Ultimate.DebugAddCharge();
            }
        }
    }
}
