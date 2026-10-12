// Комбо: убийства с интервалом меньше window секунд складываются в серию.
// Каждое убийство даёт очки × множитель (растёт с серией до maxMultiplier), элита — вдвое больше.
// Серия обрывается по таймеру или когда кота ранят; за серию от 3 — бонусные рыбки.
// Это главный «залипательный» крючок: хочется держать серию и рисковать.
using UnityEngine;

namespace CatTanks
{
    public class ComboSystem : MonoBehaviour
    {
        public float window = 3f;
        public float multiplierPerKill = 0.25f;
        public float maxMultiplier = 4f;
        public int killsPerBonusFish = 3;

        public static ComboSystem Instance { get; private set; }
        public int Combo { get; private set; }
        public float Multiplier => Combo <= 0 ? 1f : Mathf.Min(maxMultiplier, 1f + multiplierPerKill * (Combo - 1));
        public float TimeLeft01 => Combo <= 0 ? 0f : Mathf.Clamp01(_left / window);

        float _left;

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.PlayerDamaged += OnPlayerDamaged;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.LevelWon += OnLevelEnded;
            GameEvents.LevelLost += OnLevelLost;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.PlayerDamaged -= OnPlayerDamaged;
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.LevelWon -= OnLevelEnded;
            GameEvents.LevelLost -= OnLevelLost;
        }

        void OnEnemyKilled(Enemy e, DamageInfo info)
        {
            if (!info.ByPlayer || Game.Manager == null) return;
            Combo++;
            _left = window;
            int baseScore = e != null && e.Definition != null ? e.Definition.score : 100;
            int score = Mathf.RoundToInt(baseScore * Multiplier * (e != null && e.IsElite ? 2f : 1f));
            Game.Manager.AddScore(score);
            if (e != null) Fx.Text((Vector2)e.transform.position + Vector2.up * 0.4f, $"+{score}", new Color(1f, 0.9f, 0.5f));
            if (Combo > Game.Manager.Run.bestCombo) Game.Manager.Run.bestCombo = Combo;
            GameEvents.RaiseComboChanged(Combo, Multiplier);
        }

        void OnPlayerDamaged(Health h, DamageInfo info) => End();   // ранили — серия сгорает (без бонуса меньше половины)
        void OnLevelStarted(int level) { Combo = 0; _left = 0f; }
        void OnLevelEnded(int level) => End();
        void OnLevelLost(LoseReason r) => End();

        void Update()
        {
            if (Combo <= 0 || !Game.IsPlaying) return;
            _left -= Time.deltaTime;
            if (_left <= 0f) End();
        }

        void End()
        {
            if (Combo <= 0) return;
            int combo = Combo;
            int bonus = combo >= killsPerBonusFish ? combo / killsPerBonusFish : 0;
            if (bonus > 0 && Game.Manager != null) Game.Manager.AddFish(bonus);
            Combo = 0;
            _left = 0f;
            GameEvents.RaiseComboEnded(combo, bonus);
        }
    }
}
