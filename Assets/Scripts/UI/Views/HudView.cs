// HUD в бою: сердечки, номер уровня, счётчик врагов, всплывающая подсказка, стрелка на выход и прицел.
// Лежит на корне префаба Hud. Только показывает — что показать, решает GameUI.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class HudView : MonoBehaviour
    {
        [Header("Элементы")]
        public RectTransform hearts;          // ряд сердец (HorizontalLayoutGroup)
        public Image heartTemplate;           // выключенный образец сердца — копируется по числу жизней
        public Text level;
        public Text enemies;
        public Text toast;
        public RectTransform exitArrow;
        public RectTransform crosshair;

        [Header("Сердца")]
        public Sprite heartFull;
        [Tooltip("Пусто = полное сердце полупрозрачным")]
        public Sprite heartEmpty;
        [Tooltip("Цвета, если у сердец нет своих картинок")]
        public Color heartColor = new Color(0.95f, 0.3f, 0.35f);
        public Color heartEmptyColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);

        [Header("Тексты ({0} — число)")]
        public string levelFormat = "Level {0}";
        public string enemiesFormat = "Enemies: {0}";

        readonly List<Image> _hearts = new List<Image>();
        float _toastUntil;

        public void SetLevel(int n) => level.text = string.Format(levelFormat, n);
        public void SetEnemies(int n) => enemies.text = string.Format(enemiesFormat, n);

        public void ShowToast(string text, float seconds)
        {
            toast.text = text;
            _toastUntil = Time.unscaledTime + seconds;
            toast.gameObject.SetActive(true);
        }

        public void SetHearts(int hp, int max)
        {
            while (_hearts.Count < max)
            {
                var img = Instantiate(heartTemplate, hearts);
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

        void Update()
        {
            if (toast.gameObject.activeSelf && Time.unscaledTime >= _toastUntil) toast.gameObject.SetActive(false);
        }

        /// <summary>Стрелка у края экрана в сторону выхода; прячется, когда выход виден.</summary>
        public void UpdateExitArrow(Camera cam, Transform exit)
        {
            bool show = cam != null && exit != null;
            if (show)
            {
                Vector3 vp = cam.WorldToViewportPoint(exit.position);
                Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                bool onScreen = Mathf.Abs(d.x) < 0.47f && Mathf.Abs(d.y) < 0.47f;
                show = !onScreen && d.sqrMagnitude > 0.0001f;
                if (show)
                {
                    // прижимаем к краю экрана по направлению на выход
                    float k = 0.44f / Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
                    Vector2 p = new Vector2(0.5f, 0.5f) + d * k;
                    exitArrow.anchorMin = exitArrow.anchorMax = p;
                    exitArrow.anchoredPosition = Vector2.zero;
                    float angle = Mathf.Atan2(d.y * Screen.height, d.x * Screen.width) * Mathf.Rad2Deg;
                    exitArrow.localRotation = Quaternion.Euler(0, 0, angle);
                }
            }
            exitArrow.gameObject.SetActive(show);
        }

        public void SetCrosshairVisible(bool visible) => crosshair.gameObject.SetActive(visible);

        /// <summary>Прицел — под курсором мыши (сам курсор в бою скрыт).</summary>
        public void UpdateCrosshair(Vector2 mouseScreen)
        {
            if (!crosshair.gameObject.activeSelf) return;
            var area = (RectTransform)crosshair.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, mouseScreen, null, out var local))
                crosshair.anchoredPosition = local;
        }
    }
}
