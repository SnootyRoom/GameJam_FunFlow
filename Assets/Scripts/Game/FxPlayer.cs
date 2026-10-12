// Проигрывает эффекты из FxLibrary в ответ на игровые события: выстрел, рикошет, попадание, смерть врага.
// Геймплейный код про эффекты ничего не знает — только поднимает события (Bullet.*, GameFlow.OnEnemyKilled).
// Создаёт и держит GameFlow; эффекты живут в корне уровня и удаляются вместе с ним.
using UnityEngine;

namespace CatTanks
{
    public class FxPlayer
    {
        readonly FxLibrary _lib;
        Transform _root;

        public FxPlayer(FxLibrary library) { _lib = library; }

        /// <summary>Куда складывать эффекты (корень текущего уровня).</summary>
        public void SetRoot(Transform root) { _root = root; }

        public void Subscribe()
        {
            Bullet.Fired += OnFired;
            Bullet.Bounced += OnBounced;
            Bullet.HitTarget += OnHitTarget;
            Bullet.HitWall += OnHitWall;
        }

        public void Unsubscribe()
        {
            Bullet.Fired -= OnFired;
            Bullet.Bounced -= OnBounced;
            Bullet.HitTarget -= OnHitTarget;
            Bullet.HitWall -= OnHitWall;
        }

        void OnFired(Bullet b) => Play(_lib.muzzleFlash, b.transform.position, b.Direction);
        void OnBounced(Vector2 at, Vector2 normal) => Play(_lib.ricochet, at, normal);
        void OnHitTarget(Vector2 at, Health target) => Play(_lib.targetHit, at, Vector2.right);
        void OnHitWall(Vector2 at) => Play(_lib.wallHit, at, Vector2.right);
        public void OnEnemyKilled(GameObject enemy) { if (enemy != null) Play(_lib.enemyDeath, enemy.transform.position, Vector2.right); }

        public void Play(FxLibrary.Effect fx, Vector2 at, Vector2 dir)
        {
            if (fx == null || fx.duration <= 0f || fx.size <= 0f) return;
            var sprite = fx.sprite != null ? fx.sprite : Prims.Circle;
            Vector2 s = sprite.bounds.size;
            float k = fx.size / Mathf.Max(0.01f, Mathf.Max(s.x, s.y));

            var go = Prims.Create("Fx", _root, sprite, fx.color, at, Vector2.one * k, 25);
            float angle = fx.randomRotation ? 90f * Random.Range(0, 4) : Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0, 0, angle);
            go.AddComponent<FxSprite>().Setup(fx.duration, fx.growTo);
        }
    }
}
