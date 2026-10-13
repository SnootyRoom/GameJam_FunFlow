// Стадии босса (краба): чем меньше у него HP, тем он злее. Все стадии описаны в EnemyDefinition.bossStages:
// первая действует с начала боя, следующие включаются, когда HP падает ниже порога.
//   • Стадию нельзя проскочить одним ударом — даже орех ульты оставит босса на пороге следующей стадии.
//   • Переход: босс замирает, ревёт и неуязвим, зовёт миньонов и выпускает кольцо снарядов.
//   • Особые атаки: время от времени босс встаёт, мигает (замах) и выпускает кольцо или спираль снарядов.
// Скорость, залпы и рывки меняет у EnemyCrawler / EnemyShooter — множители от значений после роста сложности.
using System;
using UnityEngine;

namespace CatTanks
{
    [Serializable]
    public class BossStage
    {
        [Tooltip("Стадия начинается, когда HP ≤ этой доли от максимума (0.66 — потерял треть). У первой стадии не важно")]
        [Range(0.05f, 1f)] public float belowHp = 0.66f;
        [Tooltip("Пауза с рёвом при входе в стадию, сек — босс не атакует и неуязвим")]
        public float roarTime = 1.1f;

        [Header("Движение и рывки")]
        [Tooltip("× к скорости бега")]
        public float moveSpeedMultiplier = 1f;
        [Tooltip("× к скорости рывка")]
        public float lungeSpeedMultiplier = 1f;
        [Tooltip("× к дальности рывка: и с какого расстояния замахивается, и как долго летит")]
        public float lungeRangeMultiplier = 1f;
        [Tooltip("Рывков подряд в одной атаке")]
        [Min(1)] public int lungeChain = 1;

        [Header("Стрельба")]
        [Tooltip("× к паузе между выстрелами (меньше 1 — чаще)")]
        public float fireIntervalMultiplier = 1f;
        [Tooltip("× к скорости снарядов")]
        public float bulletSpeedMultiplier = 1f;
        [Tooltip("Снарядов в залпе (веер)")]
        [Min(1)] public int volley = 1;
        [Tooltip("Угол между снарядами веера, градусы")]
        public float volleySpread = 14f;
        [Tooltip("Рикошетов у снаряда (и при прицеливании). Не меньше, чем в EnemyDefinition")]
        public int maxBounces = 2;
        [Tooltip("Упреждение: 0 — бьёт в кота, 1 — туда, где кот будет, если не свернёт")]
        [Range(0f, 1f)] public float leadTarget = 0f;

        [Header("Особые атаки (босс стоит и мигает, потом стреляет)")]
        [Tooltip("Как часто, сек. Кольцо и спираль чередуются случайно")]
        public float specialInterval = 7f;
        [Tooltip("Замах перед особой атакой, сек")]
        public float specialWindup = 0.8f;

        [Tooltip("Снарядов в кольце. 0 — без кольца")]
        public int ringBullets = 0;
        [Tooltip("× к скорости снарядов кольца (медленнее — чтобы можно было проскочить в щель)")]
        public float ringSpeedMultiplier = 0.75f;
        public int ringBounces = 1;

        [Tooltip("Снарядов в каждом рукаве спирали. 0 — без спирали")]
        public int spiralShots = 0;
        [Min(1)] public int spiralArms = 2;
        [Tooltip("Сколько секунд длится спираль")]
        public float spiralTime = 1.6f;
        [Tooltip("На сколько градусов проворачивается спираль за это время")]
        public float spiralTurn = 300f;
        public float spiralSpeedMultiplier = 0.8f;
        public int spiralBounces = 0;

        [Header("Миньоны (при входе в стадию)")]
        [Tooltip("Кого звать — имя ассета EnemyDefinition")]
        public string summonId = "Enemy_Crawler";
        public int summonCount = 0;
    }

    public class BossStages : MonoBehaviour, ILevelActor
    {
        public BossStage[] stages = new BossStage[0];

        /// <summary>Босс перешёл в стадию (точка, номер стадии с 1) — для звука и тряски.</summary>
        public static event Action<Vector2, int> StageChanged;
        /// <summary>Босс начал замах перед особой атакой (точка) — для звука.</summary>
        public static event Action<Vector2> SpecialTelegraphed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { StageChanged = null; SpecialTelegraphed = null; }

        /// <summary>Текущая стадия с 1. 0 — стадий нет.</summary>
        public int Stage { get; private set; }

        enum Mode { None, Roar, RingWindup, SpiralWindup, Spiral }

        LevelContext _ctx;
        Health _hp;
        EnemyCrawler _crawler;
        EnemyShooter _shooter;
        ShotTelegraph _telegraph;
        Rigidbody2D _rb;

        // базовые значения (уже с ростом сложности) — множители стадий считаются от них
        float _baseSpeed, _baseLungeSpeed, _baseLungeTime, _baseAttackRange, _baseFireInterval, _baseBulletSpeed;
        int _baseBounces;

        Mode _mode;
        float _modeStart, _modeEnd;
        float _nextSpecial;
        float _spiralAngle, _spiralSign;
        int _spiralFired;

        BossStage Current => Stage >= 1 && Stage <= stages.Length ? stages[Stage - 1] : null;

        public void Init(LevelContext ctx)
        {
            _ctx = ctx;
            _hp = GetComponent<Health>();
            _crawler = GetComponent<EnemyCrawler>();
            _shooter = GetComponent<EnemyShooter>();
            _rb = GetComponent<Rigidbody2D>();
            _telegraph = GetComponent<ShotTelegraph>();
            if (_telegraph == null) _telegraph = gameObject.AddComponent<ShotTelegraph>();

            if (_crawler != null)
            {
                _baseSpeed = _crawler.speed;
                _baseLungeSpeed = _crawler.lungeSpeed;
                _baseLungeTime = _crawler.lungeTime;
                _baseAttackRange = _crawler.attackRange;
            }
            if (_shooter != null)
            {
                _baseFireInterval = _shooter.fireInterval;
                _baseBulletSpeed = _shooter.bulletSpeed;
                _baseBounces = _shooter.maxBounces;
            }

            // стадии — по убыванию порога, чтобы номер стадии рос вместе с потерянным HP
            // (сортируем копию: массив пришёл из ассета EnemyDefinition, его трогать нельзя)
            stages = stages != null ? (BossStage[])stages.Clone() : new BossStage[0];
            Array.Sort(stages, (a, b) => b.belowHp.CompareTo(a.belowHp));
            if (stages.Length == 0) return;

            ApplyStage(1);
            _nextSpecial = Time.time + Current.specialInterval * 0.6f;
            if (_hp != null) _hp.OnHpChanged += OnHp;
        }

        void OnDestroy() { if (_hp != null) _hp.OnHpChanged -= OnHp; }

        /// <summary>HP, при котором начинается стадия index (с 1).</summary>
        int ThresholdHp(int stage) => _hp == null ? 0 : Mathf.FloorToInt(stages[stage - 1].belowHp * _hp.maxHp);

        void OnHp(int hp, int max)
        {
            if (hp <= 0 || Stage >= stages.Length) return;
            if (hp <= ThresholdHp(Stage + 1)) EnterStage(Stage + 1);
        }

        void ApplyStage(int stage)
        {
            Stage = stage;
            var s = Current;
            if (_crawler != null)
            {
                _crawler.speed = _baseSpeed * s.moveSpeedMultiplier;
                _crawler.lungeSpeed = _baseLungeSpeed * s.lungeSpeedMultiplier;
                _crawler.lungeTime = _baseLungeTime * s.lungeRangeMultiplier;
                _crawler.attackRange = _baseAttackRange * s.lungeRangeMultiplier;
                _crawler.lungeChain = s.lungeChain;
            }
            if (_shooter != null)
            {
                _shooter.fireInterval = _baseFireInterval * s.fireIntervalMultiplier;
                _shooter.bulletSpeed = _baseBulletSpeed * s.bulletSpeedMultiplier;
                _shooter.volley = s.volley;
                _shooter.volleySpread = s.volleySpread;
                _shooter.maxBounces = Mathf.Max(_baseBounces, s.maxBounces);
                _shooter.leadTarget = s.leadTarget;
            }
            // следующую стадию одним ударом не проскочить: HP остановится на её пороге
            if (_hp != null) _hp.HitFloor = stage < stages.Length ? Mathf.Max(1, ThresholdHp(stage + 1)) : 0;
        }

        void EnterStage(int stage)
        {
            ApplyStage(stage);
            StageChanged?.Invoke(transform.position, stage);
            if (_hp != null) _hp.Invulnerable = true; // пока ревёт — не бьётся: смена стадии не «бесплатное» окно для урона
            Begin(Mode.Roar, Current.roarTime);
        }

        void Update()
        {
            if (_ctx == null || !_ctx.PlayerAlive) { if (_mode != Mode.None) End(); return; }
            var s = Current;
            if (s == null) return;

            switch (_mode)
            {
                case Mode.Roar:
                case Mode.RingWindup:
                case Mode.SpiralWindup:
                    float k = Mathf.Clamp01((Time.time - _modeStart) / Mathf.Max(0.01f, _modeEnd - _modeStart));
                    // мигать начинаем после вспышки урона (0.08 с): стадия меняется прямо в момент попадания,
                    // и мигание запомнило бы красный цвет вспышки как «родной»
                    if (Time.time >= _modeStart + 0.1f) _telegraph.Show(transform.position, transform.right, 0f, 0, k, drawLine: false);
                    if (Time.time < _modeEnd) return;
                    var finished = _mode;
                    if (finished == Mode.SpiralWindup) { StartSpiral(s); return; }
                    End();
                    if (finished == Mode.Roar) { Summon(s); FireRing(s); }
                    else FireRing(s);
                    return;

                case Mode.Spiral:
                    UpdateSpiral(s);
                    return;
            }

            if (Time.time < _nextSpecial) return;
            bool hasRing = s.ringBullets > 0, hasSpiral = s.spiralShots > 0;
            if (!hasRing && !hasSpiral) return;
            // особая атака — только когда босс не занят другой атакой, виден на экране и кот рядом
            if ((_crawler != null && _crawler.Busy) || (_shooter != null && _shooter.Busy)) return;
            float range = _shooter != null ? _shooter.sightRange : 12f;
            if (!_ctx.IsOnScreen(transform.position, 0.5f) ||
                Vector2.Distance(transform.position, _ctx.Player.transform.position) > range)
            {
                _nextSpecial = Time.time + 0.5f;
                return;
            }
            bool spiral = hasSpiral && (!hasRing || UnityEngine.Random.value < 0.5f);
            SpecialTelegraphed?.Invoke(transform.position);
            Begin(spiral ? Mode.SpiralWindup : Mode.RingWindup, s.specialWindup);
        }

        void FixedUpdate()
        {
            if (_mode != Mode.None && _rb != null) _rb.linearVelocity = Vector2.zero; // стоит на месте, пока ревёт или стреляет
        }

        void Begin(Mode mode, float duration)
        {
            if (_crawler != null) { _crawler.Interrupt(); _crawler.enabled = false; }
            if (_shooter != null) { _shooter.Interrupt(0f); _shooter.enabled = false; }
            _mode = mode;
            _modeStart = Time.time;
            _modeEnd = Time.time + duration;
        }

        void End()
        {
            _mode = Mode.None;
            _telegraph.Hide();
            if (_hp != null) _hp.Invulnerable = false;
            if (_crawler != null) _crawler.enabled = true;
            if (_shooter != null)
            {
                _shooter.enabled = true;
                _shooter.Interrupt(0.5f); // после особой атаки — короткая пауза перед обычным выстрелом
            }
            var s = Current;
            if (s != null) _nextSpecial = Time.time + s.specialInterval * UnityEngine.Random.Range(0.85f, 1.15f);
        }

        void Shoot(Vector2 dir, float speedMultiplier, int bounces)
        {
            Bullet.Spawn(_shooter.Muzzle(dir), dir, gameObject, _shooter.bulletColor, _shooter.bulletSpeed * speedMultiplier,
                         bounces, _shooter.bulletPrefab, _shooter.bulletSprite);
        }

        static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        /// <summary>Кольцо снарядов во все стороны, каждый раз со случайным поворотом — щели не в одном месте.</summary>
        void FireRing(BossStage s)
        {
            if (s.ringBullets <= 0 || _shooter == null) return;
            float offset = UnityEngine.Random.Range(0f, 360f / s.ringBullets);
            for (int i = 0; i < s.ringBullets; i++)
                Shoot(Dir(offset + i * 360f / s.ringBullets), s.ringSpeedMultiplier, s.ringBounces);
        }

        // ------------------------------------------------------------------ Спираль
        // Рукава спирали начинаются от кота и проворачиваются: стоять на месте нельзя, надо обходить по кругу против вращения.

        void StartSpiral(BossStage s)
        {
            _telegraph.Hide();
            _mode = Mode.Spiral;
            _modeStart = Time.time;
            _modeEnd = Time.time + s.spiralTime;
            _spiralFired = 0;
            Vector2 toPlayer = (Vector2)_ctx.Player.transform.position - (Vector2)transform.position;
            _spiralAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            _spiralSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        }

        void UpdateSpiral(BossStage s)
        {
            if (_shooter == null || s.spiralShots <= 0) { End(); return; }
            float k = Mathf.Clamp01((Time.time - _modeStart) / Mathf.Max(0.01f, s.spiralTime));
            int due = Mathf.Min(s.spiralShots, Mathf.FloorToInt(k * s.spiralShots) + 1);
            for (; _spiralFired < due; _spiralFired++)
            {
                float a = _spiralAngle + _spiralSign * s.spiralTurn * _spiralFired / Mathf.Max(1, s.spiralShots - 1);
                for (int arm = 0; arm < s.spiralArms; arm++)
                    Shoot(Dir(a + arm * 360f / s.spiralArms), s.spiralSpeedMultiplier, s.spiralBounces);
            }
            if (Time.time >= _modeEnd) End();
        }

        // ------------------------------------------------------------------ Миньоны

        /// <summary>Зовёт миньонов на свободные клетки вокруг себя — не вплотную к коту.</summary>
        void Summon(BossStage s)
        {
            if (s.summonCount <= 0 || string.IsNullOrEmpty(s.summonId) || _ctx.Spawner == null || _ctx.Data == null) return;
            Vector2 me = transform.position, cat = _ctx.Player.transform.position;
            int placed = 0;
            for (int attempt = 0; attempt < 60 && placed < s.summonCount; attempt++)
            {
                Vector2 p = me + UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(1.8f, 3.5f);
                var cell = LevelGrid.WorldToCell(p);
                Vector2 c = LevelGrid.CellToWorld(cell);
                if (!_ctx.Data.IsWalkable(cell) || Vector2.Distance(c, cat) < 2.5f) continue;
                // между боссом и клеткой не должно быть стены — миньон не появится в соседней комнате
                Vector2 d = c - me;
                if (Ricochet.CastNearest(me, 0.3f, d.normalized, d.magnitude, transform, out _)) continue;
                if (_ctx.Spawner.SpawnEnemy(_ctx, s.summonId, cell) != null) placed++;
            }
        }
    }
}
