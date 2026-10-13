// Здоровье кота: иконка (hp_icon) и полоска из сегментов (hp_frame + hp_segment_*), один сегмент = одно HP.
// Сколько сегментов — решает Health.maxHp игрока. Подписывается на Health игрока сам.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class PlayerHpWidget : MonoBehaviour, IHudWidget
    {
        public RectTransform root;
        public SegmentBar bar;
        public Image icon;

        Health _health;

        public void Bind(HudContext hud)
        {
            if (_health != null) _health.OnHpChanged -= OnHp;
            _health = hud.Level != null ? hud.Level.PlayerHealth : null;
            root.gameObject.SetActive(_health != null);
            if (_health == null) return;
            _health.OnHpChanged += OnHp;
            bar.Set(_health.Hp, _health.maxHp, instant: true);
        }

        void OnHp(int hp, int max) => bar.Set(hp, max);

        void OnDestroy() { if (_health != null) _health.OnHpChanged -= OnHp; }
    }
}
