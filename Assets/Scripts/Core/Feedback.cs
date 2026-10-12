// Режиссёр «сочности»: слушает GameEvents и отвечает звуком, тряской, хит-стопом, частицами и текстом.
// Геймплейный код ничего не знает про эффекты — он только поднимает события. Весь тюнинг ощущений — в этом файле.
using System.Collections;
using UnityEngine;

namespace CatTanks
{
    public class Feedback : MonoBehaviour
    {
        [Header("Хит-стоп (заморозка кадра), секунды реального времени")]
        public float killStop = 0.045f;
        public float hurtStop = 0.09f;
        public float nutStop = 0.1f;

        [Header("Тряска")]
        public float shotShake = 0.08f;
        public float killShake = 0.32f;
        public float hurtShake = 0.6f;
        public float nutShake = 1f;

        float _slowUntil, _slowScale = 1f;   // хит-стоп / замедления — через GameTime
        float _lastLockToast;

        void OnEnable()
        {
            GameEvents.BulletFired += OnBulletFired;
            GameEvents.BulletBounced += OnBulletBounced;
            GameEvents.BulletExploded += OnBulletExploded;
            GameEvents.EnemySpawned += OnEnemySpawned;
            GameEvents.EnemyDamaged += OnEnemyDamaged;
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.EnemyAlerted += OnEnemyAlerted;
            GameEvents.PlayerDamaged += OnPlayerDamaged;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.PlayerFireBlocked += OnFireBlocked;
            GameEvents.PlayerReloadStarted += OnReloadStarted;
            GameEvents.PlayerReloadFinished += OnReloadFinished;
            GameEvents.UltimateReady += OnUltReady;
            GameEvents.UltimateLaunched += OnUltLaunched;
            GameEvents.UltimateImpact += OnUltImpact;
            GameEvents.SecretCollected += OnSecret;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.LevelWon += OnLevelWon;
            GameEvents.LevelLost += OnLevelLost;
            GameEvents.ExitLocked += OnExitLocked;
            GameEvents.BossAwakened += OnBossAwakened;
            GameEvents.BossPhaseChanged += OnBossPhase;
            GameEvents.ComboChanged += OnComboChanged;
            GameEvents.ComboEnded += OnComboEnded;
            GameEvents.PickupCollected += OnPickup;
            GameEvents.NewRecord += OnNewRecord;
        }

        void OnDisable()
        {
            GameEvents.BulletFired -= OnBulletFired;
            GameEvents.BulletBounced -= OnBulletBounced;
            GameEvents.BulletExploded -= OnBulletExploded;
            GameEvents.EnemySpawned -= OnEnemySpawned;
            GameEvents.EnemyDamaged -= OnEnemyDamaged;
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.EnemyAlerted -= OnEnemyAlerted;
            GameEvents.PlayerDamaged -= OnPlayerDamaged;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.PlayerFireBlocked -= OnFireBlocked;
            GameEvents.PlayerReloadStarted -= OnReloadStarted;
            GameEvents.PlayerReloadFinished -= OnReloadFinished;
            GameEvents.UltimateReady -= OnUltReady;
            GameEvents.UltimateLaunched -= OnUltLaunched;
            GameEvents.UltimateImpact -= OnUltImpact;
            GameEvents.SecretCollected -= OnSecret;
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.LevelWon -= OnLevelWon;
            GameEvents.LevelLost -= OnLevelLost;
            GameEvents.ExitLocked -= OnExitLocked;
            GameEvents.BossAwakened -= OnBossAwakened;
            GameEvents.BossPhaseChanged -= OnBossPhase;
            GameEvents.ComboChanged -= OnComboChanged;
            GameEvents.ComboEnded -= OnComboEnded;
            GameEvents.PickupCollected -= OnPickup;
            GameEvents.NewRecord -= OnNewRecord;
        }

        // ------------------------------------------------------------------ Время

        /// <summary>Замедлить время (scale 0 = стоп-кадр) на duration секунд реального времени.</summary>
        public void SlowTime(float scale, float duration)
        {
            if (Game.State != GameState.Playing && Game.State != GameState.LevelLost) return;
            _slowScale = Mathf.Min(scale, _slowUntil > Time.unscaledTime ? _slowScale : 1f);
            _slowUntil = Mathf.Max(_slowUntil, Time.unscaledTime + duration);
            GameTime.Set(this, _slowScale);
        }

        void Update()
        {
            if (_slowUntil > 0f && Time.unscaledTime >= _slowUntil)
            {
                _slowUntil = 0f;
                _slowScale = 1f;
                GameTime.Clear(this);
            }
        }

        static Vector2 PlayerPos => Game.Player != null ? (Vector2)Game.Player.transform.position : Vector2.zero;

        // ------------------------------------------------------------------ Реакции

        void OnBulletFired(Bullet b)
        {
            bool player = b.Team == Team.Player;
            if (player)
            {
                // у каждой пушки свой голос и своя отдача: тяжёлая — ниже и сильнее трясёт
                var cannon = Game.Player != null ? Game.Player.Cannon : null;
                float pitch = cannon != null ? cannon.shotPitch : 1f;
                float recoil = cannon != null ? cannon.recoil : 1f;
                Sfx.Play(SfxId.Shot, Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(recoil)), 0.04f, pitch);
                CameraFollow.Shake(shotShake * recoil);
                CameraFollow.Kick(-b.Direction, 0.12f * recoil);
                BulletAudio.Attach(b, 0.32f);
            }
            else
            {
                Sfx.PlayAt(SfxId.EnemyShot, b.Position, 0.75f, 0.06f, Mathf.Clamp(0.12f / Mathf.Max(0.05f, b.radius), 0.85f, 1.3f));
                BulletAudio.Attach(b, 0.36f);
            }
            Fx.MuzzleFlash(b.Position, b.Direction, player ? new Color(1f, 0.9f, 0.5f) : new Color(1f, 0.4f, 0.9f));
        }

        void OnBulletBounced(Bullet b, Vector2 at) => Sfx.PlayAt(SfxId.Bounce, at, 0.55f, 0.08f);

        void OnBulletExploded(Bullet b, Vector2 at, bool hitTarget)
        {
            if (!hitTarget) Sfx.PlayAt(SfxId.WallHit, at, 0.6f, 0.1f);
        }

        void OnEnemySpawned(Enemy e)
        {
            if (e.Health.maxHp > 1) WorldHealthBar.Attach(e.Health, e.Definition != null ? e.Definition.size : 0.8f);
        }

        void OnEnemyDamaged(Enemy e, DamageInfo info)
        {
            Sfx.PlayAt(SfxId.Hit, e.transform.position, 0.8f);
            Fx.Spark(e.transform.position, e.Definition != null ? e.Definition.color : Color.white, 7);
            if (info.amount > 0 && info.kind != DamageKind.Contact)
                Fx.Text((Vector2)e.transform.position + new Vector2(Random.Range(-0.3f, 0.3f), 0.5f), $"-{info.amount}", new Color(1f, 0.95f, 0.85f));
            CameraFollow.Shake(0.12f);
        }

        void OnEnemyKilled(Enemy e, DamageInfo info)
        {
            float size = e.Definition != null ? e.Definition.size : 0.8f;
            Fx.Explosion(e.transform.position, e.Definition != null ? e.Definition.color : Color.red, Mathf.Clamp(size, 0.8f, 1.8f));
            if (e.IsElite && info.ByPlayer)
                Fx.Text((Vector2)e.transform.position + Vector2.up * 0.9f, "ЭЛИТА!", new Color(1f, 0.82f, 0.25f));
            if (e.IsBoss)
            {
                Sfx.PlayAt(SfxId.NutImpact, e.transform.position, 1f, 0f, 0.8f);
                Sfx.PlayAt(SfxId.BossRoar, e.transform.position, 0.8f, 0f, 1.3f);
                Fx.Text((Vector2)e.transform.position + Vector2.up * 1.5f, "БОСС ПОВЕРЖЕН!", new Color(1f, 0.85f, 0.3f));
                CameraFollow.Shake(1f);
                CameraFollow.Punch(0.15f);
                SlowTime(0.2f, 1.2f);
                return;
            }
            if (info.kind == DamageKind.Ultimate) return; // орех сам гремит и трясёт
            Sfx.PlayAt(SfxId.Explosion, e.transform.position, 0.85f, 0.08f, Mathf.Lerp(1.15f, 0.8f, size / 2f));
            CameraFollow.Shake(killShake);
            SlowTime(0.05f, killStop);
        }

        void OnEnemyAlerted(Enemy e)
        {
            Fx.Text((Vector2)e.transform.position + Vector2.up * 0.8f, "!", new Color(1f, 0.35f, 0.3f));
            Sfx.PlayAt(SfxId.Alert, e.transform.position, 0.5f, 0.08f);
        }

        void OnPlayerDamaged(Health h, DamageInfo info)
        {
            if (h.IsDead) return;
            Sfx.Play(SfxId.PlayerHurt, 0.9f);
            CameraFollow.Shake(hurtShake);
            if (info.direction != Vector2.zero) CameraFollow.Kick(info.direction, 0.35f);
            SlowTime(0.05f, hurtStop);
            Fx.Spark(h.transform.position, new Color(1f, 0.4f, 0.3f), 10);
        }

        void OnPlayerDied()
        {
            var p = Game.Player;
            if (p != null) Fx.Explosion(p.transform.position, new Color(1f, 0.6f, 0.25f), 1.8f);
            Sfx.Play(SfxId.PlayerDeath, 1f, 0f);
            CameraFollow.Shake(1f);
            CameraFollow.Punch(0.12f);
            SlowTime(0.25f, 0.9f); // драматичное замедление
            StartCoroutine(KnellAfter(1.1f));
        }

        // погребальный колокол — когда отгремел взрыв
        IEnumerator KnellAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (Game.State == GameState.LevelLost) Sfx.Play(SfxId.DeathKnell, 0.9f, 0f);
        }

        // ------------------------------------------------------------------ Босс, комбо, лут

        void OnBossAwakened(Enemy boss)
        {
            Sfx.PlayAt(SfxId.BossRoar, boss.transform.position, 1f, 0f);
            CameraFollow.Shake(0.7f);
            CameraFollow.Punch(0.08f);
        }

        void OnBossPhase(Enemy boss, int phase)
        {
            Sfx.PlayAt(SfxId.BossRoar, boss.transform.position, 0.9f, 0f, phase == 3 ? 1.25f : 1.1f);
            Fx.Text((Vector2)boss.transform.position + Vector2.up * 1.8f, phase == 3 ? "ЯРОСТЬ!" : "ФАЗА 2", new Color(1f, 0.3f, 0.25f));
            Fx.Pulse(boss.transform.position, 3f, new Color(1f, 0.35f, 0.2f, 0.7f), 0.5f);
            CameraFollow.Shake(0.6f);
        }

        static readonly int[] ComboMilestones = { 3, 5, 8, 12, 20 };
        static readonly string[] ComboShouts = { "КОМБО!", "БЕШЕНЫЙ КОТ!", "МЯУ-НСТР!", "КОТОКАЛИПСИС!", "ЛЕГЕНДА!" };

        void OnComboChanged(int combo, float mult)
        {
            if (combo < 2) return;
            Sfx.Play(SfxId.Combo, 0.6f, 0f, 1f + 0.07f * Mathf.Min(combo, 16));
            int idx = System.Array.IndexOf(ComboMilestones, combo);
            if (idx >= 0 && Game.Player != null)
            {
                Fx.Text((Vector2)Game.Player.transform.position + Vector2.up * 1.3f, ComboShouts[idx], new Color(1f, 0.55f, 0.2f));
                CameraFollow.Punch(0.05f);
            }
        }

        void OnComboEnded(int combo, int bonus)
        {
            if (bonus > 0 && Game.Player != null)
                Fx.Text((Vector2)Game.Player.transform.position + Vector2.up * 1.1f, $"Комбо ×{combo}: +{bonus} рыб.", new Color(1f, 0.85f, 0.35f));
        }

        float _lastCoin;
        int _coinStreak;

        void OnPickup(Pickup p)
        {
            if (p.type == PickupType.Heart)
            {
                Sfx.Play(SfxId.Heal, 0.8f, 0f);
                Fx.Text((Vector2)p.transform.position + Vector2.up * 0.5f, "+1 HP", new Color(1f, 0.45f, 0.5f));
                return;
            }
            // подряд собранные рыбки звучат всё выше — приятно собирать россыпь
            _coinStreak = Time.unscaledTime - _lastCoin < 0.9f ? _coinStreak + 1 : 0;
            _lastCoin = Time.unscaledTime;
            Sfx.Play(SfxId.Coin, 0.7f, 0.02f, 1f + 0.06f * Mathf.Min(_coinStreak, 12));
        }

        void OnNewRecord() => Sfx.Play(SfxId.UltReady, 0.8f, 0f, 1.2f);

        void OnFireBlocked() => Sfx.Play(SfxId.Empty, 0.6f);
        void OnReloadStarted() => Sfx.Play(SfxId.ReloadStart, 0.7f);
        void OnReloadFinished() => Sfx.Play(SfxId.ReloadEnd, 0.75f);

        void OnUltReady()
        {
            Sfx.Play(SfxId.UltReady, 0.8f, 0f);
            if (Game.Player != null) Fx.Pulse(PlayerPos, 1.2f, new Color(1f, 0.8f, 0.35f, 0.7f), 0.4f);
        }

        void OnUltLaunched(Vector2 at, float radius, float delay)
        {
            // свист рассчитан на 1.1 с падения — подгоняем высотой тона под настоящее время
            Sfx.PlayAt(SfxId.NutWhistle, at, 0.8f, 0f, 1.1f / Mathf.Max(0.3f, delay));
        }

        void OnUltImpact(Vector2 at, float radius, int kills)
        {
            Sfx.PlayAt(SfxId.NutImpact, at, 1f, 0.03f);
            CameraFollow.Shake(nutShake);
            CameraFollow.Punch(0.12f);
            SlowTime(0.05f, nutStop);
            Fx.Text(at + Vector2.up * (radius + 0.4f), kills > 1 ? $"×{kills}!" : kills == 1 ? "БАМ!" : "мимо…",
                    kills > 0 ? new Color(1f, 0.8f, 0.3f) : new Color(0.8f, 0.8f, 0.85f));
        }

        void OnSecret(SecretFish f)
        {
            Sfx.Play(SfxId.Pickup, 1f, 0f);
            Fx.Text((Vector2)f.transform.position + Vector2.up * 0.6f, $"Секрет! +{f.value} рыб.", new Color(0.45f, 0.85f, 1f));
        }

        void OnLevelStarted(int level)
        {
            FxParticles.ClearAll();
            Sfx.Play(SfxId.LevelStart, 0.7f, 0f);
        }

        void OnLevelWon(int level)
        {
            Sfx.Play(SfxId.Win, 0.9f, 0f);
            if (Game.Player != null) Fx.Confetti(PlayerPos);
            CameraFollow.Punch(0.08f);
        }

        void OnLevelLost(LoseReason reason)
        {
            // при смерти уже гремит взрыв танка; по таймеру — глухой удар без всяких колокольчиков
            if (reason == LoseReason.TimeOut) Sfx.Play(SfxId.PlayerHurt, 0.8f, 0f, 0.7f);
        }

        void OnExitLocked()
        {
            if (Time.unscaledTime - _lastLockToast < 2.5f) return;
            _lastLockToast = Time.unscaledTime;
            Sfx.Play(SfxId.Empty, 0.6f);
            if (Game.Level != null)
                Fx.Text(Game.Level.ExitPosition + Vector2.up, EnemyRegistry.Boss != null ? "Сначала победи босса!" : $"Сначала зачисти арену ({EnemyRegistry.Count})",
                        new Color(1f, 0.8f, 0.3f));
        }
    }
}
