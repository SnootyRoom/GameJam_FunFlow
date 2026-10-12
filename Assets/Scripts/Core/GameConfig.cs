// Корневой конфиг игры. Создать ассет: ПКМ в Project → Create → CatTanks → Game Config,
// положить в Assets/Resources/GameConfig.asset (подхватится сам) или назначить в GameManager.
// Любое пустое поле заменяется настройками по умолчанию из кода — игра запускается без единого ассета.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    [CreateAssetMenu(menuName = "CatTanks/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Игрок")]
        public GameObject playerPrefab;                       // пусто = танк из примитивов
        public PlayerStats basePlayerStats = PlayerStats.Default;
        public UltimateSettings ultimate = new UltimateSettings();
        public float trajectoryPreviewLength = 30f;

        [Header("Бой")]
        public bool selfDamage = true;                        // свой снаряд после рикошета ранит стрелка
        public bool friendlyFire = true;                      // снаряды врагов ранят других врагов

        [Header("Уровни")]
        public bool requireAllEnemiesDead = false;            // рыбка открывается только после зачистки
        public int fishPerLevel = 3;                          // валюта за прохождение (тратится на прокачку)
        public int fishPerSecret = 2;

        [Header("Модули (пусто = по умолчанию)")]
        [SerializeField] DifficultyProgression difficulty = null;
        [SerializeField] EnemyCatalog enemies = null;
        [SerializeField] List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();
        [SerializeField] List<SkinDefinition> skins = new List<SkinDefinition>();
        [SerializeField] List<TankModuleDefinition> hulls = new List<TankModuleDefinition>();
        [SerializeField] List<TankModuleDefinition> cannons = new List<TankModuleDefinition>();

        [Header("Интерфейс")]
        public bool showTitleScreen = true;                   // false — сразу в игру (удобно при отладке)
        public bool showTutorial = true;                      // подсказки на первом уровне (один раз)

        [Header("Отладка")]
        public bool debugKeys = true;                         // N — следующий уровень, F5 — новый сид

        DifficultyProgression _defDifficulty;
        EnemyCatalog _defEnemies;
        List<UpgradeDefinition> _defUpgrades;
        List<SkinDefinition> _defSkins;
        List<TankModuleDefinition> _defHulls, _defCannons;

        public IReadOnlyList<TankModuleDefinition> Hulls
        {
            get
            {
                if (hulls != null && hulls.Count > 0) return hulls;
                return _defHulls ?? (_defHulls = TankModuleDefinition.CreateDefaultHulls());
            }
        }

        public IReadOnlyList<TankModuleDefinition> Cannons
        {
            get
            {
                if (cannons != null && cannons.Count > 0) return cannons;
                return _defCannons ?? (_defCannons = TankModuleDefinition.CreateDefaultCannons());
            }
        }

        public TankModuleDefinition FindModule(ModuleSlot slot, string id)
        {
            var list = slot == ModuleSlot.Hull ? Hulls : Cannons;
            foreach (var m in list) if (m != null && m.id == id) return m;
            return list.Count > 0 ? list[0] : null;
        }

        public DifficultyProgression Difficulty
        {
            get
            {
                if (difficulty != null) return difficulty;
                if (_defDifficulty == null) _defDifficulty = DifficultyProgression.CreateDefault();
                return _defDifficulty;
            }
        }

        public EnemyCatalog Enemies
        {
            get
            {
                if (enemies != null) return enemies;
                if (_defEnemies == null) _defEnemies = EnemyCatalog.CreateDefault();
                return _defEnemies;
            }
        }

        public IReadOnlyList<UpgradeDefinition> Upgrades
        {
            get
            {
                if (upgrades != null && upgrades.Count > 0) return upgrades;
                return _defUpgrades ?? (_defUpgrades = UpgradeDefinition.CreateDefaults());
            }
        }

        public IReadOnlyList<SkinDefinition> Skins
        {
            get
            {
                if (skins != null && skins.Count > 0) return skins;
                return _defSkins ?? (_defSkins = SkinDefinition.CreateDefaults());
            }
        }

        public SkinDefinition FindSkin(string id)
        {
            var list = Skins;
            foreach (var s in list) if (s != null && s.id == id) return s;
            return list.Count > 0 ? list[0] : null;
        }

        static GameConfig _default;

        /// <summary>Assets/Resources/GameConfig.asset, а если его нет — конфиг из кода.</summary>
        public static GameConfig Default
        {
            get
            {
                if (_default != null) return _default;
                _default = Resources.Load<GameConfig>("GameConfig");
                if (_default == null)
                {
                    _default = CreateInstance<GameConfig>();
                    _default.name = "GameConfig (default)";
                }
                return _default;
            }
        }
    }
}
