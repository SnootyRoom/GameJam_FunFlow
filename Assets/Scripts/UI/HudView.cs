// Игровой HUD на uGUI (собирается кодом): сердечки, ульта, уровень/время/враги/рыбки, прицел с патронами
// и перезарядкой, стрелка к рыбке за краем экрана, баннер уровня, тосты, всплывающие тексты, виньетка урона,
// обучение на первом уровне. Данные только читает (Game.*, EnemyRegistry) и слушает GameEvents.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks
{
    public class HudView : MonoBehaviour
    {
        CanvasGroup _hud;

        // сердечки
        RectTransform _heartsRow;
        readonly List<Image> _hearts = new List<Image>();
        readonly List<float> _heartPunch = new List<float>();
        int _shownHp = -1, _shownMax = -1;

        // инфо справа
        Text _levelText, _timerText, _enemiesText, _fishText, _scoreText;
        float _shownScore;

        // комбо
        CanvasGroup _comboGroup;
        RectTransform _comboRoot, _comboFill;
        Text _comboText, _comboMult;
        float _comboPunch;

        // босс
        CanvasGroup _bossGroup;
        RectTransform _bossFill, _bossTrail;
        Text _bossName;
        Enemy _boss;
        float _bossShake;
        float _fishPunch;
        int _lastFish = -1;

        // ульта
        UltBadge _ult;

        // прицел
        RectTransform _cross;
        Image _crossRing, _crossDot, _crossReload;
        RectTransform _ammoRow;
        readonly List<Image> _ammoPips = new List<Image>();
        float _crossKick;

        // стрелка к рыбке
        RectTransform _arrow, _arrowTri;
        Image _arrowTriImg, _arrowFishImg;

        // виньетка, баннер, тосты, всплывающие тексты
        Image _vignette;
        float _hurtFlash;
        CanvasGroup _banner;
        Text _bannerTitle, _bannerSub;
        float _bannerT = 99f;
        RectTransform _toastRoot, _popupRoot;

        class Toast { public CanvasGroup cg; public float t, life; }
        class Popup { public Text text; public Vector2 world; public float t; }
        readonly List<Toast> _toasts = new List<Toast>();
        readonly List<Popup> _popups = new List<Popup>();

        // обучение
        readonly List<KeyValuePair<float, string>> _tutorial = new List<KeyValuePair<float, string>>();
        float _levelClock;

        static readonly string[] TutorialSteps =
        {
            "<b>WASD</b> — ехать",
            "<b>Мышь</b> — прицел, <b>ЛКМ / пробел</b> — выстрел",
            "Снаряды <b>отскакивают</b> от стен — пунктир показывает путь. <color=#FF5C52>Красный</color> = попадёшь в себя!",
            "Доберись до <color=#FFCC40><b>рыбки</b></color> — стрелка у края экрана покажет, где она",
            "<b>R</b> — перезарядка (начнётся сама, когда снаряды кончатся)",
            "Убивай подряд — растёт <color=#FF9E38><b>комбо</b></color> и множитель очков. Рыбки с врагов летят к коту сами",
            "За убийства копится <color=#FFBD47><b>орех</b></color>: зажми <b>ПКМ / Q</b> — прицел, отпусти — сброс. Рушит стены!",
        };

        // ------------------------------------------------------------------ Сборка

        void Awake()
        {
            UiKit.EnsureEventSystem();
            var canvas = UiKit.MakeCanvas("HUD", transform, 10);
            var root = canvas.transform;

            _vignette = UiKit.Img(root, UiKit.Vignette, new Color(1f, 0.15f, 0.1f, 0f), "Vignette");
            _vignette.preserveAspect = false;
            _vignette.Rt().Stretch();

            var hudNode = UiKit.Node("Hud", root).Stretch();
            _hud = hudNode.gameObject.AddComponent<CanvasGroup>();
            _hud.blocksRaycasts = false;

            BuildHearts(hudNode);
            BuildInfo(hudNode);
            _ult = UltBadge.Create(hudNode);
            BuildArrow(hudNode);

            _popupRoot = UiKit.Node("Popups", hudNode).Stretch();
            _toastRoot = UiKit.Node("Toasts", hudNode).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(1000, 500));
            var tv = UiKit.VStack(_toastRoot, 10, 0, TextAnchor.UpperCenter);
            tv.childForceExpandWidth = false;

            BuildBanner(hudNode);
            BuildCombo(hudNode);
            BuildBossBar(hudNode);
            BuildCrosshair(root); // поверх всего, кроме экранов меню
        }

        void BuildHearts(RectTransform parent)
        {
            var panel = UiKit.Panel(parent, new Color(0.07f, 0.08f, 0.12f, 0.6f), "HeartsPanel");
            panel.Rt().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(100, 76));
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiKit.HStack(panel, 6, 14, TextAnchor.MiddleLeft);
            _heartsRow = panel.Rt();
        }

        void BuildInfo(RectTransform parent)
        {
            var panel = UiKit.Panel(parent, new Color(0.07f, 0.08f, 0.12f, 0.6f), "InfoPanel");
            panel.Rt().Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(100, 76));
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var h = UiKit.HStack(panel, 26, 14, TextAnchor.MiddleRight);
            h.padding = new RectOffset(26, 26, 10, 10);

            _scoreText = InfoItem(panel.transform, null, UiKit.TextColor);
            _levelText = InfoItem(panel.transform, null, UiKit.Accent);
            _timerText = InfoItem(panel.transform, Prims.Ring, UiKit.Muted);
            _enemiesText = InfoItem(panel.transform, Prims.RoundedRect, new Color(0.85f, 0.3f, 0.3f));
            _fishText = InfoItem(panel.transform, Prims.Fish, UiKit.Gold);
        }

        Text InfoItem(Transform parent, Sprite icon, Color iconColor)
        {
            var item = UiKit.Node("Item", parent);
            UiKit.HStack(item, 8, 0, TextAnchor.MiddleLeft);
            if (icon != null)
            {
                var img = UiKit.Img(item, icon, iconColor, "Icon");
                if (icon == Prims.RoundedRect) img.pixelsPerUnitMultiplier = 4f;
                UiKit.Size(img, icon == Prims.RoundedRect ? 22 : 34, 34);
            }
            var t = UiKit.Label(item, "", 34, UiKit.TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (icon == null) t.color = iconColor;
            return t;
        }

        void BuildArrow(RectTransform parent)
        {
            _arrow = UiKit.Node("FishArrow", parent).Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            var glow = UiKit.Img(_arrow, Prims.Glow, new Color(1f, 0.8f, 0.3f, 0.35f), "Glow");
            glow.Rt().Stretch(-30, -30, -30, -30);
            _arrowTri = UiKit.Node("Tri", _arrow).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            _arrowTriImg = UiKit.Img(_arrowTri, Prims.Triangle, UiKit.Gold, "Triangle");
            _arrowTriImg.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34, 0), new Vector2(34, 34));
            _arrowFishImg = UiKit.Img(_arrow, Prims.Fish, UiKit.Gold, "Fish");
            _arrowFishImg.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 52));
        }

        void BuildBanner(RectTransform parent)
        {
            var node = UiKit.Node("Banner", parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1200, 200));
            _banner = node.gameObject.AddComponent<CanvasGroup>();
            _banner.alpha = 0f;
            _bannerTitle = UiKit.Label(node, "", 96, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            _bannerTitle.Rt().Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(1200, 120));
            var outline = _bannerTitle.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(3, -3);
            _bannerSub = UiKit.Label(node, "", 38, UiKit.Gold, TextAnchor.MiddleCenter);
            _bannerSub.Rt().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(1200, 60));
        }

        void BuildCombo(RectTransform parent)
        {
            _comboRoot = UiKit.Node("Combo", parent).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, 120), new Vector2(260, 150));
            _comboGroup = _comboRoot.gameObject.AddComponent<CanvasGroup>();
            _comboGroup.alpha = 0f;
            _comboText = UiKit.Label(_comboRoot, "", 72, UiKit.Accent, TextAnchor.LowerRight, FontStyle.Bold);
            _comboText.Rt().Place(new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(260, 90));
            var o = _comboText.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.25f, 0.1f, 0f, 0.8f);
            o.effectDistance = new Vector2(3, -3);
            _comboMult = UiKit.Label(_comboRoot, "", 28, UiKit.TextColor, TextAnchor.UpperRight, FontStyle.Bold);
            _comboMult.Rt().Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -88), new Vector2(260, 34));
            var bg = UiKit.Panel(_comboRoot, new Color(0f, 0f, 0f, 0.45f), "Timer", 4f);
            bg.Rt().Place(new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(200, 12));
            var fill = UiKit.Panel(bg.transform, UiKit.Accent, "Fill", 4f);
            _comboFill = fill.Rt();
            _comboFill.anchorMin = Vector2.zero;
            _comboFill.anchorMax = Vector2.one;
            _comboFill.offsetMin = _comboFill.offsetMax = Vector2.zero;
        }

        void BuildBossBar(RectTransform parent)
        {
            var root = UiKit.Node("BossBar", parent).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(820, 70));
            _bossGroup = root.gameObject.AddComponent<CanvasGroup>();
            _bossGroup.alpha = 0f;
            _bossName = UiKit.Label(root, "", 30, new Color(1f, 0.55f, 0.3f), TextAnchor.UpperCenter, FontStyle.Bold);
            _bossName.Rt().Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(820, 36));
            var bg = UiKit.Panel(root, new Color(0.05f, 0.05f, 0.08f, 0.85f), "Bg", 3f);
            bg.Rt().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(820, 26));
            var trail = UiKit.Panel(bg.transform, new Color(1f, 0.9f, 0.6f, 0.8f), "Trail", 3f);
            _bossTrail = trail.Rt();
            var fill = UiKit.Panel(bg.transform, new Color(0.95f, 0.3f, 0.2f), "Fill", 3f);
            _bossFill = fill.Rt();
            foreach (var r in new[] { _bossTrail, _bossFill })
            {
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(3, 3); r.offsetMax = new Vector2(-3, -3);
            }
            // отметки фаз 60% и 30%
            foreach (float m in new[] { 0.6f, 0.3f })
            {
                var tick = UiKit.Img(bg.transform, Prims.Square, new Color(1f, 1f, 1f, 0.6f), "Tick");
                tick.preserveAspect = false;
                var tr = tick.Rt();
                tr.anchorMin = new Vector2(m, 0); tr.anchorMax = new Vector2(m, 1);
                tr.sizeDelta = new Vector2(3, 0);
                tr.anchoredPosition = Vector2.zero;
            }
        }

        void BuildCrosshair(Transform root)
        {
            _cross = UiKit.Node("Crosshair", root).Place(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            var back = UiKit.Img(_cross, Prims.Ring, new Color(0.05f, 0.06f, 0.1f, 0.55f), "RingBack");
            back.Rt().Stretch(-4, -4, -4, -4);
            _crossRing = UiKit.Img(_cross, Prims.Ring, Color.white, "Ring");
            _crossRing.Rt().Stretch();
            _crossReload = UiKit.Img(_cross, Prims.Ring, new Color(1f, 1f, 1f, 0.9f), "Reload");
            _crossReload.Rt().Stretch(-6, -6, -6, -6);
            _crossReload.type = Image.Type.Filled;
            _crossReload.fillMethod = Image.FillMethod.Radial360;
            _crossReload.fillOrigin = (int)Image.Origin360.Top;
            _crossReload.fillClockwise = false;
            var dotBack = UiKit.Img(_cross, Prims.Circle, new Color(0.05f, 0.06f, 0.1f, 0.6f), "DotBack");
            dotBack.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(13, 13));
            _crossDot = UiKit.Img(_cross, Prims.Circle, Color.white, "Dot");
            _crossDot.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 8));
            var ammoBg = UiKit.Panel(_cross, new Color(0.05f, 0.06f, 0.1f, 0.6f), "AmmoBg", 3f);
            ammoBg.Rt().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(40, 20));
            var fit = ammoBg.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var h = UiKit.HStack(ammoBg, 5, 0, TextAnchor.MiddleCenter);
            h.padding = new RectOffset(8, 8, 5, 5);
            _ammoRow = ammoBg.Rt();
        }

        // ------------------------------------------------------------------ События

        void OnEnable()
        {
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.PlayerDamaged += OnPlayerDamaged;
            GameEvents.UltimateReady += OnUltReady;
            GameEvents.SecretCollected += OnSecret;
            GameEvents.ComboChanged += OnComboChanged;
            GameEvents.BossAwakened += OnBossAwakened;
            GameEvents.NewRecord += OnNewRecord;
            GameEvents.PlayerFireBlocked += OnFireBlocked;
            GameEvents.EnemyKilled += OnEnemyKilled;
            Fx.TextRequested += OnPopup;
        }

        void OnDisable()
        {
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.PlayerDamaged -= OnPlayerDamaged;
            GameEvents.UltimateReady -= OnUltReady;
            GameEvents.SecretCollected -= OnSecret;
            GameEvents.ComboChanged -= OnComboChanged;
            GameEvents.BossAwakened -= OnBossAwakened;
            GameEvents.NewRecord -= OnNewRecord;
            GameEvents.PlayerFireBlocked -= OnFireBlocked;
            GameEvents.EnemyKilled -= OnEnemyKilled;
            Fx.TextRequested -= OnPopup;
            Cursor.visible = true;
        }

        void OnLevelStarted(int level)
        {
            _levelClock = 0f;
            _bannerT = 0f;
            _boss = null;
            _bannerTitle.text = $"УРОВЕНЬ {level}";
            _bannerSub.text = BannerSubtitle(level);
            _shownHp = _shownMax = -1;
            foreach (var t in _toasts) if (t.cg != null) Destroy(t.cg.gameObject);
            _toasts.Clear();

            _tutorial.Clear();
            var gm = Game.Manager;
            if (level == 1 && gm != null && gm.Config.showTutorial && !gm.Save.tutorialDone)
                for (int i = 0; i < TutorialSteps.Length; i++)
                    _tutorial.Add(new KeyValuePair<float, string>(1.5f + i * 4.5f, TutorialSteps[i]));
        }

        static string BannerSubtitle(int level)
        {
            var gm = Game.Manager;
            if (gm != null)
            {
                foreach (var d in gm.Config.Enemies.definitions)
                    if (d != null && d.isBoss && d.AppearsOn(level))
                        return $"БОСС-УРОВЕНЬ: {d.displayName} стережёт рыбку";
                var fresh = new List<string>();
                foreach (var d in gm.Config.Enemies.definitions)
                    if (d != null && d.minLevel == level && level > 1)
                        fresh.Add(d.isGuard ? d.displayName + " (стережёт рыбку)" : d.displayName);
                if (fresh.Count > 0) return (fresh.Count == 1 ? "Новый враг: " : "Новые враги: ") + string.Join(", ", fresh);
                if (level == gm.Config.Difficulty.eliteFromLevel) return "Осторожно: появились элитные враги с коронами";
                if (gm.TimeLimit > 0) return $"Успей за {Mathf.CeilToInt(gm.TimeLimit)} c";
                if (gm.Config.requireAllEnemiesDead) return "Зачисти арену, чтобы открыть рыбку";
            }
            return "Доберись до рыбки";
        }

        void OnPlayerDamaged(Health h, DamageInfo info) { _hurtFlash = 1f; }

        void OnComboChanged(int combo, float mult) { _comboPunch = 1f; }

        void OnBossAwakened(Enemy boss)
        {
            _boss = boss;
            _bannerT = 0f;
            _bannerTitle.text = "БОСС!";
            _bannerSub.text = boss.Definition != null ? boss.Definition.displayName : "";
        }

        void OnNewRecord() => ShowToast("<color=#FFBD47><b>НОВЫЙ РЕКОРД!</b></color>  жми дальше", 3f);

        void OnUltReady() => ShowToast("<color=#FFBD47><b>Орех готов!</b></color>  зажми ПКМ / Q", 2.5f);

        void OnSecret(SecretFish f)
        {
            var gm = Game.Manager;
            if (gm != null) ShowToast($"<color={UiKit.Hex(UiKit.Cyan)}><b>Секретная рыбка!</b></color>  +{f.value}", 2.5f);
        }

        void OnFireBlocked() { _crossKick = 1f; }

        void OnEnemyKilled(Enemy e, DamageInfo info)
        {
            if (info.ByPlayer && EnemyRegistry.Count == 0 && Game.IsPlaying)
                ShowToast($"<color={UiKit.Hex(UiKit.Good)}><b>Арена зачищена!</b></color>", 2.5f);
        }

        void OnPopup(Vector2 world, string text, Color color)
        {
            var t = UiKit.Label(_popupRoot, text, 34, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.Rt().sizeDelta = new Vector2(600, 60);
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            _popups.Add(new Popup { text = t, world = world });
        }

        public void ShowToast(string text, float life = 3f)
        {
            var panel = UiKit.Panel(_toastRoot, new Color(0.07f, 0.08f, 0.12f, 0.85f), "Toast");
            var h = UiKit.HStack(panel, 0, 0, TextAnchor.MiddleCenter);
            h.padding = new RectOffset(30, 30, 14, 14);
            var label = UiKit.Label(panel.transform, text, 30, UiKit.TextColor);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var cg = panel.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _toasts.Add(new Toast { cg = cg, life = life });
            while (_toasts.Count > 4) { Destroy(_toasts[0].cg.gameObject); _toasts.RemoveAt(0); }
        }

        // ------------------------------------------------------------------ Кадр

        void Update()
        {
            var gm = Game.Manager;
            if (gm == null || gm.Run == null) return;
            float dt = Time.unscaledDeltaTime;
            var state = gm.State;
            bool inGame = state != GameState.Menu && state != GameState.Loading;
            _hud.alpha = Mathf.MoveTowards(_hud.alpha, inGame ? 1f : 0f, dt * 5f);

            var p = Game.Player;
            bool aiming = state == GameState.Playing && p != null && p.isActiveAndEnabled && !gm.InTransition;
            Cursor.visible = !aiming;

            UpdateHearts(p, dt);
            UpdateInfo(gm, dt);
            _ult.Tick(p != null ? p.Ultimate : null, dt);
            UpdateCrosshair(p, aiming, dt);
            UpdateArrow(state == GameState.Playing);
            UpdateVignette(p, dt);
            UpdateBanner(dt);
            UpdateCombo(dt);
            UpdateBossBar(dt);
            UpdateToasts(dt);
            UpdatePopups(dt);

            if (state == GameState.Playing)
            {
                _levelClock += Time.deltaTime;
                while (_tutorial.Count > 0 && _levelClock >= _tutorial[0].Key)
                {
                    ShowToast(_tutorial[0].Value, 4.2f);
                    _tutorial.RemoveAt(0);
                }
            }
        }

        void UpdateHearts(PlayerTank p, float dt)
        {
            if (p == null) return;
            int hp = Mathf.Max(0, p.Health.Hp), max = p.Health.maxHp;
            if (max != _shownMax)
            {
                foreach (var h in _hearts) Destroy(h.gameObject);
                _hearts.Clear();
                _heartPunch.Clear();
                for (int i = 0; i < max; i++)
                {
                    var img = UiKit.Img(_heartsRow, Prims.Heart, UiKit.Danger, "Heart");
                    UiKit.Size(img, 48, 48);
                    _hearts.Add(img);
                    _heartPunch.Add(0f);
                }
                _shownMax = max;
                _shownHp = hp;
            }
            if (hp < _shownHp) for (int i = hp; i < _shownHp && i < _heartPunch.Count; i++) _heartPunch[i] = 1f;
            _shownHp = hp;

            bool low = hp == 1 && max > 1;
            for (int i = 0; i < _hearts.Count; i++)
            {
                bool full = i < hp;
                _heartPunch[i] = Mathf.MoveTowards(_heartPunch[i], 0f, dt * 3f);
                float beat = full && low ? 1f + 0.12f * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 8f)) : 1f;
                _hearts[i].rectTransform.localScale = Vector3.one * (beat + 0.5f * _heartPunch[i]);
                _hearts[i].color = full ? UiKit.Danger : new Color(1f, 1f, 1f, 0.15f + 0.6f * _heartPunch[i]);
            }
        }

        void UpdateInfo(GameManager gm, float dt)
        {
            _levelText.text = $"УР. {gm.Run.levelNumber}";
            if (gm.TimeLimit > 0)
            {
                float left = gm.TimeLeft;
                _timerText.text = $"{(int)left / 60}:{(int)left % 60:00}";
                bool hurry = left < 10f;
                _timerText.color = hurry ? UiKit.Danger : UiKit.TextColor;
                _timerText.rectTransform.localScale = Vector3.one * (hurry ? 1f + 0.1f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f);
            }
            else
            {
                float t = gm.LevelTime;
                _timerText.text = $"{(int)t / 60}:{(int)t % 60:00}";
                _timerText.color = UiKit.TextColor;
            }
            _enemiesText.text = EnemyRegistry.Count.ToString();

            if (gm.Run.fish != _lastFish)
            {
                if (_lastFish >= 0) _fishPunch = 1f;
                _lastFish = gm.Run.fish;
            }
            _fishPunch = Mathf.MoveTowards(_fishPunch, 0f, dt * 3f);
            _fishText.text = gm.Run.fish.ToString();
            // очки «докручиваются» к настоящему значению — приятно смотреть, как растут
            _shownScore = Mathf.MoveTowards(_shownScore, gm.Run.score, Mathf.Max(30f, (gm.Run.score - _shownScore) * 8f) * dt);
            if (gm.Run.score < _shownScore) _shownScore = gm.Run.score;
            _scoreText.text = Mathf.RoundToInt(_shownScore).ToString("N0").Replace(",", " ");
            _fishText.rectTransform.localScale = Vector3.one * (1f + 0.35f * _fishPunch);
        }

        void UpdateCrosshair(PlayerTank p, bool aiming, float dt)
        {
            bool nutAim = p != null && p.Ultimate != null && p.Ultimate.IsAiming;
            _cross.gameObject.SetActive(aiming && !nutAim);
            if (!aiming || nutAim) return;
            _cross.position = GameInput.MouseScreen();

            var w = p.Weapon;
            var aim = p.Preview != null ? p.Preview.LastResult : AimResult.None;
            Color c = aim == AimResult.Self ? UiKit.Danger : aim == AimResult.Enemy ? UiKit.Gold : Color.white;
            _crossKick = Mathf.MoveTowards(_crossKick, 0f, dt * 5f);
            if (_crossKick > 0f) c = Color.Lerp(c, UiKit.Danger, _crossKick);
            bool ready = w != null && w.CanFire;
            _crossRing.color = Prims.WithAlpha(c, ready ? 0.95f : 0.45f);
            _crossDot.color = c;
            float shake = _crossKick * 8f * Mathf.Sin(Time.unscaledTime * 60f);
            _cross.localScale = Vector3.one * (1f + 0.15f * (aim == AimResult.Enemy ? 1f : 0f));
            _cross.anchoredPosition += new Vector2(shake, 0f);

            if (w == null) return;
            // кольцо перезарядки: заполняется по кругу, пока идёт перезарядка
            _crossReload.enabled = w.IsReloading;
            _crossReload.fillAmount = w.Reload01;
            _crossReload.color = new Color(1f, 0.7f, 0.25f, 0.95f);

            while (_ammoPips.Count < w.magazineSize)
            {
                var pip = UiKit.Img(_ammoRow, Prims.Circle, Color.white, "Pip");
                UiKit.Size(pip, 10, 10);
                _ammoPips.Add(pip);
            }
            // патроны в магазине; во время перезарядки «наливаются» по одному
            int shown = w.IsReloading ? Mathf.FloorToInt(w.Reload01 * w.magazineSize) : w.Ammo;
            for (int i = 0; i < _ammoPips.Count; i++)
            {
                _ammoPips[i].gameObject.SetActive(i < w.magazineSize);
                bool full = i < shown;
                _ammoPips[i].color = !full ? new Color(1f, 1f, 1f, 0.2f)
                                   : w.IsReloading ? new Color(1f, 0.7f, 0.25f, 0.6f) : new Color(1f, 0.9f, 0.55f);
            }
        }

        void UpdateArrow(bool playing)
        {
            var cam = Camera.main;
            var level = Game.Level;
            if (!playing || cam == null || level == null || level.Data == null) { _arrow.gameObject.SetActive(false); return; }

            Vector3 sp = cam.WorldToScreenPoint(level.ExitPosition);
            float margin = 40f;
            bool onScreen = sp.z > 0 && sp.x > margin && sp.x < Screen.width - margin && sp.y > margin && sp.y < Screen.height - margin;
            _arrow.gameObject.SetActive(!onScreen);
            if (onScreen) return;

            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 dir = ((Vector2)sp - center);
            if (sp.z < 0) dir = -dir;
            dir = dir.sqrMagnitude < 1f ? Vector2.up : dir.normalized;
            float px = Screen.height / 1080f;
            Vector2 half = center - new Vector2(70f, 150f) * px; // сверху и снизу — панели HUD, не залезаем на них
            float k = Mathf.Min(Mathf.Abs(half.x / Mathf.Max(0.0001f, Mathf.Abs(dir.x))), Mathf.Abs(half.y / Mathf.Max(0.0001f, Mathf.Abs(dir.y))));
            _arrow.position = center + dir * k;
            _arrowTri.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            _arrow.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 5f));

            bool open = (!Game.Config.requireAllEnemiesDead || EnemyRegistry.Count == 0) && EnemyRegistry.Boss == null;
            var col = open ? UiKit.Gold : new Color(0.6f, 0.6f, 0.65f);
            _arrowTriImg.color = col;
            _arrowFishImg.color = col;
        }

        void UpdateCombo(float dt)
        {
            var c = ComboSystem.Instance;
            int combo = c != null ? c.Combo : 0;
            bool show = combo >= 2 && Game.IsPlaying;
            _comboGroup.alpha = Mathf.MoveTowards(_comboGroup.alpha, show ? 1f : 0f, dt * (show ? 10f : 3f));
            if (combo >= 2)
            {
                _comboText.text = $"×{combo}";
                _comboMult.text = $"очки ×{c.Multiplier:0.##}";
                // цвет «накаляется» с ростом серии
                _comboText.color = Color.Lerp(UiKit.Accent, new Color(1f, 0.25f, 0.2f), Mathf.Clamp01((combo - 2) / 10f));
                _comboFill.anchorMax = new Vector2(c.TimeLeft01, 1f);
            }
            _comboPunch = Mathf.MoveTowards(_comboPunch, 0f, dt * 4f);
            _comboRoot.localScale = Vector3.one * (1f + 0.3f * _comboPunch * _comboPunch);
        }

        void UpdateBossBar(float dt)
        {
            bool alive = _boss != null && _boss.IsAlive && Game.State != GameState.Menu;
            _bossGroup.alpha = Mathf.MoveTowards(_bossGroup.alpha, alive ? 1f : 0f, dt * 3f);
            if (_boss == null) return;
            if (!_boss.IsAlive) { _boss = null; return; }
            float k = _boss.Health.Hp / (float)Mathf.Max(1, _boss.Health.maxHp);
            if (k < _bossFill.anchorMax.x - 0.001f) _bossShake = 1f;
            _bossFill.anchorMax = new Vector2(k, 1f);
            // «след» урона догоняет полосу с задержкой
            _bossTrail.anchorMax = new Vector2(Mathf.MoveTowards(_bossTrail.anchorMax.x, k, dt * 0.35f), 1f);
            var bb = _boss.GetComponent<BossBehaviour>();
            int phase = bb != null ? bb.Phase : 1;
            _bossName.text = $"{_boss.Definition.displayName}" + (phase == 3 ? "  —  ЯРОСТЬ" : phase == 2 ? "  —  фаза 2" : "");
            _bossShake = Mathf.MoveTowards(_bossShake, 0f, dt * 4f);
            ((RectTransform)_bossGroup.transform).anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 80f) * 6f * _bossShake, -110f);
        }

        void UpdateVignette(PlayerTank p, float dt)
        {
            _hurtFlash = Mathf.MoveTowards(_hurtFlash, 0f, dt * 2.2f);
            float low = 0f;
            if (p != null && !p.Health.IsDead && p.Health.Hp == 1 && p.Health.maxHp > 1 && Game.IsPlaying)
                low = 0.22f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f);
            _vignette.color = new Color(1f, 0.12f, 0.08f, Mathf.Max(low, 0.7f * _hurtFlash));
        }

        void UpdateBanner(float dt)
        {
            _bannerT += dt;
            const float inT = 0.25f, hold = 1.8f, outT = 0.5f;
            float a = _bannerT < inT ? _bannerT / inT : _bannerT < inT + hold ? 1f : 1f - (_bannerT - inT - hold) / outT;
            _banner.alpha = Mathf.Clamp01(a);
            float s = _bannerT < inT ? Mathf.Lerp(1.25f, 1f, _bannerT / inT) : 1f;
            _banner.transform.localScale = Vector3.one * s;
        }

        void UpdateToasts(float dt)
        {
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                if (t.cg == null) { _toasts.RemoveAt(i); continue; }
                t.t += dt;
                float a = t.t < 0.2f ? t.t / 0.2f : t.t > t.life - 0.4f ? (t.life - t.t) / 0.4f : 1f;
                t.cg.alpha = Mathf.Clamp01(a);
                if (t.t >= t.life) { Destroy(t.cg.gameObject); _toasts.RemoveAt(i); }
            }
        }

        void UpdatePopups(float dt)
        {
            var cam = Camera.main;
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var p = _popups[i];
                p.t += dt;
                if (p.t >= 1.1f || p.text == null || cam == null)
                {
                    if (p.text != null) Destroy(p.text.gameObject);
                    _popups.RemoveAt(i);
                    continue;
                }
                Vector2 world = p.world + Vector2.up * (0.9f * (1f - (1f - p.t) * (1f - p.t)));
                p.text.rectTransform.position = cam.WorldToScreenPoint(world);
                float pop = p.t < 0.12f ? Mathf.Lerp(0.5f, 1.2f, p.t / 0.12f) : Mathf.Lerp(1.2f, 1f, (p.t - 0.12f) * 5f);
                p.text.rectTransform.localScale = Vector3.one * Mathf.Max(1f, pop);
                p.text.color = Prims.WithAlpha(p.text.color, p.t > 0.7f ? 1f - (p.t - 0.7f) / 0.4f : 1f);
            }
        }
    }
}
