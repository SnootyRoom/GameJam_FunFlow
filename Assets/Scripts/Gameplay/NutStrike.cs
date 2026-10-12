// Падающий орех (ульта, из ветки shishki). Сначала на земле растёт тень и пульсирует круг опасности —
// у врагов (и кота!) есть время убежать. Потом удар: урон всем в радиусе (в центре — насмерть, у края — частично),
// стены и укрытия под орехом разрушаются, остаётся воронка, камеру трясёт.
// Звук и эффекты подписываются на события Launched / Impacted.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public class NutStrike : MonoBehaviour
    {
        public static event Action<Vector2, float, float> Launched;   // (точка, радиус, время падения)
        public static event Action<Vector2, float, int> Impacted;     // (точка, радиус, сколько убито)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Launched = null; Impacted = null; Hit.Clear(); }

        UltimateSettings _s;
        LevelContext _ctx;
        Vector2 _target;
        float _t;
        Transform _nut, _shadow;
        SpriteRenderer _ringSr, _fillSr, _shadowSr;
        float _nutStartScale, _nutEndScale;

        static readonly List<Health> Hit = new List<Health>();
        static readonly Collider2D[] Overlap = new Collider2D[64];

        const float DropHeight = 9f;
        static readonly Color Danger = new Color(1f, 0.3f, 0.2f, 1f);

        public static NutStrike Launch(Vector2 target, UltimateSettings s, GameObject owner, LevelContext ctx, Sprite walnut)
        {
            var go = new GameObject("NutStrike");
            go.transform.SetParent(ctx != null ? ctx.Root : null, false);
            go.transform.position = target;
            var strike = go.AddComponent<NutStrike>();
            strike._s = s;
            strike._ctx = ctx;
            strike._target = target;
            strike.Build(walnut);
            Launched?.Invoke(target, s.blastRadius, s.fallTime);
            return strike;
        }

        void Build(Sprite walnut)
        {
            float d = _s.blastRadius * 2f;
            _fillSr = Child(PixelArt.DitherDisc(d), Danger, 3);
            _ringSr = Child(PixelArt.Ring(d, 2), Danger, 4);
            _shadowSr = Child(PixelArt.DitherDisc(_s.blastRadius * 1.6f), new Color(0f, 0f, 0f, 0.3f), 5);
            _shadow = _shadowSr.transform;

            var nutSprite = walnut != null ? walnut : PixelArt.AimEnd;
            var nut = Child(nutSprite, Color.white, 60);
            _nut = nut.transform;
            float w = nutSprite.bounds.size.x;
            _nutStartScale = 3.2f / w;                    // в небе — большой (ближе к камере)
            _nutEndScale = _s.blastRadius * 0.9f / w;     // у земли — размером с воронку
        }

        SpriteRenderer Child(Sprite sprite, Color color, int order)
        {
            return Prims.Child("Nut", transform, sprite, color, Vector2.zero, Vector2.one, order).GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            _t += Time.deltaTime / Mathf.Max(0.05f, _s.fallTime);
            float t = Mathf.Clamp01(_t);
            float fall = t * t; // свободное падение — ускоряется

            _nut.localPosition = Vector3.up * (DropHeight * (1f - fall));
            _nut.localScale = Vector3.one * Mathf.Lerp(_nutStartScale, _nutEndScale, fall);
            // поворот шагами по 90° — пиксель-арт не «мылится», а кувыркание читается
            _nut.localRotation = Quaternion.Euler(0, 0, 90f * Mathf.Floor(t * 8f));

            _shadow.localScale = Vector3.one * Mathf.Lerp(0.25f, 1f, fall);
            _shadowSr.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.15f, 0.5f, t));

            // круг опасности мигает всё чаще ближе к удару
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, t));
            _ringSr.color = new Color(Danger.r, Danger.g, Danger.b, Mathf.Lerp(0.45f, 1f, pulse));
            _fillSr.color = new Color(Danger.r, Danger.g, Danger.b, Mathf.Lerp(0.15f, 0.35f, t) + 0.1f * pulse);

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
                if (hp.IsPlayer)
                {
                    hp.Damage(_s.selfDamage, Team.Player, true);
                    continue;
                }
                hp.Damage(dist <= R * _s.coreFraction ? _s.coreDamage : _s.edgeDamage, Team.Player, true);
                if (hp.IsDead) { kills++; continue; }
                // выживших отбрасываем
                var rb = hp.GetComponent<Rigidbody2D>();
                Vector2 dir = dist > 0.01f ? (p - c) / dist : Vector2.up;
                if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.AddForce(dir * 6f, ForceMode2D.Impulse);
            }

            if (_s.destroyWalls && _ctx != null && _ctx.Builder != null) _ctx.Builder.DestroyCells(c, R * 0.85f);

            // воронка остаётся до конца уровня: над полом, под следами гусениц и всем остальным
            var crater = Prims.Create("Crater", transform.parent, PixelArt.Crater(R * 2.2f), Color.white, c, Vector2.one, -9);
            crater.transform.rotation = Quaternion.Euler(0, 0, 90f * UnityEngine.Random.Range(0, 4));

            CameraFollow.Shake(0.35f, 0.45f);
            Impacted?.Invoke(c, R, kills);
            Destroy(gameObject);
        }
    }
}
