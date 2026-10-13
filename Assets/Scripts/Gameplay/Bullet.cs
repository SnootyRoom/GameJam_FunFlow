// Снаряд с рикошетом. Без коллайдера и без Rigidbody: каждый кадр делает CircleCast
// и сам отражается от стен через Vector2.Reflect — так углы всегда точные.
// Эффекты и звук снаряд не рисует сам — только сообщает о событиях (Fired, Bounced, HitTarget, HitWall).
// Их слушает FxPlayer (см. GameFlow → Fx Library); звук можно повесить туда же.
using System;
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
        public Team Team { get; private set; }
        public Vector2 Direction { get; private set; }
        public int Bounces { get; private set; }

        // ----- события для эффектов и звука -----
        public static event Action<Bullet> Fired;                     // только что вылетел из ствола
        public static event Action<Vector2, Vector2> Bounced;          // (точка, нормаль стены)
        public static event Action<Vector2, Health> HitTarget;         // попал в кого-то со здоровьем
        public static event Action<Vector2> HitWall;                   // разбился о стену (кончились отскоки)

        /// <summary>Размер спрайта снаряда в юнитах (по большей стороне), если он задан спрайтом.</summary>
        public const float SpriteSize = 0.32f;

        /// <summary>Снаряды сбивают друг друга. Задаётся в GameFlow (галочка bulletsCollide).</summary>
        public static bool BulletsCollide = true;

        static readonly List<Bullet> Alive = new List<Bullet>();
        float _dieAt;
        bool _dead, _checkSpawn;

        // Статика переживает перезапуск Play Mode, если в Unity выключена перезагрузка домена — чистим явно.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Alive.Clear();
            Fired = null; Bounced = null; HitTarget = null; HitWall = null;
            BulletsCollide = true;
        }

        /// <summary>Все снаряды в полёте — для ИИ, который от них уворачивается. Только читать.</summary>
        public static IReadOnlyList<Bullet> Active => Alive;

        /// <summary>Сколько снарядов этого владельца сейчас в полёте (для лимита как в Wii Tanks).</summary>
        public static int CountOwnedBy(GameObject owner)
        {
            int n = 0;
            foreach (var b in Alive) if (b != null && b.Owner == owner) n++;
            return n;
        }

        /// <summary>
        /// Создаёт снаряд. Вид: prefab, иначе sprite (в исходных цветах), иначе кружок-примитив цвета color.
        /// scale — размер относительно обычного снаряда (и картинка, и радиус попадания), damage — урон.
        /// </summary>
        public static Bullet Spawn(Vector2 position, Vector2 direction, GameObject owner, Color color,
                                   float speed, int maxBounces, GameObject prefab = null, Sprite sprite = null,
                                   float scale = 1f, int damage = 1)
        {
            // снаряд живёт там же, где стрелок (в корне уровня) — и удаляется вместе с уровнем
            Transform parent = owner != null ? owner.transform.parent : null;
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, position, Quaternion.identity, parent);
            else if (sprite != null)
            {
                Vector2 s = sprite.bounds.size;
                float k = SpriteSize / Mathf.Max(0.01f, Mathf.Max(s.x, s.y));
                go = Prims.Create("Bullet", parent, sprite, Color.white, position, Vector2.one * k, 20);
            }
            else go = Prims.Create("Bullet", parent, Prims.Circle, color, position, Vector2.one * 0.24f, 20);

            var b = go.GetComponent<Bullet>();
            if (b == null) b = go.AddComponent<Bullet>();
            b.Owner = owner;
            var ownerHp = owner != null ? owner.GetComponentInParent<Health>() : null;
            b.Team = ownerHp != null ? ownerHp.team : Team.Enemy;
            b.speed = speed;
            b.maxBounces = maxBounces;
            b.damage = damage;
            if (!Mathf.Approximately(scale, 1f))
            {
                go.transform.localScale *= scale;
                b.radius *= scale;
            }
            b.SetDirection(direction);
            b._checkSpawn = true; // упор в стену/врага проверим в первом кадре — когда стрелок уже выставил урон
            Fired?.Invoke(b);
            return b;
        }

        /// <summary>Кот получает от любого снаряда 1 HP (у него 3 HP на жизнь), враги — полный урон снаряда.
        /// Иначе свой рикошет основным орудием (урон 2) отнимал бы у кота сразу две трети здоровья.</summary>
        int DamageTo(Health hp) => hp.IsPlayer ? Mathf.Min(damage, 1) : damage;

        void OnEnable() { Alive.Add(this); _dieAt = Time.time + lifetime; }
        void OnDisable() { Alive.Remove(this); }

        void SetDirection(Vector2 d)
        {
            Direction = d.normalized;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Выстрел в упор: ствол уже в стене или во враге. Стена — снаряд разбивается (а не отскакивает в стрелка),
        /// враг — получает попадание. true — снаряд закончился.
        /// </summary>
        bool ResolveSpawnOverlap()
        {
            Vector2 pos = transform.position;
            var blocker = Ricochet.SpawnBlocker(pos, radius, Owner != null ? Owner.transform : null);
            if (blocker == null) return false;
            var hp = blocker.GetComponentInParent<Health>();
            if (hp != null)
            {
                if (Health.CanHurt(Team, hp)) hp.Damage(DamageTo(hp), Team);
                HitTarget?.Invoke(pos, hp);
            }
            else HitWall?.Invoke(pos);
            Kill();
            return true;
        }

        /// <summary>
        /// Снаряды сбивают друг друга (как в Wii Tanks). Проверяем «на пролёт» за этот кадр, а не только текущие
        /// позиции, — иначе встречные быстрые снаряды проскакивают друг сквозь друга между кадрами.
        /// Снаряды одного стрелка друг друга не сбивают.
        /// </summary>
        bool Clash(float step)
        {
            Vector2 p = transform.position, v = Direction * step;
            foreach (var other in Alive)
            {
                if (other == this || other == null || other._dead || other.Owner == Owner) continue;
                Vector2 q = other.transform.position, w = other.Direction * (other.speed * Time.deltaTime);
                Vector2 d0 = q - p, dv = w - v;
                float t = dv.sqrMagnitude > 1e-6f ? Mathf.Clamp01(-Vector2.Dot(d0, dv) / dv.sqrMagnitude) : 0f;
                Vector2 gap = d0 + dv * t;
                if (gap.magnitude >= radius + other.radius) continue;
                HitWall?.Invoke(p + v * t + gap * 0.5f); // эффект и звук — как удар о стену
                other.Kill();
                Kill();
                return true;
            }
            return false;
        }

        void Kill()
        {
            _dead = true;
            Destroy(gameObject);
        }

        void Update()
        {
            if (_dead) return;
            if (Time.time > _dieAt) { Kill(); return; }
            if (_checkSpawn)
            {
                _checkSpawn = false;
                if (ResolveSpawnOverlap()) return;
            }

            float remaining = speed * Time.deltaTime;
            if (BulletsCollide && Clash(remaining)) return;
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
                    if (Health.CanHurt(Team, hp)) hp.Damage(DamageTo(hp), Team); // без friendly fire снаряд просто гаснет о своего
                    HitTarget?.Invoke(hit.centroid, hp);
                    Kill();
                    return;
                }

                if (Bounces >= maxBounces)
                {
                    HitWall?.Invoke(hit.centroid);
                    Kill();
                    return;
                }

                Bounces++;
                remaining -= hit.distance;
                transform.position = hit.centroid + hit.normal * Ricochet.Skin;
                SetDirection(Vector2.Reflect(Direction, hit.normal));
                Bounced?.Invoke(hit.point, hit.normal);
            }
        }
    }
}
