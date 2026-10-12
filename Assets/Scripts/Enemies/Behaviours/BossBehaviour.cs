// Босс «Краб-Планшет»: просыпается, когда кот подъехал (или попал в него), и чередует атаки.
// Фазы по HP: 1 (>60%) — карусель и веер; 2 (60–30%) — + кольцо, рывок, подмога; 3 (<30%) — ЯРОСТЬ: всё быстрее и плотнее.
// Каждая атака честно предупреждает: надпись, свечение, лазер или круг на полу — есть время среагировать.
// Движение — ChaseBehaviour (держит дистанцию), на время атак босс его «придерживает» (Hold).
using System.Collections.Generic;
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    public class BossBehaviour : EnemyBehaviour
    {
        public Transform turret;
        public float bodyRadius = 0.9f;
        public float wakeRange = 11f;
        public Color bulletColor = new Color(1f, 0.45f, 0.15f);

        public bool Awake_ { get; private set; }
        public int Phase { get; private set; } = 1;

        enum Attack { None, Volley, Carousel, Nova, Charge, Summon }

        ChaseBehaviour _chase;
        Rigidbody2D _rb;
        Attack _current;
        Attack _last;
        float _cooldown, _t, _fireTimer, _spin, _spinDir = 1f;
        int _step, _summons;
        Vector2 _dashDir;
        readonly List<SpriteRenderer> _fx = new List<SpriteRenderer>();
        SpriteRenderer _aura, _warnRing, _dashLine;

        protected override void OnSpawned()
        {
            _chase = GetComponent<ChaseBehaviour>();
            _rb = GetComponent<Rigidbody2D>();
            _cooldown = 1.5f;
            Enemy.Health.Damaged += (h, i) => Wake();
            // визуальные подсказки атак (живут на уровне, а не на боссе — не крутятся вместе с ним)
            _aura = MakeFx("BossAura", Prims.Glow, 4f, 7);
            _warnRing = MakeFx("BossWarn", Prims.Ring, 1f, 6);
            _dashLine = MakeFx("BossDash", Prims.Square, 1f, 6);
        }

        SpriteRenderer MakeFx(string name, Sprite sprite, float scale, int order)
        {
            var go = Prims.Create(name, Game.LevelRoot, sprite, new Color(1f, 0.3f, 0.2f, 0f), transform.position, Vector2.one * scale, order);
            var sr = go.GetComponent<SpriteRenderer>();
            _fx.Add(sr);
            return sr;
        }

        void OnDestroy()
        {
            foreach (var f in _fx) if (f != null) Destroy(f.gameObject);
        }

        protected override void OnDeath(DamageInfo info)
        {
            foreach (var f in _fx) if (f != null) f.enabled = false;
            Fx.Explosion(transform.position, Def.color, 2.2f);
        }

        void Wake()
        {
            if (Awake_) return;
            Awake_ = true;
            if (_chase != null) _chase.ForceAggro();
            GameEvents.RaiseBossAwakened(Enemy);
        }

        float Frac => Enemy.Health.Hp / (float)Mathf.Max(1, Enemy.Health.maxHp);
        float Speed => Phase == 3 ? 1.35f : Phase == 2 ? 1.15f : 1f;

        void Update()
        {
            if (!Active) { HideFx(); return; }
            Vector2 pos = Position;
            foreach (var f in _fx) if (f != null && f != _dashLine) f.transform.position = pos;

            if (!Awake_)
            {
                if (DistanceToPlayer < wakeRange) Wake();
                else return;
            }

            int phase = Frac > 0.6f ? 1 : Frac > 0.3f ? 2 : 3;
            if (phase != Phase)
            {
                Phase = phase;
                GameEvents.RaiseBossPhase(Enemy, phase);
                if (_chase != null) _chase.SpeedScale = phase == 3 ? 1.35f : 1.1f;
            }

            // ядро-турель всегда немного вращается, в карусели — быстро
            _spin += (_current == Attack.Carousel ? 0f : 40f * Speed) * Time.deltaTime;
            if (turret != null && _current != Attack.Carousel) turret.rotation = Quaternion.Euler(0, 0, _spin);

            if (_current == Attack.None)
            {
                SetAura(0.12f + 0.06f * Mathf.Sin(Time.time * 4f), new Color(1f, 0.5f, 0.2f));
                _cooldown -= Time.deltaTime * Speed;
                if (_cooldown <= 0f) Begin(Pick());
                return;
            }
            _t += Time.deltaTime * Speed;
            switch (_current)
            {
                case Attack.Volley: UpdateVolley(); break;
                case Attack.Carousel: UpdateCarousel(); break;
                case Attack.Nova: UpdateNova(); break;
                case Attack.Charge: UpdateCharge(); break;
                case Attack.Summon: UpdateSummon(); break;
            }
        }

        Attack Pick()
        {
            var options = new List<Attack> { Attack.Volley, Attack.Carousel };
            if (Phase >= 2) { options.Add(Attack.Nova); options.Add(Attack.Charge); }
            if (Phase >= 2 && _summons < Phase - 1) options.Add(Attack.Summon);
            options.Remove(_last);
            return options[Random.Range(0, options.Count)];
        }

        void Begin(Attack a)
        {
            _current = a;
            _last = a;
            _t = 0f;
            _step = 0;
            _fireTimer = 0f;
            if (_chase != null) _chase.Hold = a == Attack.Carousel || a == Attack.Nova || a == Attack.Charge || a == Attack.Summon;
            string shout = a == Attack.Carousel ? "КАРУСЕЛЬ!" : a == Attack.Nova ? "КОЛЬЦО!" : a == Attack.Charge ? "ТАРАН!" : a == Attack.Summon ? "ПОДМОГА!" : null;
            if (shout != null) Fx.Text(Position + Vector2.up * 1.6f, shout, new Color(1f, 0.45f, 0.3f));
            GameEvents.RaiseBossAttack(Enemy, a.ToString());
        }

        void End()
        {
            _current = Attack.None;
            _cooldown = Phase == 3 ? 0.8f : Phase == 2 ? 1.1f : 1.5f;
            if (_chase != null) _chase.Hold = false;
            HideFx();
        }

        // ------------------------------------------------------------------ Атаки

        // Веер: 3/5/7 снарядов по игроку после короткого прицеливания
        void UpdateVolley()
        {
            SetAura(0.25f + 0.25f * _t / 0.5f, new Color(1f, 0.35f, 0.2f));
            if (_t < 0.5f) return;
            int count = Phase == 1 ? 3 : Phase == 2 ? 5 : 7;
            Vector2 aim = (PlayerPosition - Position).normalized;
            for (int i = 0; i < count; i++)
            {
                float a = (i - (count - 1) / 2f) * 11f;
                Fire(Quaternion.Euler(0, 0, a) * aim, 7f, 1);
            }
            End();
        }

        // Карусель: босс замирает и выпускает вращающиеся спирали снарядов
        void UpdateCarousel()
        {
            const float windup = 0.7f;
            if (_t < windup)
            {
                SetAura(0.2f + 0.5f * _t / windup, new Color(1f, 0.6f, 0.15f));
                _spin += 360f * (_t / windup) * Time.deltaTime * _spinDir;
                if (turret != null) turret.rotation = Quaternion.Euler(0, 0, _spin);
                return;
            }
            float duration = Phase == 3 ? 4f : 3.2f;
            int arms = Phase == 1 ? 2 : Phase == 2 ? 3 : 4;
            float rate = Phase == 3 ? 0.11f : 0.14f;
            _spin += (Phase == 3 ? 190f : 150f) * Time.deltaTime * Speed * _spinDir;
            if (turret != null) turret.rotation = Quaternion.Euler(0, 0, _spin);
            SetAura(0.6f + 0.15f * Mathf.Sin(Time.time * 20f), new Color(1f, 0.6f, 0.15f));
            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                _fireTimer = rate;
                for (int i = 0; i < arms; i++)
                {
                    float a = (_spin + i * 360f / arms) * Mathf.Deg2Rad;
                    Fire(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 5f, 0);
                }
            }
            if (_t >= windup + duration) { _spinDir = -_spinDir; End(); }
        }

        // Кольцо: круг на полу расширяется — затем 2–3 волны по 20 снарядов со сдвигом
        void UpdateNova()
        {
            const float windup = 0.6f;
            int waves = Phase == 3 ? 3 : 2;
            if (_t < windup)
            {
                float k = _t / windup;
                _warnRing.enabled = true;
                _warnRing.transform.localScale = Vector3.one * Mathf.Lerp(1f, 6f, k);
                _warnRing.color = new Color(1f, 0.3f, 0.2f, 0.7f * (1f - k * 0.5f));
                SetAura(0.3f + 0.4f * k, new Color(1f, 0.3f, 0.2f));
                return;
            }
            _warnRing.enabled = false;
            float waveTime = windup + _step * 0.35f;
            if (_step < waves && _t >= waveTime)
            {
                const int n = 20;
                float offset = _step * 180f / n;
                for (int i = 0; i < n; i++)
                {
                    float a = (offset + i * 360f / n) * Mathf.Deg2Rad;
                    Fire(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 5.5f, 0);
                }
                CameraFollow.Shake(0.15f);
                _step++;
            }
            if (_step >= waves && _t > waveTime + 0.3f) End();
        }

        // Таран: красная полоса на полу показывает направление → рывок → оглушение у стены
        void UpdateCharge()
        {
            const float windup = 0.65f;
            if (_step == 0)
            {
                _dashDir = (PlayerPosition - Position).normalized;
                _step = 1;
            }
            if (_t < windup)
            {
                float k = _t / windup;
                _dashLine.enabled = true;
                _dashLine.transform.position = Position + _dashDir * 4f;
                _dashLine.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(_dashDir.y, _dashDir.x) * Mathf.Rad2Deg);
                _dashLine.transform.localScale = new Vector3(8f, bodyRadius * 2f, 1f);
                bool blink = k < 0.6f || Mathf.Repeat(Time.time * 18f, 1f) < 0.6f;
                _dashLine.color = new Color(1f, 0.25f, 0.2f, blink ? 0.12f + 0.2f * k : 0.05f);
                SetAura(0.3f + 0.5f * k, new Color(1f, 0.25f, 0.2f));
                return;
            }
            _dashLine.enabled = false;
            float dashTime = 0.7f;
            if (_t < windup + dashTime)
            {
                Vector2 step = _dashDir * (11f * Speed * Time.deltaTime);
                if (Ricochet.CastNearest(_rb.position, bodyRadius * 0.9f, _dashDir, step.magnitude + 0.05f, transform, out var hit)
                    && hit.collider.GetComponentInParent<IDamageable>() == null)
                {
                    // врезался в стену — оглушён
                    CameraFollow.Shake(0.5f);
                    Fx.Spark(hit.point, Color.white, 12);
                    _t = windup + dashTime;
                    return;
                }
                _rb.MovePosition(_rb.position + step);
                return;
            }
            SetAura(0.08f, new Color(0.6f, 0.7f, 1f)); // оглушение — окно для атаки
            if (_t > windup + dashTime + 0.9f) End();
        }

        // Подмога: два шустрика из-под панциря
        void UpdateSummon()
        {
            SetAura(0.3f + 0.3f * Mathf.Sin(Time.time * 12f), new Color(0.9f, 0.4f, 1f));
            if (_t < 0.8f) return;
            var def = Game.Config.Enemies.Get(EnemyType.Runner);
            if (def != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 p = Position + Random.insideUnitCircle.normalized * 1.8f;
                    var cell = LevelBuilder.WorldToCell(p);
                    if (!Ctx.Level.IsWalkable(cell)) cell = LevelBuilder.WorldToCell(Position);
                    var e = EnemyFactory.Spawn(Game.Config.Enemies, new EnemySpawn(cell, EnemyType.Runner, 1), Ctx, Game.LevelRoot);
                    if (e != null) Fx.Pulse(e.transform.position, 0.8f, new Color(0.9f, 0.4f, 1f, 0.7f), 0.3f);
                }
            }
            _summons++;
            End();
        }

        // ------------------------------------------------------------------

        void Fire(Vector2 dir, float speed, int bounces)
        {
            Bullet.Spawn(Position + dir * (bodyRadius + 0.2f), dir, gameObject, Team.Enemy, bulletColor, speed, bounces, null, 1, 0.13f);
        }

        void SetAura(float alpha, Color c)
        {
            if (_aura == null) return;
            _aura.enabled = true;
            _aura.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
        }

        void HideFx()
        {
            if (_warnRing != null) _warnRing.enabled = false;
            if (_dashLine != null) _dashLine.enabled = false;
        }
    }
}
