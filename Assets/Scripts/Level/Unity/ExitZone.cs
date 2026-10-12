// Рыбка — цель уровня: когда кот въезжает в триггер, уровень пройден.
// Если в GameConfig включено requireAllEnemiesDead — сначала нужно зачистить арену (рыбка серая).
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Collider2D))]
    public class ExitZone : MonoBehaviour
    {
        /// <summary>Открыта, если не нужно зачищать арену и нет живого босса (босс стережёт рыбку).</summary>
        public bool IsOpen => (!Game.Config.requireAllEnemiesDead || EnemyRegistry.Count == 0) && EnemyRegistry.Boss == null;

        Transform _icon, _glow;
        SpriteRenderer _iconSr, _glowSr;
        Color _iconColor, _glowColor;
        Vector3 _iconScale, _glowScale;
        bool _wasOpen = true;

        void Reset() { GetComponent<Collider2D>().isTrigger = true; }

        void Awake()
        {
            _icon = transform.Find("Icon");
            _glow = transform.Find("Glow");
            if (_icon != null) { _iconSr = _icon.GetComponent<SpriteRenderer>(); _iconColor = _iconSr.color; _iconScale = _icon.localScale; }
            if (_glow != null) { _glowSr = _glow.GetComponent<SpriteRenderer>(); _glowColor = _glowSr.color; _glowScale = _glow.localScale; }
        }

        void Update()
        {
            bool open = IsOpen;
            float t = Time.time;
            if (_icon != null)
            {
                _icon.localPosition = new Vector3(0f, 0.12f * Mathf.Sin(t * 2.5f), 0f);
                _icon.localRotation = Quaternion.Euler(0, 0, 8f * Mathf.Sin(t * 1.7f));
                _icon.localScale = _iconScale * (1f + 0.05f * Mathf.Sin(t * 5f));
                _iconSr.color = open ? _iconColor : new Color(0.55f, 0.55f, 0.6f, 0.8f);
            }
            if (_glow != null)
            {
                _glow.localScale = _glowScale * (1f + 0.12f * Mathf.Sin(t * 3f));
                _glowSr.color = open ? _glowColor : Prims.WithAlpha(_glowColor, 0.1f);
            }
            if (open && !_wasOpen)
            {
                Fx.Pulse(transform.position, 2f, new Color(1f, 0.85f, 0.3f, 0.8f), 0.5f);
                Fx.Text((Vector2)transform.position + Vector2.up, "Рыбка открыта!", new Color(1f, 0.85f, 0.3f));
                Sfx.Play(SfxId.UltReady, 0.7f, 0f);
            }
            _wasOpen = open;
        }

        // Stay, а не Enter: кот мог заехать до того, как убили последнего врага
        void OnTriggerStay2D(Collider2D other)
        {
            if (!Game.IsPlaying || other.GetComponentInParent<PlayerTank>() == null) return;
            if (!IsOpen) { GameEvents.RaiseExitLocked(); return; }
            if (Game.Manager != null) Game.Manager.WinLevel();
        }
    }
}
