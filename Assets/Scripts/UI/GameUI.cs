// Весь интерфейс игры: главное меню, HUD (здоровье, уровень, враги, стрелка на Б, прицел),
// пауза, экраны «Уровень пройден» и «Кот подбит». Собирается кодом при старте.
// Повесь на любой объект в сцене. Спрайты художника назначаются в инспекторе, пустые поля = примитивы.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class GameUI : MonoBehaviour
    {
        [Header("Связи")]
        public GameFlow flow;                 // пусто = найдётся сам
        public bool showMainMenu = true;      // false — игра стартует сразу, меню только по паузе

        [Header("Тексты")]
        public string gameTitle = "Кот в танке";
        public string controlsHint = "WASD — ехать   Мышь — целиться   ЛКМ — огонь   Esc — пауза";

        [Header("Спрайты художника (пусто = примитивы)")]
        public Font font;
        public Sprite logo;                   // 1024×512
        public Sprite menuBackground;         // фон главного меню, 1920×1080, в исходных цветах; заполняет экран, лишнее обрезается
        public Sprite heartFull;              // 48×48
        public Sprite heartEmpty;
        public Sprite exitArrow;              // 48×48, смотрит вправо
        public Sprite crosshair;              // 32×32
        public Sprite buttonSprite;           // 320×80, лучше с 9-slice границами
        public Sprite panelSprite;

        [Header("Цвета")]
        public Color accent = new Color(1f, 0.65f, 0.25f);    // рыжий кот
        public Color buttonColor = new Color(0.22f, 0.24f, 0.28f, 0.95f);
        public Color textColor = Color.white;
        public Color dimColor = new Color(0f, 0f, 0f, 0.6f);
        public Color heartColor = new Color(0.95f, 0.3f, 0.35f);

        Font _font;
        RectTransform _canvasRect;
        GameObject _hud, _menu, _pause, _banner;
        RectTransform _hearts, _arrow, _crosshair;
        Text _levelText, _enemiesText, _bannerTitle, _bannerSub, _toastText;
        readonly List<Image> _heartImages = new List<Image>();
        float _toastUntil;

        // ------------------------------------------------------------------ Жизненный цикл

        void Awake()
        {
            if (flow == null)
            {
#if UNITY_2023_1_OR_NEWER
                flow = FindFirstObjectByType<GameFlow>();
#else
                flow = FindObjectOfType<GameFlow>();
#endif
            }
            if (flow == null) { Debug.LogError("[GameUI] В сцене нет GameFlow."); enabled = false; return; }
            if (showMainMenu) flow.autoStart = false; // ждём кнопку «Играть»

            _font = font != null ? font : UIFactory.DefaultFont();
            UIFactory.EnsureEventSystem();
            Build();

            // подписываемся в Awake: GameFlow может стартовать раньше нашего Start
            flow.OnStateChanged += HandleState;
            flow.OnLevelStarted += HandleLevelStarted;
            flow.OnLevelCompleted += HandleLevelCompleted;
            flow.OnPlayerKilled += HandlePlayerKilled;
            flow.OnPlayerHpChanged += SetHearts;
            flow.OnExitBlocked += HandleExitBlocked;
            flow.OnEnemyCountChanged += SetEnemyCount;
        }

        void Start()
        {
            HandleState(flow.State);
            if (flow.Current != null) HandleLevelStarted(flow.Current);
        }

        void OnDestroy()
        {
            Cursor.visible = true;
            if (flow == null) return;
            flow.OnStateChanged -= HandleState;
            flow.OnLevelStarted -= HandleLevelStarted;
            flow.OnLevelCompleted -= HandleLevelCompleted;
            flow.OnPlayerKilled -= HandlePlayerKilled;
            flow.OnPlayerHpChanged -= SetHearts;
            flow.OnExitBlocked -= HandleExitBlocked;
            flow.OnEnemyCountChanged -= SetEnemyCount;
        }

        void Update()
        {
            if (flow == null) return;
            var ctx = flow.Current;

            UpdateExitArrow(ctx);
            UpdateCrosshair();
            _toastText.gameObject.SetActive(Time.unscaledTime < _toastUntil);
        }

        // ------------------------------------------------------------------ Сборка

        void Build()
        {
            var canvas = UIFactory.CreateCanvas(transform, "GameCanvas", 100);
            _canvasRect = (RectTransform)canvas.transform;
            BuildHud(canvas.transform);
            BuildBanner(canvas.transform);
            BuildPause(canvas.transform);
            BuildMenu(canvas.transform);
            _crosshair = UIFactory.Rect(canvas.transform, "Crosshair", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36, 36));
            _crosshair.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(_crosshair, crosshair != null ? crosshair : Prims.Ring, crosshair != null ? Color.white : accent);
        }

        void BuildHud(Transform root)
        {
            _hud = UIFactory.Stretch(root, "HUD").gameObject;
            var t = _hud.transform;

            _hearts = UIFactory.Rect(t, "Hearts", new Vector2(0, 1), new Vector2(32, -28), new Vector2(400, 56));
            var row = _hearts.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 10;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            _levelText = UIFactory.Text(UIFactory.Rect(t, "Level", new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(400, 56)),
                                        _font, "Уровень 1", 40, TextAnchor.MiddleCenter, textColor);
            _enemiesText = UIFactory.Text(UIFactory.Rect(t, "Enemies", new Vector2(1, 1), new Vector2(-32, -24), new Vector2(400, 56)),
                                          _font, "Врагов: 0", 36, TextAnchor.MiddleRight, textColor);
            _toastText = UIFactory.Text(UIFactory.Rect(t, "Toast", new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(1200, 60)),
                                        _font, "", 36, TextAnchor.MiddleCenter, accent);

            _arrow = UIFactory.Rect(t, "ExitArrow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            _arrow.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(_arrow, exitArrow != null ? exitArrow : Prims.Triangle, exitArrow != null ? Color.white : new Color(1f, 0.85f, 0.2f));
        }

        void BuildBanner(Transform root)
        {
            var rt = UIFactory.Stretch(root, "Banner");
            _banner = rt.gameObject;
            var strip = UIFactory.Rect(rt, "Strip", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2200, 240));
            strip.pivot = new Vector2(0.5f, 0.5f);
            UIFactory.Image(strip, panelSprite != null ? panelSprite : Prims.Square, panelSprite != null ? Color.white : dimColor);
            _bannerTitle = UIFactory.Text(UIFactory.Rect(strip, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1600, 100)),
                                          _font, "", 72, TextAnchor.MiddleCenter, accent);
            _bannerTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _bannerSub = UIFactory.Text(UIFactory.Rect(strip, "Sub", new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(1600, 60)),
                                        _font, "", 36, TextAnchor.MiddleCenter, textColor);
            _bannerSub.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        void BuildPause(Transform root)
        {
            var rt = UIFactory.Stretch(root, "Pause");
            _pause = rt.gameObject;
            UIFactory.Image(rt, Prims.Square, dimColor, true); // блокирует клики по игре
            var title = UIFactory.Text(UIFactory.Rect(rt, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(800, 110)),
                                       _font, "Пауза", 80, TextAnchor.MiddleCenter, accent);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            MenuButton(rt, 60, "Продолжить", () => flow.Pause(false));
            MenuButton(rt, -50, "Заново", () => flow.RestartLevel());
            MenuButton(rt, -160, "В меню", () => flow.GoToMenu());
        }

        void BuildMenu(Transform root)
        {
            var rt = UIFactory.Stretch(root, "MainMenu");
            _menu = rt.gameObject;
            UIFactory.Image(rt, Prims.Square, new Color(0.12f, 0.13f, 0.15f, 1f), true);

            if (menuBackground != null)
            {
                // как background-size: cover — на любом соотношении сторон без полос и растяжения
                var bg = UIFactory.Rect(rt, "Background", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                UIFactory.Image(bg, menuBackground, Color.white);
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = menuBackground.rect.width / menuBackground.rect.height;
            }

            if (logo != null)
            {
                var l = UIFactory.Rect(rt, "Logo", new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1024, 512) * 0.7f);
                l.pivot = new Vector2(0.5f, 0.5f);
                UIFactory.Image(l, logo, Color.white).preserveAspect = true;
            }
            else
            {
                var t = UIFactory.Text(UIFactory.Rect(rt, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 240), new Vector2(1400, 160)),
                                       _font, gameTitle, 120, TextAnchor.MiddleCenter, accent);
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            MenuButton(rt, -10, "Играть", () => flow.StartNewGame());
            MenuButton(rt, -120, "Выход", () => flow.Quit());
            var hint = UIFactory.Text(UIFactory.Rect(rt, "Hint", new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(1600, 50)),
                                      _font, controlsHint, 30, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.6f));
            hint.rectTransform.pivot = new Vector2(0.5f, 0);
        }

        void MenuButton(Transform parent, float y, string label, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIFactory.Button(parent, label, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(360, 90),
                                     buttonSprite != null ? buttonSprite : Prims.Square,
                                     buttonSprite != null ? Color.white : buttonColor,
                                     _font, label, 40, textColor, onClick);
            ((RectTransform)b.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        // ------------------------------------------------------------------ Реакция на игру

        void HandleState(GameState s)
        {
            _menu.SetActive(s == GameState.Menu);
            _pause.SetActive(s == GameState.Paused);
            _hud.SetActive(s != GameState.Menu);
            _banner.SetActive(s == GameState.LevelComplete || s == GameState.PlayerDead);
            bool playing = s == GameState.Playing;
            _crosshair.gameObject.SetActive(playing);
            Cursor.visible = !playing; // в бою вместо курсора — прицел
        }

        void HandleLevelStarted(LevelContext ctx)
        {
            _levelText.text = "Уровень " + ctx.LevelNumber;
            SetEnemyCount(ctx.AliveEnemyCount());
            if (ctx.PlayerHealth != null) SetHearts(ctx.PlayerHealth.Hp, ctx.PlayerHealth.maxHp);
        }

        void HandleLevelCompleted(LevelContext ctx)
        {
            _bannerTitle.text = "Уровень " + ctx.LevelNumber + " пройден!";
            _bannerSub.text = "Телефоны в панике. Едем дальше...";
        }

        void HandlePlayerKilled(LevelContext ctx)
        {
            _bannerTitle.text = "Кота подбили!";
            _bannerSub.text = "Пробуем ещё раз...";
        }

        void HandleExitBlocked()
        {
            _toastText.text = "Сначала разберись со всеми телефонами!";
            _toastUntil = Time.unscaledTime + 1.5f;
        }

        void SetEnemyCount(int alive)
        {
            _enemiesText.text = "Врагов: " + alive;
        }

        void SetHearts(int hp, int max)
        {
            while (_heartImages.Count < max)
            {
                var rt = UIFactory.Rect(_hearts, "Heart", new Vector2(0, 0.5f), Vector2.zero, new Vector2(48, 48));
                _heartImages.Add(UIFactory.Image(rt, heartFull != null ? heartFull : Prims.Circle, Color.white));
            }
            for (int i = 0; i < _heartImages.Count; i++)
            {
                var img = _heartImages[i];
                img.gameObject.SetActive(i < max);
                bool full = i < hp;
                if (heartFull != null)
                {
                    img.sprite = full || heartEmpty == null ? heartFull : heartEmpty;
                    img.color = full || heartEmpty != null ? Color.white : new Color(1, 1, 1, 0.25f);
                }
                else
                {
                    img.color = full ? heartColor : new Color(0.3f, 0.3f, 0.3f, 0.6f);
                }
            }
        }

        // ------------------------------------------------------------------ Стрелка на Б и прицел

        void UpdateExitArrow(LevelContext ctx)
        {
            var cam = Camera.main;
            bool show = _hud.activeSelf && ctx != null && ctx.Exit != null && cam != null;
            if (show)
            {
                Vector3 vp = cam.WorldToViewportPoint(ctx.Exit.position);
                Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                bool onScreen = Mathf.Abs(d.x) < 0.47f && Mathf.Abs(d.y) < 0.47f;
                show = !onScreen && d.sqrMagnitude > 0.0001f;
                if (show)
                {
                    // прижимаем к краю экрана по направлению на выход
                    float k = 0.44f / Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));
                    Vector2 p = new Vector2(0.5f, 0.5f) + d * k;
                    _arrow.anchorMin = _arrow.anchorMax = p;
                    _arrow.anchoredPosition = Vector2.zero;
                    float angle = Mathf.Atan2(d.y * Screen.height, d.x * Screen.width) * Mathf.Rad2Deg;
                    _arrow.localRotation = Quaternion.Euler(0, 0, angle);
                }
            }
            _arrow.gameObject.SetActive(show);
        }

        void UpdateCrosshair()
        {
            if (!_crosshair.gameObject.activeSelf) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, GameInput.MouseScreen(), null, out var local))
                _crosshair.anchoredPosition = local;
        }
    }
}
