// Падающий орех (ульта). Сначала на земле растёт тень и пульсирует круг опасности — у врагов (и кота!)
// есть время убежать. Потом удар: урон всем в радиусе (в центре — насмерть, у края — частично),
// стены и укрытия под орехом разрушаются, остаётся воронка.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public class NutStrike : MonoBehaviour
    {
        UltimateSettings _s;
        GameObject _owner;
        Vector2 _target;
        float _t;
        Transform _nut, _shadow, _ring, _ringInner;
        SpriteRenderer _shadowSr, _ringSr, _innerSr;
        static readonly List<Health> Hit = new List<Health>();
        static readonly Collider2D[] Overlap = new Collider2D[64];

        const float DropHeight = 9f;

        public static NutStrike Launch(Vector2 target, UltimateSettings s, GameObject owner)
        {
            var go = new GameObject("NutStrike");
            go.transform.SetParent(Game.LevelRoot, false);
            go.transform.position = target;
            var strike = go.AddComponent<NutStrike>();
            strike._s = s;
            strike._owner = owner;
            strike._target = target;
            strike.Build();
            Hazards.Add(strike, target, s.blastRadius, Time.time + s.fallTime);
            GameEvents.RaiseUltimateLaunched(target, s.blastRadius, s.fallTime);
            return strike;
        }

        void Build()
        {
            float R = _s.blastRadius;
            _ring = Prims.Child("DangerRing", transform, Prims.Ring, new Color(1f, 0.3f, 0.2f, 0.8f), Vector2.zero, Vector2.one * R * 2f, 4).transform;
            _ringSr = _ring.GetComponent<SpriteRenderer>();
            _ringInner = Prims.Child("DangerFill", transform, Prims.Circle, new Color(1f, 0.25f, 0.15f, 0.12f), Vector2.zero, Vector2.one * R * 2f, 3).transform;
            _innerSr = _ringInner.GetComponent<SpriteRenderer>();
            _shadow = Prims.Child("NutShadow", transform, Prims.Circle, new Color(0f, 0f, 0f, 0.1f), Vector2.zero, Vector2.one * 0.3f, 5).transform;
            _shadowSr = _shadow.GetComponent<SpriteRenderer>();
            _nut = Prims.Child("Nut", transform, Prims.Walnut, Color.white, Vector2.up * DropHeight, Vector2.one * 3f, 60).transform;
        }

        void OnDestroy() { Hazards.Remove(this); }

        void Update()
        {
            _t += Time.deltaTime / Mathf.Max(0.05f, _s.fallTime);
            float t = Mathf.Clamp01(_t);
            float fall = t * t; // свободное падение — ускоряется

            _nut.localPosition = Vector3.up * (DropHeight * (1f - fall));
            _nut.localScale = Vector3.one * Mathf.Lerp(3.2f, _s.blastRadius * 0.9f, fall);
            _nut.localRotation = Quaternion.Euler(0, 0, 200f * t + 20f * Mathf.Sin(t * 9f));

            float shadowSize = Mathf.Lerp(0.4f, _s.blastRadius * 1.6f, fall);
            _shadow.localScale = new Vector3(shadowSize, shadowSize * 0.8f, 1f);
            _shadowSr.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.1f, 0.5f, t));

            // круг опасности пульсирует всё чаще ближе к удару
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, t));
            _ringSr.color = new Color(1f, 0.3f, 0.2f, Mathf.Lerp(0.45f, 0.95f, pulse));
            _innerSr.color = new Color(1f, 0.25f, 0.15f, Mathf.Lerp(0.06f, 0.2f, t) + 0.05f * pulse);
            _ring.localScale = Vector3.one * _s.blastRadius * 2f * (1f + 0.03f * pulse);

            if (_t >= 1f) Impact();
        }

        void Impact()
        {
            float R = _s.blastRadius;
            Vector2 c = _target;

            // урон: каждому объекту со здоровьем — один раз
            Hit.Clear();
            int n = Physics2D.OverlapCircle(c, R, ContactFilter2D.noFilter, Overlap);
            int kills = 0;
            for (int i = 0; i < n; i++)
            {
                var hp = Overlap[i] != null ? Overlap[i].GetComponentInParent<Health>() : null;
                if (hp == null || hp.IsDead || Hit.Contains(hp)) continue;
                Hit.Add(hp);
                Vector2 p = hp.transform.position;
                float dist = Vector2.Distance(p, c);
                Vector2 dir = dist > 0.01f ? (p - c) / dist : Vector2.up;
                if (hp.team == Team.Player)
                {
                    hp.ApplyDamage(new DamageInfo(_s.selfDamage, _owner, Team.Player, DamageKind.Explosion, p, dir));
                    continue;
                }
                int dmg = dist <= R * _s.coreFraction ? _s.coreDamage : _s.edgeDamage;
                hp.ApplyDamage(new DamageInfo(dmg, _owner, Team.Player, DamageKind.Ultimate, p, dir));
                if (hp.IsDead) kills++;
                // отбрасываем выживших
                var rb = hp.GetComponent<Rigidbody2D>();
                if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.AddForce(dir * 6f, ForceMode2D.Impulse);
            }

            if (_s.destroyWalls && Game.Level != null) Game.Level.DestroyCells(c, R * 0.85f);

            // воронка остаётся до конца уровня
            var crater = Prims.Create("Crater", Game.LevelRoot, Prims.Crater, Color.white, c, Vector2.one * R * 2.2f, -9);
            crater.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            Fx.NutImpact(c, R);
            GameEvents.RaiseUltimateImpact(c, R, kills);
            Destroy(gameObject);
        }
    }
}
