// Снаряд с рикошетом. Без коллайдера и без Rigidbody: каждый кадр делает CircleCast
// и сам отражается от стен через Vector2.Reflect — так углы всегда точные.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public class Bullet : MonoBehaviour
    {
        public float speed = 8f;
        public float radius = 0.12f;
        public int maxBounces = 1;
        public int damage = 1;
        public float lifetime = 10f;

        public GameObject Owner { get; private set; }
        public Vector2 Direction { get; private set; }
        public int Bounces { get; private set; }

        static readonly List<Bullet> Alive = new List<Bullet>();
        float _dieAt;

        /// <summary>Сколько снарядов этого владельца сейчас в полёте (для лимита как в Wii Tanks).</summary>
        public static int CountOwnedBy(GameObject owner)
        {
            int n = 0;
            foreach (var b in Alive) if (b != null && b.Owner == owner) n++;
            return n;
        }

        /// <summary>Создаёт снаряд. prefab можно не задавать — будет кружок-примитив.</summary>
        public static Bullet Spawn(Vector2 position, Vector2 direction, GameObject owner, Color color,
                                   float speed, int maxBounces, GameObject prefab = null)
        {
            // снаряд живёт там же, где стрелок (в корне уровня) — и удаляется вместе с уровнем
            Transform parent = owner != null ? owner.transform.parent : null;
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, position, Quaternion.identity, parent);
            else go = Prims.Create("Bullet", parent, Prims.Circle, color, position, Vector2.one * 0.24f, 20);

            var b = go.GetComponent<Bullet>();
            if (b == null) b = go.AddComponent<Bullet>();
            b.Owner = owner;
            b.speed = speed;
            b.maxBounces = maxBounces;
            b.SetDirection(direction);
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

                var hp = hit.collider.GetComponentInParent<Health>();
                if (hp != null)
                {
                    hp.Damage(damage);
                    Explode(hit.centroid);
                    return;
                }

                if (Bounces >= maxBounces)
                {
                    Explode(hit.centroid);
                    return;
                }

                Bounces++;
                remaining -= hit.distance;
                transform.position = hit.centroid + hit.normal * Ricochet.Skin;
                SetDirection(Vector2.Reflect(Direction, hit.normal));
                SpawnSpark(hit.point);
            }
        }

        void Explode(Vector2 at)
        {
            SpawnSpark(at);
            Destroy(gameObject);
        }

        // Заглушка эффекта: короткая вспышка. Потом замени на спрайт искр от художника.
        void SpawnSpark(Vector2 at)
        {
            var go = Prims.Create("Spark", transform.parent, Prims.Circle, new Color(1f, 0.9f, 0.4f, 0.8f), at, Vector2.one * 0.35f, 25);
            Destroy(go, 0.06f);
        }
    }
}
