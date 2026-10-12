// «Ощущение удара»: стоп-кадр (hit stop) и тряска камеры в ответ на игровые события.
//   • выстрел игрока — лёгкий толчок камеры;
//   • попадание по врагу — микро-пауза и толчок;
//   • убийство врага — пауза длиннее;
//   • попадание по игроку — самая заметная пауза и тряска, чтобы урон нельзя было не заметить.
// Геймплей про это ничего не знает — только поднимает события. Создаётся GameFlow (или добавь на его объект сам,
// чтобы крутить числа в инспекторе). Стоп-кадр — через GameTime, поэтому не спорит с паузой и замедлением ульты.
using UnityEngine;

namespace CatTanks
{
    public class HitFeel : MonoBehaviour
    {
        [System.Serializable]
        public struct Kick
        {
            [Tooltip("Стоп-кадр, секунды реального времени (0 — без него)")]
            public float freeze;
            [Tooltip("Амплитуда тряски камеры, клетки")]
            public float shake;
            public float shakeTime;
            public Kick(float freeze, float shake, float shakeTime) { this.freeze = freeze; this.shake = shake; this.shakeTime = shakeTime; }
        }

        public bool enableFreeze = true;
        public bool enableShake = true;
        [Tooltip("Насколько замедляется время во время стоп-кадра (0 — полная остановка)")]
        [Range(0f, 0.3f)] public float freezeScale = 0.03f;

        public Kick playerShot = new Kick(0f, 0.05f, 0.08f);
        public Kick minigunShot = new Kick(0f, 0.02f, 0.05f);
        public Kick enemyHit = new Kick(0.045f, 0.08f, 0.1f);
        public Kick enemyKilled = new Kick(0.07f, 0.14f, 0.16f);
        public Kick playerHit = new Kick(0.12f, 0.3f, 0.3f);

        GameFlow _flow;
        float _freezeUntil;
        bool _frozen;

        public void Bind(GameFlow flow)
        {
            Unbind();
            _flow = flow;
            Bullet.Fired += OnFired;
            Bullet.HitTarget += OnHitTarget;
            if (_flow != null) _flow.OnEnemyKilled += OnEnemyKilled;
        }

        void Unbind()
        {
            Bullet.Fired -= OnFired;
            Bullet.HitTarget -= OnHitTarget;
            if (_flow != null) _flow.OnEnemyKilled -= OnEnemyKilled;
        }

        void OnDestroy()
        {
            Unbind();
            if (_frozen) GameTime.Clear(this);
        }

        void OnFired(Bullet b)
        {
            if (b.Team != Team.Player) return;
            Apply(b.maxBounces > 0 ? playerShot : minigunShot); // у пулемёта рикошетов нет
        }

        void OnHitTarget(Vector2 at, Health target)
        {
            if (target == null) return;
            Apply(target.IsPlayer ? playerHit : enemyHit);
        }

        void OnEnemyKilled(GameObject enemy) => Apply(enemyKilled);

        void Apply(Kick k)
        {
            if (enableShake && k.shake > 0f) CameraFollow.Shake(k.shake, k.shakeTime);
            if (!enableFreeze || k.freeze <= 0f) return;
            // несколько ударов подряд не складываются — берём самый длинный хвост
            _freezeUntil = Mathf.Max(_freezeUntil, Time.unscaledTime + k.freeze);
            if (!_frozen) { _frozen = true; GameTime.Set(this, freezeScale); }
        }

        void Update()
        {
            if (_frozen && Time.unscaledTime >= _freezeUntil)
            {
                _frozen = false;
                GameTime.Clear(this);
            }
        }
    }
}
