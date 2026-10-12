// Ульта «Орех»: каждые N убийств игрока → +1 заряд.
// Зажми ПКМ/Q — время замедляется, на земле появляется прицел (в пределах maxRange от кота).
// Отпусти — с неба падает орех (NutStrike): через fallTime секунд удар по площади, стены рушатся.
// Красный прицел — кот сам в зоне удара. Отменить — отпустить кнопку, наведя прицел на кота.
using System;
using UnityEngine;

namespace CatTanks
{
    [Serializable]
    public class UltimateSettings
    {
        public int maxCharges = 2;
        public int maxUsesPerLevel = 2;
        public int startCharges = 0;

        [Header("Прицеливание")]
        public float maxRange = 10f;          // насколько далеко от кота можно сбросить
        public float aimTimeScale = 0.35f;    // замедление времени, пока целишься

        [Header("Удар")]
        public float fallTime = 1.1f;         // секунд от сброса до удара — время убежать
        public float blastRadius = 2.6f;
        [Range(0f, 1f)] public float coreFraction = 0.55f; // ближе этой доли радиуса — насмерть
        public int coreDamage = 99;
        public int edgeDamage = 6;            // у края — серьёзно, но крепкий враг (громила, краб) выживет
        public int selfDamage = 2;            // если кот сам под орехом
        public bool destroyWalls = true;
    }

    public class PlayerUltimate : MonoBehaviour
    {
        public UltimateSettings settings = new UltimateSettings();
        public int killsPerCharge = 5;

        public int Charges { get; private set; }
        public int KillProgress { get; private set; }
        public int UsesLeft { get; private set; }
        public float Progress01 => Charges >= settings.maxCharges ? 1f : KillProgress / (float)Mathf.Max(1, killsPerCharge);
        public bool CanActivate => Charges > 0 && UsesLeft > 0;

        public bool IsAiming { get; private set; }
        public Vector2 AimPoint { get; private set; }
        /// <summary>Кот сам окажется в зоне удара.</summary>
        public bool AimHitsSelf => IsAiming && Vector2.Distance(AimPoint, transform.position) < settings.blastRadius + 0.4f;

        public event Action Changed;
        public event Action ChargeGained;

        PlayerTank _tank;
        Transform _aimRoot, _aimRing, _aimFill, _rangeRing;
        SpriteRenderer _aimRingSr, _aimFillSr, _aimCenterSr;

        static readonly Color AimColor = new Color(1f, 0.8f, 0.3f, 0.95f);
        static readonly Color DangerColor = new Color(1f, 0.25f, 0.2f, 0.95f);

        void Awake() { _tank = GetComponent<PlayerTank>(); }

        public void Configure(UltimateSettings s, int killsPerCharge)
        {
            settings = s ?? new UltimateSettings();
            this.killsPerCharge = Mathf.Max(1, killsPerCharge);
            Charges = Mathf.Min(settings.startCharges, settings.maxCharges);
            UsesLeft = settings.maxUsesPerLevel;
            KillProgress = 0;
            Changed?.Invoke();
        }

        void OnEnable() { GameEvents.EnemyKilled += OnEnemyKilled; }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            CancelAim();
        }

        void OnDestroy()
        {
            GameTime.Clear(this);
            if (_aimRoot != null) Destroy(_aimRoot.gameObject);
        }

        void OnEnemyKilled(Enemy e, DamageInfo info)
        {
            if (!info.ByPlayer || info.kind == DamageKind.Ultimate) return; // ульта сама себя не заряжает
            if (Charges >= settings.maxCharges) return;
            KillProgress += e != null && e.IsElite ? 2 : 1;
            if (KillProgress >= killsPerCharge)
            {
                KillProgress = Charges + 1 < settings.maxCharges ? KillProgress - killsPerCharge : 0;
                Charges++;
                ChargeGained?.Invoke();
                if (UsesLeft > 0) GameEvents.RaiseUltimateReady();
            }
            Changed?.Invoke();
        }

        /// <summary>Отладка (U при debugKeys): +1 заряд и +1 использование.</summary>
        public void DebugAddCharge()
        {
            Charges = Mathf.Min(Charges + 1, settings.maxCharges);
            UsesLeft = Mathf.Max(UsesLeft, 1);
            ChargeGained?.Invoke();
            GameEvents.RaiseUltimateReady();
            Changed?.Invoke();
        }

        void Update()
        {
            if (_tank != null && !_tank.InputEnabled) { CancelAim(); return; }

            if (!IsAiming)
            {
                if (GameInput.UltimatePressed())
                {
                    if (CanActivate) BeginAim();
                    else GameEvents.RaisePlayerFireBlocked();
                }
                return;
            }

            UpdateAim();
            if (!GameInput.UltimateHeld())
            {
                // отпустил над самим котом — передумал
                if (Vector2.Distance(GameInput.MouseWorld(), transform.position) < 0.7f) CancelAim();
                else Drop();
            }
        }

        void BeginAim()
        {
            IsAiming = true;
            GameTime.Set(this, settings.aimTimeScale);
            EnsureAimVisuals();
            _aimRoot.gameObject.SetActive(true);
            UpdateAim();
            Changed?.Invoke();
        }

        void UpdateAim()
        {
            Vector2 me = transform.position;
            AimPoint = me + Vector2.ClampMagnitude(GameInput.MouseWorld() - me, settings.maxRange);

            _aimRing.position = AimPoint;
            _aimFill.position = AimPoint;
            _rangeRing.position = me;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
            Color c = AimHitsSelf ? DangerColor : AimColor;
            _aimRingSr.color = Prims.WithAlpha(c, 0.7f + 0.3f * pulse);
            _aimFillSr.color = Prims.WithAlpha(c, 0.12f + 0.06f * pulse);
            _aimCenterSr.color = AimHitsSelf ? DangerColor : Color.white;
            _aimRing.localScale = Vector3.one * settings.blastRadius * 2f * (1f + 0.04f * pulse);
        }

        void Drop()
        {
            Vector2 at = AimPoint;
            CancelAim();
            Charges--;
            UsesLeft--;
            NutStrike.Launch(at, settings, gameObject);
            Changed?.Invoke();
        }

        void CancelAim()
        {
            if (!IsAiming) return;
            IsAiming = false;
            GameTime.Clear(this);
            if (_aimRoot != null) _aimRoot.gameObject.SetActive(false);
            Changed?.Invoke();
        }

        void EnsureAimVisuals()
        {
            if (_aimRoot != null) return;
            // отдельный объект на уровне: не вращается с танком и удаляется вместе с уровнем
            _aimRoot = new GameObject("NutAim").transform;
            _aimRoot.SetParent(transform.parent, false);
            // граница дальности — тёмная, чтобы читалась на светлом полу
            _rangeRing = Prims.Child("Range", _aimRoot, Prims.Ring, new Color(0.12f, 0.14f, 0.22f, 0.45f), Vector2.zero, Vector2.one * settings.maxRange * 2f, 2).transform;
            _aimFill = Prims.Child("Fill", _aimRoot, Prims.Circle, AimColor, Vector2.zero, Vector2.one * settings.blastRadius * 2f, 3).transform;
            _aimFillSr = _aimFill.GetComponent<SpriteRenderer>();
            _aimRing = Prims.Child("Ring", _aimRoot, Prims.Ring, AimColor, Vector2.zero, Vector2.one * settings.blastRadius * 2f, 4).transform;
            _aimRingSr = _aimRing.GetComponent<SpriteRenderer>();
            // орех в центре прицела — масштаб компенсирует масштаб кольца
            var center = Prims.Child("Center", _aimRoot, Prims.Walnut, Color.white, Vector2.zero, Vector2.one * 0.7f, 5);
            _aimCenterSr = center.GetComponent<SpriteRenderer>();
            center.transform.SetParent(_aimRing, true);
        }
    }
}
