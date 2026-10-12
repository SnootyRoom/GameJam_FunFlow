// Башня смотрит на курсор, ЛКМ/пробел — выстрел.
// Магазин: magazineSize снарядов, потом перезарядка reloadTime секунд (сама, когда кончились, или по R).
// Дополнительно — лимит снарядов в полёте (maxBulletsAlive) и пауза между выстрелами (fireCooldown).
using System;
using UnityEngine;

namespace CatTanks
{
    public class PlayerWeapon : MonoBehaviour
    {
        public Transform turret;
        public float muzzleDistance = 0.6f;
        public GameObject bulletPrefab;
        public Color bulletColor = new Color(1f, 0.85f, 0.35f);

        [Header("Заполняется из PlayerStats")]
        public float bulletSpeed = 8f;
        public int maxBounces = 1;
        public float fireCooldown = 0.3f;
        public int maxBulletsAlive = 5;
        public int magazineSize = 5;
        public float reloadTime = 1.6f;
        public int damage = 2;
        public float bulletRadius = Bullet.DefaultRadius;
        public float spreadDegrees;
        public float splashRadius;
        public int splashDamage;

        public event Action Fired;
        public event Action ReloadStarted;
        public event Action ReloadFinished;

        PlayerTank _tank;
        float _nextFire, _reloadStart, _reloadEnd;

        public int Ammo { get; private set; }
        public bool IsReloading => _reloadEnd > 0f;
        /// <summary>Прогресс перезарядки 0..1 (1 — не перезаряжается).</summary>
        public float Reload01 => IsReloading ? Mathf.Clamp01((Time.time - _reloadStart) / Mathf.Max(0.01f, _reloadEnd - _reloadStart)) : 1f;

        public Vector2 AimDirection => turret != null ? (Vector2)turret.right : (Vector2)transform.right;
        public Vector2 Muzzle => (Vector2)transform.position + AimDirection * muzzleDistance;
        public float BulletRadius => bulletRadius;
        public int BulletsInFlight => Bullet.CountOwnedBy(gameObject);
        /// <summary>Сколько выстрелов можно сделать прямо сейчас (магазин с учётом лимита в полёте).</summary>
        public int BulletsReady => IsReloading ? 0 : Mathf.Min(Ammo, Mathf.Max(0, maxBulletsAlive - BulletsInFlight));
        public bool CanFire => !IsReloading && Ammo > 0 && Time.time >= _nextFire && BulletsInFlight < maxBulletsAlive;
        /// <summary>0 — только что выстрелил, 1 — пауза между выстрелами прошла.</summary>
        public float Cooldown01 => fireCooldown <= 0 ? 1f : Mathf.Clamp01(1f - (_nextFire - Time.time) / fireCooldown);

        void Awake()
        {
            _tank = GetComponent<PlayerTank>();
            if (turret == null) turret = transform.Find("Turret");
        }

        public void Apply(PlayerStats s)
        {
            bulletSpeed = s.bulletSpeed;
            maxBounces = s.maxBounces;
            fireCooldown = s.fireCooldown;
            maxBulletsAlive = s.maxBulletsAlive;
            magazineSize = s.magazineSize;
            reloadTime = s.reloadTime;
            damage = s.bulletDamage;
            bulletRadius = s.bulletRadius;
            spreadDegrees = s.spreadDegrees;
            splashRadius = s.splashRadius;
            splashDamage = s.splashDamage;
            Ammo = magazineSize;
            _reloadEnd = 0f;
        }

        void Update()
        {
            // перезарядка идёт и при открытом прицеле ульты, и пока игрок не жмёт ничего
            if (IsReloading && Time.time >= _reloadEnd)
            {
                _reloadEnd = 0f;
                Ammo = magazineSize;
                ReloadFinished?.Invoke();
                GameEvents.RaisePlayerReloadFinished();
            }

            if (_tank != null && !_tank.InputEnabled) return;

            if (turret != null)
            {
                Vector2 aim = GameInput.MouseWorld() - (Vector2)transform.position;
                if (aim.sqrMagnitude > 0.001f)
                {
                    // башня догоняет курсор очень быстро, но не мгновенно — читается как механизм
                    float want = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                    float cur = turret.eulerAngles.z;
                    turret.rotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(cur, want, 1f - Mathf.Exp(-30f * Time.deltaTime)));
                }
            }

            bool aimingUlt = _tank != null && _tank.Ultimate != null && _tank.Ultimate.IsAiming;
            if (GameInput.ReloadPressed() && !IsReloading && Ammo < magazineSize) StartReload();

            if (!aimingUlt && GameInput.FirePressed())
            {
                if (CanFire) Fire();
                else GameEvents.RaisePlayerFireBlocked();
            }
        }

        public void Fire()
        {
            _nextFire = Time.time + fireCooldown;
            Ammo--;
            Vector2 dir = spreadDegrees > 0f ? (Vector2)(Quaternion.Euler(0, 0, UnityEngine.Random.Range(-spreadDegrees, spreadDegrees)) * AimDirection) : AimDirection;
            var b = Bullet.Spawn(Muzzle, dir, gameObject, Team.Player, bulletColor, bulletSpeed, maxBounces, bulletPrefab, damage, bulletRadius);
            if (splashRadius > 0f) { b.splashRadius = splashRadius; b.splashDamage = splashDamage; }
            Fired?.Invoke();
            if (Ammo <= 0) StartReload();
        }

        public void StartReload()
        {
            if (IsReloading) return;
            _reloadStart = Time.time;
            _reloadEnd = Time.time + reloadTime;
            ReloadStarted?.Invoke();
            GameEvents.RaisePlayerReloadStarted();
        }
    }
}
