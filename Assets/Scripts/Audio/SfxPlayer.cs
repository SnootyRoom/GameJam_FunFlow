// Связывает игровые события со звуками Sfx и музыкой Music (громкости и высоты — из ветки shishki, Feedback.cs).
// Геймплейный код про звук ничего не знает — только поднимает события; как и FxPlayer, создаётся в GameFlow.
using CatTanks.UI;
using UnityEngine;

namespace CatTanks
{
    public class SfxPlayer
    {
        readonly GameFlow _flow;
        int _lastPlayerHp = int.MaxValue;

        public SfxPlayer(GameFlow flow) { _flow = flow; }

        public void Subscribe()
        {
            Sfx.Warmup();
            Bullet.Fired += OnFired;
            Bullet.Bounced += OnBounced;
            Bullet.HitTarget += OnHitTarget;
            Bullet.HitWall += OnHitWall;
            PlayerTank.FireBlocked += OnFireBlocked;
            PlayerUltimate.ChargeReady += OnUltReady;
            NutStrike.Launched += OnNutLaunched;
            NutStrike.Impacted += OnNutImpact;
            PixelButton.Hovered += OnUiHover;
            PixelButton.Pressed += OnUiClick;
            TitleView.Dismissed += OnTitleDismissed;
            _flow.OnStateChanged += OnStateChanged;
            ApplyMusic(_flow.State);
            _flow.OnLevelStarted += OnLevelStarted;
            _flow.OnLevelCompleted += OnLevelCompleted;
            _flow.OnPlayerKilled += OnPlayerKilled;
            _flow.OnPlayerHpChanged += OnPlayerHp;
            _flow.OnEnemyKilled += OnEnemyKilled;
            _flow.OnExitBlocked += OnExitBlocked;
        }

        public void Unsubscribe()
        {
            Bullet.Fired -= OnFired;
            Bullet.Bounced -= OnBounced;
            Bullet.HitTarget -= OnHitTarget;
            Bullet.HitWall -= OnHitWall;
            PlayerTank.FireBlocked -= OnFireBlocked;
            PlayerUltimate.ChargeReady -= OnUltReady;
            NutStrike.Launched -= OnNutLaunched;
            NutStrike.Impacted -= OnNutImpact;
            PixelButton.Hovered -= OnUiHover;
            PixelButton.Pressed -= OnUiClick;
            TitleView.Dismissed -= OnTitleDismissed;
            _flow.OnStateChanged -= OnStateChanged;
            _flow.OnLevelStarted -= OnLevelStarted;
            _flow.OnLevelCompleted -= OnLevelCompleted;
            _flow.OnPlayerKilled -= OnPlayerKilled;
            _flow.OnPlayerHpChanged -= OnPlayerHp;
            _flow.OnEnemyKilled -= OnEnemyKilled;
            _flow.OnExitBlocked -= OnExitBlocked;
        }

        // ----- бой -----
        void OnFired(Bullet b)
        {
            if (b.Team == Team.Player) Sfx.Play(SfxId.Shot, 0.9f, 0.04f);
            else Sfx.PlayAt(SfxId.EnemyShot, b.transform.position, 0.75f, 0.06f);
        }

        void OnBounced(Vector2 at, Vector2 normal) => Sfx.PlayAt(SfxId.Bounce, at, 0.55f, 0.08f);
        void OnHitWall(Vector2 at) => Sfx.PlayAt(SfxId.WallHit, at, 0.6f, 0.1f);

        void OnHitTarget(Vector2 at, Health target)
        {
            if (target != null && target.IsPlayer) return; // игроку — свой звук через изменение здоровья
            Sfx.PlayAt(SfxId.Hit, at, 0.8f);
        }

        void OnEnemyKilled(GameObject enemy)
        {
            if (enemy != null) Sfx.PlayAt(SfxId.Explosion, enemy.transform.position, 0.85f, 0.08f);
        }

        void OnPlayerHp(int hp, int max)
        {
            if (hp < _lastPlayerHp && hp > 0) Sfx.Play(SfxId.PlayerHurt, 0.9f);
            _lastPlayerHp = hp;
        }

        void OnFireBlocked() => Sfx.Play(SfxId.Empty, 0.6f);

        // ----- ульта -----
        void OnUltReady() => Sfx.Play(SfxId.UltReady, 0.8f, 0f);
        void OnNutLaunched(Vector2 at, float radius, float fallTime) =>
            Sfx.PlayAt(SfxId.NutWhistle, at, 0.8f, 0f, 1.1f / Mathf.Max(0.3f, fallTime));
        void OnNutImpact(Vector2 at, float radius, int kills) => Sfx.PlayAt(SfxId.NutImpact, at, 1f, 0.03f);

        // ----- ход игры -----
        void OnLevelStarted(LevelContext ctx)
        {
            _lastPlayerHp = int.MaxValue;
            Sfx.Play(SfxId.LevelStart, 0.7f, 0f);
        }

        void OnLevelCompleted(LevelContext ctx) => Sfx.Play(SfxId.Win, 0.9f, 0f);

        void OnPlayerKilled(LevelContext ctx)
        {
            Sfx.Play(SfxId.PlayerDeath, 1f, 0f);
            Sfx.Play(SfxId.DeathKnell, 0.9f, 0f);
        }

        void OnExitBlocked() => Sfx.Play(SfxId.Alert, 0.6f, 0f);

        // ----- интерфейс -----
        void OnUiHover() => Sfx.Play(SfxId.UiHover, 0.6f, 0.03f);
        void OnUiClick() => Sfx.Play(SfxId.UiClick, 0.8f, 0.02f);
        void OnTitleDismissed() => Sfx.Play(SfxId.MenuEnter, 0.9f, 0f);

        // пауза: звук только на переходах игра ↔ пауза (не при уходе в меню или смерти)
        GameState _lastState = GameState.Menu;
        void OnStateChanged(GameState s)
        {
            if (s == GameState.Paused && _lastState == GameState.Playing) Sfx.Play(SfxId.PauseOpen, 0.8f, 0f);
            else if (s == GameState.Playing && _lastState == GameState.Paused) Sfx.Play(SfxId.PauseClose, 0.8f, 0f);
            _lastState = s;
            ApplyMusic(s);
        }

        // музыка: меню и стартовый экран — спокойная тема, уровни — боевая; на паузе бой приглушён, после гибели тише
        static void ApplyMusic(GameState s)
        {
            switch (s)
            {
                case GameState.Menu: Music.Play(MusicTrack.Menu); break;
                case GameState.Paused: Music.Play(MusicTrack.Battle, muffled: true); break;
                case GameState.PlayerDead: Music.Play(MusicTrack.Battle, duck: 0.5f); break;
                default: Music.Play(MusicTrack.Battle); break;
            }
        }
    }
}
