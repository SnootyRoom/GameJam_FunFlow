// Глобальная шина событий. Нужна, чтобы системы не знали друг о друге напрямую:
// враг умер → ульта копит заряд, HUD считает убийства, звук играет — и враг про них ничего не знает.
// Подписывайся в OnEnable, отписывайся в OnDisable.
using System;
using UnityEngine;

namespace CatTanks
{
    public static class GameEvents
    {
        public static event Action<GameState> StateChanged;
        public static event Action<int> LevelStarted;            // номер уровня
        public static event Action<int> LevelWon;                // номер уровня
        public static event Action<LoseReason> LevelLost;

        public static event Action<Health, DamageInfo> PlayerDamaged;
        public static event Action PlayerDied;

        public static event Action<Enemy> EnemySpawned;
        public static event Action<Enemy, DamageInfo> EnemyKilled; // info.ByPlayer — убил ли игрок
        public static event Action<Enemy, DamageInfo> EnemyDamaged; // ранен, но жив
        public static event Action<Enemy> EnemyAlerted;           // заметил игрока (для «!» и звука)
        public static event Action<Enemy> BossAwakened;
        public static event Action<Enemy, int> BossPhaseChanged;
        public static event Action<Enemy, string> BossAttack;
        public static event Action<int, float> ComboChanged;       // комбо, множитель очков
        public static event Action<int, int> ComboEnded;           // итоговое комбо, бонус рыбок
        public static event Action<Pickup> PickupCollected;
        public static event Action NewRecord;

        public static event Action<Bullet> BulletFired;          // ИИ может «услышать» выстрел
        public static event Action<Bullet, Vector2> BulletBounced;
        public static event Action PlayerFireBlocked;            // нажал огонь, а снарядов нет / перезарядка
        public static event Action PlayerReloadStarted;
        public static event Action PlayerReloadFinished;
        public static event Action<Bullet, Vector2, bool> BulletExploded; // где разбился; true — попал в объект со здоровьем
        public static event Action UltimateReady;                // появился новый заряд ульты
        public static event Action ExitLocked;                   // кот у рыбки, но арена не зачищена
        public static event Action<Vector2, float, float> UltimateLaunched; // точка, радиус, секунд до удара
        public static event Action<Vector2, float, int> UltimateImpact;     // точка, радиус, сколько убито
        public static event Action<SecretFish> SecretCollected;

        public static void RaiseStateChanged(GameState s) => StateChanged?.Invoke(s);
        public static void RaiseLevelStarted(int level) => LevelStarted?.Invoke(level);
        public static void RaiseLevelWon(int level) => LevelWon?.Invoke(level);
        public static void RaiseLevelLost(LoseReason r) => LevelLost?.Invoke(r);
        public static void RaisePlayerDamaged(Health h, DamageInfo info) => PlayerDamaged?.Invoke(h, info);
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaiseEnemySpawned(Enemy e) => EnemySpawned?.Invoke(e);
        public static void RaiseEnemyKilled(Enemy e, DamageInfo info) => EnemyKilled?.Invoke(e, info);
        public static void RaiseEnemyDamaged(Enemy e, DamageInfo info) => EnemyDamaged?.Invoke(e, info);
        public static void RaiseEnemyAlerted(Enemy e) => EnemyAlerted?.Invoke(e);
        public static void RaiseBossAwakened(Enemy e) => BossAwakened?.Invoke(e);
        public static void RaiseBossPhase(Enemy e, int phase) => BossPhaseChanged?.Invoke(e, phase);
        public static void RaiseBossAttack(Enemy e, string attack) => BossAttack?.Invoke(e, attack);
        public static void RaiseComboChanged(int combo, float mult) => ComboChanged?.Invoke(combo, mult);
        public static void RaiseComboEnded(int combo, int bonus) => ComboEnded?.Invoke(combo, bonus);
        public static void RaisePickupCollected(Pickup p) => PickupCollected?.Invoke(p);
        public static void RaiseNewRecord() => NewRecord?.Invoke();
        public static void RaiseBulletFired(Bullet b) => BulletFired?.Invoke(b);
        public static void RaiseBulletBounced(Bullet b, Vector2 at) => BulletBounced?.Invoke(b, at);
        public static void RaisePlayerFireBlocked() => PlayerFireBlocked?.Invoke();
        public static void RaiseUltimateReady() => UltimateReady?.Invoke();
        public static void RaiseExitLocked() => ExitLocked?.Invoke();
        public static void RaisePlayerReloadStarted() => PlayerReloadStarted?.Invoke();
        public static void RaisePlayerReloadFinished() => PlayerReloadFinished?.Invoke();
        public static void RaiseBulletExploded(Bullet b, Vector2 at, bool hitTarget) => BulletExploded?.Invoke(b, at, hitTarget);
        public static void RaiseUltimateLaunched(Vector2 at, float radius, float delay) => UltimateLaunched?.Invoke(at, radius, delay);
        public static void RaiseUltimateImpact(Vector2 at, float radius, int kills) => UltimateImpact?.Invoke(at, radius, kills);
        public static void RaiseSecretCollected(SecretFish f) => SecretCollected?.Invoke(f);

        // при выключенном Domain Reload статические события переживают перезапуск Play Mode
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            StateChanged = null; LevelStarted = null; LevelWon = null; LevelLost = null;
            PlayerDamaged = null; PlayerDied = null; EnemySpawned = null; EnemyKilled = null;
            BulletFired = null; UltimateLaunched = null; UltimateImpact = null; SecretCollected = null;
            PlayerReloadStarted = null; PlayerReloadFinished = null; BulletExploded = null;
            EnemyDamaged = null; EnemyAlerted = null; BulletBounced = null; PlayerFireBlocked = null;
            UltimateReady = null; ExitLocked = null;
            BossAwakened = null; BossPhaseChanged = null; BossAttack = null;
            ComboChanged = null; ComboEnded = null; PickupCollected = null; NewRecord = null;
        }
    }
}
