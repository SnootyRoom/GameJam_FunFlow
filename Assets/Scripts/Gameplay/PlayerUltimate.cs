// Ульта «Орех» (из ветки shishki, переложена на нашу архитектуру и пиксельный стиль).
// Каждые killsPerCharge убийств игрока → +1 заряд. Зажми Q — время замедляется, на земле появляется прицел
// (в пределах maxRange от кота). Отпусти — с неба падает орех (NutStrike): через fallTime удар по площади,
// стены рушатся. Красный прицел — кот сам в зоне удара. Отменить — отпустить Q, наведя прицел на кота.
// Убийства самой ультой заряд не дают. Заряды живут в пределах уровня (танк создаётся заново на каждом).
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
        public int edgeDamage = 6;            // у края — серьёзно, но крепкий враг (краб) может выжить
        public int selfDamage = 2;            // если кот сам под орехом
        public bool destroyWalls = true;
    }

    public class PlayerUltimate : MonoBehaviour, ILevelActor
    {
        public UltimateSettings settings = new UltimateSettings();
        [Tooltip("Сколько убийств нужно на один заряд")]
        public int killsPerCharge = 3;
        [Tooltip("Картинка ореха (Assets/Art/Ult/walnut)")]
        public Sprite walnutSprite;

        [Header("Цвета прицела")]
        public Color aimColor = new Color(1f, 0.8f, 0.3f, 0.95f);
        public Color dangerColor = new Color(1f, 0.25f, 0.2f, 0.95f);
        public Color rangeColor = new Color(0.12f, 0.14f, 0.22f, 0.5f);

        public int Charges { get; private set; }
        public int KillProgress { get; private set; }
        public int UsesLeft { get; private set; }
        public float Progress01 => Charges >= settings.maxCharges ? 1f : KillProgress / (float)Mathf.Max(1, killsPerCharge);
        public bool CanActivate => Charges > 0 && UsesLeft > 0;

        public bool IsAiming { get; private set; }
        public Vector2 AimPoint { get; private set; }
        /// <summary>Кот сам окажется в зоне удара.</summary>
        public bool AimHitsSelf => IsAiming && Vector2.Distance(AimPoint, transform.position) < settings.blastRadius + 0.4f;

        public event Action Changed;                       // для HUD
        public static event Action ChargeReady;            // новый заряд, которым можно воспользоваться — для звука

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ChargeReady = null;

        LevelContext _ctx;
        Transform _aimRoot;
        SpriteRenderer _range, _fill, _ring, _center;

        public void Init(LevelContext ctx)
        {
            _ctx = ctx;
            ctx.EnemyKilled += OnEnemyKilled;
            Charges = Mathf.Min(settings.startCharges, settings.maxCharges);
            UsesLeft = settings.maxUsesPerLevel;
            KillProgress = 0;
            Changed?.Invoke();
        }

        void OnDisable() => CancelAim();

        void OnDestroy()
        {
            if (_ctx != null) _ctx.EnemyKilled -= OnEnemyKilled;
            GameTime.Clear(this);
            if (_aimRoot != null) Destroy(_aimRoot.gameObject);
        }

        void OnEnemyKilled(GameObject enemy)
        {
            var hp = enemy != null ? enemy.GetComponentInChildren<Health>() : null;
            if (hp == null || hp.LastAttacker != Team.Player || hp.LastHitByUltimate) return; // ульта сама себя не заряжает
            if (Charges >= settings.maxCharges) return;
            KillProgress++;
            if (KillProgress >= killsPerCharge)
            {
                KillProgress = 0;
                Charges++;
                if (UsesLeft > 0) ChargeReady?.Invoke();
            }
            Changed?.Invoke();
        }

        /// <summary>Отладка (U при debugKeys): +1 заряд и +1 использование.</summary>
        public void DebugAddCharge()
        {
            Charges = Mathf.Min(Charges + 1, settings.maxCharges);
            UsesLeft = Mathf.Max(UsesLeft, 1);
            ChargeReady?.Invoke();
            Changed?.Invoke();
        }

        void Update()
        {
            // пауза или стоп-кадр победы — прицеливание отменяем
            if (GameTime.Paused || Time.timeScale == 0f) { CancelAim(); return; } // при прицеливании время 0.35, не 0

            if (!IsAiming)
            {
                if (GameInput.UltimatePressed())
                {
                    if (CanActivate) BeginAim();
                    else PlayerTank.RaiseFireBlocked();
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

            _range.transform.position = me;
            _fill.transform.position = AimPoint;
            _ring.transform.position = AimPoint;
            _center.transform.position = AimPoint;
            // пульсирует прозрачность, а не размер — пиксели остаются ровными
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
            Color c = AimHitsSelf ? dangerColor : aimColor;
            _ring.color = WithAlpha(c, 0.7f + 0.3f * pulse);
            _fill.color = WithAlpha(c, 0.25f + 0.15f * pulse);
            _center.color = AimHitsSelf ? dangerColor : Color.white;
        }

        void Drop()
        {
            Vector2 at = AimPoint;
            CancelAim();
            Charges--;
            UsesLeft--;
            NutStrike.Launch(at, settings, gameObject, _ctx, walnutSprite);
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
            _aimRoot.SetParent(_ctx != null ? _ctx.Root : transform.parent, false);
            float d = settings.blastRadius * 2f;
            _range = Sprite(PixelArt.Ring(settings.maxRange * 2f, 1, dashed: true), rangeColor, 2);
            _fill = Sprite(PixelArt.DitherDisc(d), aimColor, 3);
            _ring = Sprite(PixelArt.Ring(d, 2), aimColor, 4);
            _center = Sprite(walnutSprite != null ? walnutSprite : PixelArt.AimEnd, Color.white, 5);
            if (walnutSprite != null) _center.transform.localScale = Vector3.one * (0.7f / walnutSprite.bounds.size.x);
        }

        SpriteRenderer Sprite(Sprite sprite, Color color, int order)
        {
            var go = Prims.Create("Aim", _aimRoot, sprite, color, Vector3.zero, Vector2.one, order);
            return go.GetComponent<SpriteRenderer>();
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
