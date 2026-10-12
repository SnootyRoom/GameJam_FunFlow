// Чисто визуальная «жизнь» танка: отдача ствола, следы гусениц, пыль, дым при низком HP,
// кольцо готовой ульты, покачивание кота. На геймплей не влияет — можно смело тюнить.
using UnityEngine;

namespace CatTanks
{
    public class PlayerVisuals : MonoBehaviour
    {
        public float recoilDistance = 0.14f;
        public float trackSpacing = 0.22f;

        PlayerTank _tank;
        Transform _barrel, _cat, _ultRing;
        SpriteRenderer _ultRingSr;
        Vector3 _barrelRest, _catRest;
        float _recoil, _trackDist, _nextSmoke, _nextDust;
        Vector2 _lastPos;

        void Awake()
        {
            _tank = GetComponent<PlayerTank>();
            var turret = transform.Find("Turret");
            if (turret != null)
            {
                _barrel = turret.Find("Barrel");
                _cat = turret.Find("Cat");
            }
            if (_barrel != null) _barrelRest = _barrel.localPosition;
            if (_cat != null) _catRest = _cat.localScale;
            _lastPos = transform.position;

            var ring = Prims.Child("UltRing", transform, Prims.Ring, new Color(0.4f, 0.9f, 1f, 0f), Vector2.zero, Vector2.one, 9);
            ring.AddComponent<NoFlash>();
            var keep = ring.AddComponent<KeepWorldOffset>();
            keep.worldScale = Vector2.one * 1.5f;
            _ultRing = ring.transform;
            _ultRingSr = ring.GetComponent<SpriteRenderer>();
        }

        void Start()
        {
            if (_tank != null && _tank.Weapon != null) _tank.Weapon.Fired += OnFired;
        }

        void OnDestroy()
        {
            if (_tank != null && _tank.Weapon != null) _tank.Weapon.Fired -= OnFired;
        }

        void OnFired() { _recoil = 1f; }

        void Update()
        {
            float dt = Time.deltaTime;

            // отдача: ствол уходит назад и пружинит обратно
            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 7f);
            if (_barrel != null) _barrel.localPosition = _barrelRest - Vector3.right * (recoilDistance * _recoil * _recoil);
            if (_cat != null)
            {
                float squash = 1f + 0.12f * _recoil;
                _cat.localScale = new Vector3(_catRest.x * squash, _catRest.y / squash, 1f);
            }

            // следы гусениц и пыль из-под них
            Vector2 pos = transform.position;
            _trackDist += Vector2.Distance(pos, _lastPos);
            _lastPos = pos;
            if (_trackDist >= trackSpacing)
            {
                _trackDist = 0f;
                float ang = transform.eulerAngles.z;
                Vector2 side = transform.up * 0.36f;
                Fx.TrackMark(pos + side, ang);
                Fx.TrackMark(pos - side, ang);
                if (Time.time >= _nextDust)
                {
                    _nextDust = Time.time + 0.12f;
                    Fx.Dust(pos - (Vector2)transform.right * 0.45f + (Random.value < 0.5f ? side : -side));
                }
            }

            // при 1 HP танк дымит — понятно без взгляда на HUD
            var hp = _tank != null ? _tank.Health : null;
            if (hp != null && !hp.IsDead && hp.Hp <= 1 && hp.maxHp > 1 && Time.time >= _nextSmoke)
            {
                _nextSmoke = Time.time + 0.12f;
                Fx.Smoke(pos + Random.insideUnitCircle * 0.2f);
            }

            // кольцо вокруг танка, пока ульта готова
            var ult = _tank != null ? _tank.Ultimate : null;
            bool ready = ult != null && ult.CanActivate && Game.IsPlaying;
            float a = ready ? 0.35f + 0.25f * Mathf.Sin(Time.time * 6f) : 0f;
            _ultRingSr.color = Prims.WithAlpha(_ultRingSr.color, Mathf.MoveTowards(_ultRingSr.color.a, a, dt * 3f));
        }
    }
}
