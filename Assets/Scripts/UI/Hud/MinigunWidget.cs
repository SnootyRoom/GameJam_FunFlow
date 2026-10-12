// Магазин пулемёта: патрон на каждый выстрел; при перезарядке патроны бледно заполняются по одному, рядом полоска.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class MinigunWidget : MonoBehaviour, IHudWidget
    {
        public RectTransform root;
        public Image[] pips;
        public RectTransform reloadFill;      // видна только во время перезарядки
        public Color roundOn = new Color(0.95f, 0.55f, 0.25f);
        public Color roundOff = new Color(0.15f, 0.12f, 0.2f, 0.55f);

        PlayerTank _tank;

        public void Bind(HudContext hud)
        {
            _tank = hud.Tank;
            root.gameObject.SetActive(Visible);
        }

        bool Visible => _tank != null && _tank.minigunEnabled && _tank.Minigun != null;

        void Update()
        {
            if (!Visible) { if (root.gameObject.activeSelf) root.gameObject.SetActive(false); return; }
            var mag = _tank.Minigun;
            int max = mag.Size;
            int filled = mag.Reloading ? Mathf.FloorToInt(mag.Reload01 * max) : mag.Rounds;
            for (int i = 0; i < pips.Length; i++)
            {
                pips[i].gameObject.SetActive(i < max);
                var c = i < filled ? roundOn : roundOff;
                if (mag.Reloading && i < filled) c.a *= 0.6f; // ещё не готовы — бледнее
                pips[i].color = c;
            }
            if (reloadFill != null)
            {
                reloadFill.parent.gameObject.SetActive(mag.Reloading);
                reloadFill.anchorMax = new Vector2(mag.Reload01, 1f);
            }
        }
    }
}
