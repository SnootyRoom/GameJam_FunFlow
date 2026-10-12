// Сердечки здоровья кота. Подписывается на Health игрока сам.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class HeartsWidget : MonoBehaviour, IHudWidget
    {
        public RectTransform row;             // ряд сердец (HorizontalLayoutGroup)
        public Image heartTemplate;           // выключенный образец сердца — копируется по числу жизней
        public Sprite heartFull;
        [Tooltip("Пусто = полное сердце полупрозрачным")]
        public Sprite heartEmpty;
        [Tooltip("Цвета, если у сердец нет своих картинок")]
        public Color heartColor = new Color(0.95f, 0.3f, 0.35f);
        public Color heartEmptyColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);

        readonly List<Image> _hearts = new List<Image>();
        Health _health;

        public void Bind(HudContext hud)
        {
            if (_health != null) _health.OnHpChanged -= Set;
            _health = hud.Level != null ? hud.Level.PlayerHealth : null;
            if (_health == null) return;
            _health.OnHpChanged += Set;
            Set(_health.Hp, _health.maxHp);
        }

        void OnDestroy() { if (_health != null) _health.OnHpChanged -= Set; }

        public void Set(int hp, int max)
        {
            while (_hearts.Count < max)
            {
                var img = Instantiate(heartTemplate, row);
                img.name = "Heart";
                _hearts.Add(img);
            }
            for (int i = 0; i < _hearts.Count; i++)
            {
                var img = _hearts[i];
                img.gameObject.SetActive(i < max);
                bool full = i < hp;
                if (heartFull != null)
                {
                    img.sprite = full || heartEmpty == null ? heartFull : heartEmpty;
                    img.color = full || heartEmpty != null ? Color.white : new Color(1, 1, 1, 0.25f);
                }
                else img.color = full ? heartColor : heartEmptyColor;
            }
        }
    }
}
