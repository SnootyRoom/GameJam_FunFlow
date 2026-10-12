// Ульта «Орех»: иконка, заряды и полоска до следующего заряда. Готовая ульта «дышит».
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class UltWidget : MonoBehaviour, IHudWidget
    {
        public RectTransform root;
        public Image icon;
        public Text key;
        public Image[] pips;                  // заряды
        public RectTransform barFill;         // полоска до следующего заряда (anchorMax.x = прогресс)
        public Color pipOn = new Color(1f, 0.8f, 0.3f);
        public Color pipOff = new Color(0f, 0f, 0f, 0.45f);
        public Color iconInactive = new Color(1f, 1f, 1f, 0.4f);

        PlayerUltimate _ult;
        bool _ready;

        public void Bind(HudContext hud)
        {
            if (_ult != null) _ult.Changed -= Refresh;
            _ult = hud.Ultimate;
            if (_ult != null) _ult.Changed += Refresh;
            Refresh();
        }

        void OnDestroy() { if (_ult != null) _ult.Changed -= Refresh; }

        void Refresh()
        {
            bool visible = _ult != null;
            root.gameObject.SetActive(visible);
            if (!visible) { _ready = false; return; }
            int max = _ult.settings.maxCharges;
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].gameObject.SetActive(i < max);
                pips[i].color = i < _ult.Charges ? pipOn : pipOff;
            }
            if (barFill != null) barFill.anchorMax = new Vector2(Mathf.Clamp01(_ult.Progress01), 1f);
            _ready = _ult.CanActivate;
            icon.color = _ready ? Color.white : iconInactive;
            if (!_ready) icon.rectTransform.localScale = Vector3.one;
        }

        void Update()
        {
            // готовая ульта «дышит» — заметно краем глаза
            if (_ready && icon != null)
                icon.rectTransform.localScale = Vector3.one * (1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)));
        }
    }
}
