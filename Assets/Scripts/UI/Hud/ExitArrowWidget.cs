// Стрелка у края экрана в сторону рыбы (выхода); прячется, когда выход виден.
using UnityEngine;

namespace CatTanks.UI
{
    public class ExitArrowWidget : MonoBehaviour, IHudWidget
    {
        public RectTransform arrow;

        Transform _exit;

        public void Bind(HudContext hud) { _exit = hud.Level != null ? hud.Level.Exit : null; }

        void LateUpdate()
        {
            var cam = Camera.main;
            bool show = cam != null && _exit != null;
            if (show)
            {
                Vector3 vp = cam.WorldToViewportPoint(_exit.position);
                Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                bool onScreen = Mathf.Abs(d.x) < 0.47f && Mathf.Abs(d.y) < 0.47f;
                show = !onScreen && d.sqrMagnitude > 0.0001f;
                if (show)
                {
                    // прижимаем к краю экрана по направлению на выход
                    float k = 0.44f / Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
                    Vector2 p = new Vector2(0.5f, 0.5f) + d * k;
                    arrow.anchorMin = arrow.anchorMax = p;
                    arrow.anchoredPosition = Vector2.zero;
                    float angle = Mathf.Atan2(d.y * Screen.height, d.x * Screen.width) * Mathf.Rad2Deg;
                    arrow.localRotation = Quaternion.Euler(0, 0, angle);
                }
            }
            if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
        }
    }
}
