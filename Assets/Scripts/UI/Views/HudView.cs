// HUD в бою: сердечки, номер уровня, счётчик врагов, боезапас, всплывающая подсказка, стрелка на выход и прицел.
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

        [Header("Ульта «Орех»")]
        public RectTransform ultRoot;
        public Image ultIcon;
        public Text ultKey;
        public Image[] ultPips;               // заряды
        public RectTransform ultBarFill;      // полоска до следующего заряда (anchorMax.x = прогресс)
        public Color ultPipOn = new Color(1f, 0.8f, 0.3f);
        public Color ultPipOff = new Color(0f, 0f, 0f, 0.45f);
        public Color ultIconInactive = new Color(1f, 1f, 1f, 0.4f);

        [Header("Боезапас (снаряды основного орудия)")]
        public RectTransform ammoRoot;
        public Image[] ammoPips;              // по иконке на снаряд; лишние прячутся
        public RectTransform ammoReloadFill;  // полоска перезарядки (anchorMax.x = готовность)
        public Color ammoOn = Color.white;
        public Color ammoOff = new Color(0.15f, 0.12f, 0.2f, 0.55f);
        public Color reloadColor = new Color(1f, 0.8f, 0.3f);
        public Color readyColor = Color.white;
        [Tooltip("Прицел, пока выстрел не готов: прозрачность и масштаб")]
        [Range(0f, 1f)] public float crosshairNotReadyAlpha = 0.35f;
        [Range(0.3f, 1f)] public float crosshairNotReadyScale = 0.75f;

        [Header("Звёзды уровня")]
        public Image[] liveStars;             // состояние звёзд прямо сейчас
        public Text starsTotal;               // звёзд за забег
        public string starsTotalFormat = "Stars: {0}";
        public Color starOn = new Color32(255, 196, 56, 255);
        public Color starPossible = new Color(1f, 1f, 1f, 0.6f);
        public Color starOff = new Color(0.35f, 0.3f, 0.4f, 0.75f);

        [Header("Магазин пулемёта")]
        public RectTransform minigunRoot;
        public Image[] minigunPips;           // патрон на каждый выстрел магазина
        public RectTransform minigunReloadFill;
        public Color minigunOn = new Color(0.95f, 0.55f, 0.25f);
        public Color minigunOff = new Color(0.15f, 0.12f, 0.2f, 0.55f);

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

        // ----- боезапас -----
        int _ammoShown = -1;
        bool _canFire = true;
        float[] _pipPop;                      // «подпрыгивание» иконки, когда снаряд вернулся
        float _crosshairPop;
        Image _crosshairImage;
        Color _crosshairColor;

        /// <summary>Боезапас: left из max готовы, reload01 — перезарядка, canFire — ЛКМ выстрелит прямо сейчас.</summary>
        public void SetAmmo(bool visible, int left, int max, float reload01, bool canFire)
        {
            if (ammoRoot == null) return;
            ammoRoot.gameObject.SetActive(visible);
            if (!visible) { SetCrosshairReady(true); return; }
            if (_pipPop == null || _pipPop.Length != ammoPips.Length) _pipPop = new float[ammoPips.Length];
            for (int i = 0; i < ammoPips.Length; i++)
            {
                ammoPips[i].gameObject.SetActive(i < max);
                ammoPips[i].color = i < left ? ammoOn : ammoOff;
                if (_ammoShown >= 0 && i < left && i >= _ammoShown) _pipPop[i] = 1f; // снаряд вернулся — иконка подпрыгивает
            }
            _ammoShown = left;
            if (ammoReloadFill != null)
            {
                ammoReloadFill.anchorMax = new Vector2(Mathf.Clamp01(reload01), 1f);
                var img = ammoReloadFill.GetComponent<Image>();
                if (img != null) img.color = reload01 >= 1f ? readyColor : reloadColor;
            }
            SetCrosshairReady(canFire);
        }

        /// <summary>Магазин пулемёта: left из max патронов; во время перезарядки патроны «наполняются» по reload01.</summary>
        public void SetMinigun(bool visible, int left, int max, float reload01, bool reloading)
        {
            if (minigunRoot == null) return;
            minigunRoot.gameObject.SetActive(visible);
            if (!visible) return;
            int filled = reloading ? Mathf.FloorToInt(reload01 * max) : left; // при перезарядке патроны заполняются по одному
            for (int i = 0; i < minigunPips.Length; i++)
            {
                minigunPips[i].gameObject.SetActive(i < max);
                var c = i < filled ? minigunOn : minigunOff;
                if (reloading && i < filled) c.a *= 0.6f; // ещё не готовы — бледнее
                minigunPips[i].color = c;
            }
            if (minigunReloadFill != null)
            {
                minigunReloadFill.parent.gameObject.SetActive(reloading);
                minigunReloadFill.anchorMax = new Vector2(Mathf.Clamp01(reload01), 1f);
            }
        }

        StarState[] _starStates;
        float[] _starAnim;                    // >0 — звезда «прыгает» (заработана), <0 — дрожит (потеряна)
        int _shownTotal = -1;

        /// <summary>Звёзды уровня (null — спрятать) и счёт за забег. Смена состояния звезды анимируется.</summary>
        public void SetStars(LevelStars stars, int total)
        {
            if (starsTotal != null && total != _shownTotal) { _shownTotal = total; starsTotal.text = string.Format(starsTotalFormat, total); }
            if (liveStars == null) return;
            if (_starStates == null || _starStates.Length != liveStars.Length)
            {
                _starStates = new StarState[liveStars.Length];
                _starAnim = new float[liveStars.Length];
            }
            for (int i = 0; i < liveStars.Length; i++)
            {
                bool used = stars != null && i < stars.Rules.Length;
                liveStars[i].gameObject.SetActive(used);
                if (!used) continue;
                var s = stars.State(i);
                if (s != _starStates[i]) _starAnim[i] = s == StarState.Earned ? 1f : s == StarState.Lost ? -1f : 0f;
                _starStates[i] = s;
                liveStars[i].color = s == StarState.Earned ? starOn : s == StarState.Lost ? starOff : starPossible;
            }
        }

        /// <summary>Новый уровень — без анимации смены состояний.</summary>
        public void ResetStars()
        {
            _starStates = null;
            if (liveStars != null) foreach (var s in liveStars) s.rectTransform.localScale = Vector3.one;
        }

        void AnimateStars()
        {
            if (_starAnim == null) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _starAnim.Length; i++)
            {
                float a = _starAnim[i];
                var rt = liveStars[i].rectTransform;
                if (a > 0f) rt.localScale = Vector3.one * (1f + 0.6f * a);                               // прыжок
                else if (a < 0f) rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(a * 40f) * 25f * -a); // дрожь
                else { rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity; }
                _starAnim[i] = Mathf.MoveTowards(a, 0f, dt * 2.5f);
            }
        }

        void SetCrosshairReady(bool ready)
        {
            if (ready && !_canFire) _crosshairPop = 1f; // выстрел снова готов — прицел «щёлкает»
            _canFire = ready;
        }

        void AnimateAmmo()
        {
            float dt = Time.unscaledDeltaTime;
            if (_pipPop != null)
                for (int i = 0; i < _pipPop.Length; i++)
                {
                    _pipPop[i] = Mathf.MoveTowards(_pipPop[i], 0f, dt * 6f);
                    ammoPips[i].rectTransform.localScale = Vector3.one * (1f + 0.35f * _pipPop[i]);
                }

            if (crosshair == null) return;
            if (_crosshairImage == null)
            {
                _crosshairImage = crosshair.GetComponent<Image>();
                if (_crosshairImage != null) _crosshairColor = _crosshairImage.color;
            }
            _crosshairPop = Mathf.MoveTowards(_crosshairPop, 0f, dt * 5f);
            float scale = (_canFire ? 1f : crosshairNotReadyScale) + 0.3f * _crosshairPop;
            crosshair.localScale = Vector3.one * scale;
            if (_crosshairImage != null)
            {
                var c = _crosshairColor;
                c.a *= _canFire ? 1f : crosshairNotReadyAlpha;
                _crosshairImage.color = c;
            }
        }

        bool _ultReady;

        /// <summary>Индикатор ульты. charges/max — заряды, progress — до следующего, usable — можно применить сейчас.</summary>
        public void SetUltimate(bool visible, int charges, int max, float progress, bool usable)
        {
            if (ultRoot == null) return;
            ultRoot.gameObject.SetActive(visible);
            if (!visible) return;
            for (int i = 0; i < ultPips.Length; i++)
            {
                ultPips[i].gameObject.SetActive(i < max);
                ultPips[i].color = i < charges ? ultPipOn : ultPipOff;
            }
            if (ultBarFill != null) ultBarFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            _ultReady = usable;
            ultIcon.color = usable ? Color.white : ultIconInactive;
            if (!usable) ultIcon.rectTransform.localScale = Vector3.one;
        }

        void Update()
        {
            if (toast.gameObject.activeSelf && Time.unscaledTime >= _toastUntil) toast.gameObject.SetActive(false);
            AnimateAmmo();
            AnimateStars();
            // готовая ульта «дышит» — заметно краем глаза
            if (_ultReady && ultIcon != null)
                ultIcon.rectTransform.localScale = Vector3.one * (1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)));
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
