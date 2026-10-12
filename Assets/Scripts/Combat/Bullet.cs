// Снаряд с рикошетом. Без коллайдера и без Rigidbody: каждый кадр делает CircleCast
// и сам отражается от стен через Vector2.Reflect — так углы всегда точные.
// Bullet.All — все снаряды в полёте (для лимита на экране и для ИИ, который уворачивается).
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public class Bullet : MonoBehaviour
    {
        public const float DefaultRadius = 0.12f;

        public float speed = 8f;
        public float radius = DefaultRadius;
        public int maxBounces = 1;
        public int damage = 1;
        public float lifetime = 10f;
        public float splashRadius = 0f;   // фугас: при разрыве ранит всех чужих в радиусе
        public int splashDamage = 0;

        public GameObject Owner { get; private set; }
        public Team Team { get; private set; }
        public Vector2 Direction { get; private set; }
        public Vector2 Position => transform.position;
        public Vector2 Velocity => Direction * speed;
        public Color Color => _color;
        public int Bounces { get; private set; }

        static readonly List<Bullet> Alive = new List<Bullet>();
        public static IReadOnlyList<Bullet> All => Alive;
        float _dieAt, _nextTrail;
        Color _color = Color.white;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Alive.Clear();

        /// <summary>Сколько снарядов этого владельца сейчас в полёте (для лимита как в Wii Tanks).</summary>
        public static int CountOwnedBy(GameObject owner)
        {
            int n = 0;
            foreach (var b in Alive) if (b != null && b.Owner == owner) n++;
            return n;
        }

        /// <summary>Создаёт снаряд. prefab можно не задавать — будет кружок-примитив (размер — по radius).</summary>
        public static Bullet Spawn(Vector2 position, Vector2 direction, GameObject owner, Team team, Color color,
                                   float speed, int maxBounces, GameObject prefab = null, int damage = 1, float radius = DefaultRadius)
        {
            Transform parent = Game.LevelRoot;
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, position, Quaternion.identity, parent);
            else
            {
                go = Prims.Create("Bullet", parent, Prims.Circle, Prims.Light(color, 0.4f), position, Vector2.one * (radius * 2.4f), 20);
                Prims.Child("Glow", go.transform, Prims.Glow, Prims.WithAlpha(color, 0.55f), Vector2.zero, Vector2.one * 3.2f, 19);
            }

            var b = go.GetComponent<Bullet>();
            if (b == null) b = go.AddComponent<Bullet>();
            b.Owner = owner;
            b.Team = team;
            b.speed = speed;
            b.maxBounces = maxBounces;
            b.damage = Mathf.Max(1, damage);
            b.radius = radius;
            b._color = color;
            b.SetDirection(direction);
            GameEvents.RaiseBulletFired(b);
            return b;
        }

        void OnEnable() { Alive.Add(this); _dieAt = Time.time + lifetime; }
        void OnDisable() { Alive.Remove(this); }

        void SetDirection(Vector2 d)
        {
            Direction = d.normalized;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
        }

        void Update()
        {
            if (Time.time > _dieAt) { Destroy(gameObject); return; }
            if (Time.time >= _nextTrail)
            {
                _nextTrail = Time.time + 0.02f;
                Fx.Trail(transform.position, _color);
            }

            float remaining = speed * Time.deltaTime;
            // несколько итераций — если в одном кадре снаряд отскочил в угол и сразу ещё раз
            for (int guard = 0; guard < 4 && remaining > 0f; guard++)
            {
                Vector2 pos = transform.position;
                Transform ignore = Bounces == 0 && Owner != null ? Owner.transform : null;

                if (!Ricochet.CastNearest(pos, radius, Direction, remaining, ignore, out var hit))
                {
                    transform.position = pos + Direction * remaining;
                    return;
                }

                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null)
                {
                    if (CanDamage(target, hit.collider, out bool self))
                        // свой рикошет ранит на 1 — иначе тяжёлая пушка убивала бы кота одним отскоком
                        target.ApplyDamage(new DamageInfo(self ? 1 : damage, Owner, Team, DamageKind.Bullet, hit.point, Direction));
                    Explode(hit.centroid, true);
                    return;
                }

                if (Bounces >= maxBounces)
                {
                    Explode(hit.centroid, false);
                    return;
                }

                Bounces++;
                remaining -= hit.distance;
                transform.position = hit.centroid + hit.normal * Ricochet.Skin;
                SetDirection(Vector2.Reflect(Direction, hit.normal));
                Fx.Spark(hit.point, _color, 4);
                GameEvents.RaiseBulletBounced(this, hit.point);
            }
        }

        bool CanDamage(IDamageable target, Collider2D col, out bool self)
        {
            var cfg = Game.Config;
            self = Owner != null && col.transform.IsChildOf(Owner.transform);
            if (self) return cfg.selfDamage;
            if (target.Team == Team && Team == Team.Enemy) return cfg.friendlyFire;
            return true;
        }

        static readonly Collider2D[] SplashBuf = new Collider2D[32];
        static readonly System.Collections.Generic.List<IDamageable> SplashHit = new System.Collections.Generic.List<IDamageable>();

        void Splash(Vector2 at)
        {
            Fx.Explosion(at, _color, 0.7f);
            SplashHit.Clear();
            int n = Physics2D.OverlapCircle(at, splashRadius, ContactFilter2D.noFilter, SplashBuf);
            for (int i = 0; i < n; i++)
            {
                var t = SplashBuf[i] != null ? SplashBuf[i].GetComponentInParent<IDamageable>() : null;
                if (t == null || t.IsDead || SplashHit.Contains(t) || t.Team == Team) continue; // своих фугас не задевает
                SplashHit.Add(t);
                t.ApplyDamage(new DamageInfo(splashDamage, Owner, Team, DamageKind.Explosion, at, ((Vector2)SplashBuf[i].transform.position - at).normalized));
            }
        }

        void Explode(Vector2 at, bool hitTarget)
        {
            if (splashRadius > 0f) Splash(at);
            Fx.Spark(at, _color, 6);
            GameEvents.RaiseBulletExploded(this, at, hitTarget);
            Destroy(gameObject);
        }
    }
}
