// Боезапас основного орудия: иконка на каждый снаряд (летящие гаснут) и полоска перезарядки.
// Вернувшийся снаряд «подпрыгивает».
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class AmmoWidget : MonoBehaviour, IHudWidget
    {
        public RectTransform root;
        public Image[] pips;                  // по иконке на снаряд; лишние прячутся
        public RectTransform reloadFill;      // anchorMax.x = готовность
        public Color ammoOn = Color.white;
        public Color ammoOff = new Color(0.15f, 0.12f, 0.2f, 0.55f);
        public Color reloadColor = new Color(1f, 0.8f, 0.3f);
        public Color readyColor = Color.white;

        PlayerTank _tank;
        Image _reloadImage;
        float[] _pop;
        int _shown = -1;

        public void Bind(HudContext hud)
        {
            _tank = hud.Tank;
            _shown = -1;
            root.gameObject.SetActive(_tank != null);
        }

        void Update()
        {
            if (_tank == null) { if (root.gameObject.activeSelf) root.gameObject.SetActive(false); return; }
            if (_pop == null || _pop.Length != pips.Length) _pop = new float[pips.Length];

            int left = _tank.AmmoLeft, max = _tank.AmmoMax;
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].gameObject.SetActive(i < max);
                pips[i].color = i < left ? ammoOn : ammoOff;
                if (_shown >= 0 && i < left && i >= _shown) _pop[i] = 1f; // снаряд вернулся — иконка подпрыгивает
                _pop[i] = Mathf.MoveTowards(_pop[i], 0f, Time.unscaledDeltaTime * 6f);
                pips[i].rectTransform.localScale = Vector3.one * (1f + 0.35f * _pop[i]);
            }
            _shown = left;

            if (reloadFill == null) return;
            float reload = _tank.Reload01;
            reloadFill.anchorMax = new Vector2(reload, 1f);
            if (_reloadImage == null) _reloadImage = reloadFill.GetComponent<Image>();
            if (_reloadImage != null) _reloadImage.color = reload >= 1f ? readyColor : reloadColor;
        }
    }
}
