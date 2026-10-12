// Экраны поверх игры: титульный, пауза, настройки, скины, победа + магазин прокачки, поражение, затемнение переходов.
// Какой экран виден — решается по Game.State, сами экраны только вызывают методы GameManager.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks
{
    public class ScreensView : MonoBehaviour
    {
        enum Overlay { None, Settings, Skins, Garage }

        UiFade _menu, _pause, _settings, _skins, _garage, _win, _lose;
        Image _fade;
        Overlay _overlay;
        GameState _lastState = (GameState)(-1);
        float _stateTime;

        Text _menuRecord;
        RectTransform _menuFish;
        Text _volumeLabel, _controlsLabel, _controlsHint, _shakeLabel;
        readonly List<SkinCard> _skinCards = new List<SkinCard>();
        Text _winStats, _winWallet, _winScore, _loseScore, _loseRecord;
        readonly List<Image> _stars = new List<Image>();
        readonly List<Text> _starLabels = new List<Text>();
        int _starsShown;
        RectTransform _walletIcon;
        readonly List<ShopCard> _shopCards = new List<ShopCard>();
        Text _loseTitle, _loseHint, _loseStats;

        class SkinCard { public SkinDefinition skin; public Button button; public Text status; public Image frame; }
        class ModuleCard { public TankModuleDefinition module; public Button button; public Text status; public Image frame; }
        readonly List<ModuleCard> _moduleCards = new List<ModuleCard>();
        Text _garageNote, _menuLoadout;
        class ShopCard { public UpgradeDefinition up; public Button buy; public Text buyLabel; public List<Image> pips = new List<Image>(); }

        static readonly float[] VolumeSteps = { 0f, 0.25f, 0.5f, 0.7f, 1f };

        static readonly string[] Hints =
        {
            "Снаряд отскакивает от стен — бей из-за угла рикошетом.",
            "<color=#FF5C52>Красный</color> пунктир — ты попадёшь сам в себя!",
            "Орех (зажми ПКМ / Q) падает не сразу — целься туда, куда враг побежит.",
            "Орех рушит стены и укрытия — можно пробить короткий путь к рыбке.",
            "Не стой под своим орехом: красный прицел — заденет и кота.",
            "R — перезарядить заранее, пока рядом никого нет.",
            "Комбо сгорает, если кота ранили. Держи дистанцию — и множитель дорастёт до ×4.",
            "Босс в оглушении после тарана беззащитен — самое время стрелять.",
            "Карусель босса крутится по кругу — ищи просвет между спиралями.",
            "Три звезды: пройди, зачисти арену и успей за контрольное время — это лишние рыбки.",
            "Сердечки падают чаще, когда у кота мало HP — рискни и подбери.",
            "Секретные рыбки прячутся в тупиках — на них покупается прокачка.",
            "После удара кот секунду неуязвим (мигает).",
            "Стрелки сидят у укрытий: выманивай их или бей отскоком.",
            "Ползуны бегут на звук выстрела — отступай в узкий коридор.",
            "Снарядов на экране ограниченное число — не трать их впустую.",
        };

        // ------------------------------------------------------------------ Сборка

        void Awake()
        {
            UiKit.EnsureEventSystem();
            var canvas = UiKit.MakeCanvas("Screens", transform, 20);
            var root = canvas.transform;

            BuildMenu(root);
            BuildPause(root);
            BuildSettings(root);
            BuildSkins(root);
            BuildGarage(root);
            BuildWin(root);
            BuildLose(root);

            var fadeCanvas = UiKit.MakeCanvas("Fade", transform, 100);
            _fade = UiKit.Img(fadeCanvas.transform, Prims.Square, new Color(0.03f, 0.03f, 0.06f, 0f), "Fade");
            _fade.preserveAspect = false;
            _fade.Rt().Stretch();
        }

        UiFade MakeScreen(Transform root, string name, out RectTransform panel, float width, Color? dim = null)
        {
            var node = UiKit.Node(name, root).Stretch();
            node.gameObject.AddComponent<CanvasGroup>();
            var bg = UiKit.Img(node, Prims.Square, dim ?? UiKit.Dim, "Dim");
            bg.preserveAspect = false;
            bg.raycastTarget = true;
            bg.Rt().Stretch();

            var p = UiKit.Panel(node, UiKit.Ink, "Panel");
            panel = p.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 100));
            var fit = p.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiKit.VStack(p, 16, 40);

            var fade = node.gameObject.AddComponent<UiFade>();
            fade.popTarget = panel;
            return fade;
        }

        static Text Title(Transform parent, string text, Color color, int size = 64)
        {
            var t = UiKit.Label(parent, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Size(t, -1, size + 20);
            return t;
        }

        static Text Line(Transform parent, string text, int size = 30, Color? color = null)
        {
            var t = UiKit.Label(parent, text, size, color ?? UiKit.Muted);
            UiKit.Size(t, -1, size + 18);
            return t;
        }

        void BuildMenu(Transform root)
        {
            var node = UiKit.Node("Menu", root).Stretch();
            node.gameObject.AddComponent<CanvasGroup>();
            var bg = UiKit.Img(node, UiKit.Vignette, new Color(0.02f, 0.02f, 0.06f, 0.95f), "Vignette");
            bg.preserveAspect = false;
            bg.Rt().Stretch(-200, -200, -200, -200);
            var dim = UiKit.Img(node, Prims.Square, new Color(0.02f, 0.02f, 0.06f, 0.55f), "Dim");
            dim.preserveAspect = false;
            dim.raycastTarget = true;
            dim.Rt().Stretch();

            var col = UiKit.Node("Column", node).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1400, 760));
            UiKit.VStack(col, 18, 0, TextAnchor.MiddleCenter);

            _menuFish = UiKit.Node("FishHolder", col);
            UiKit.Size(_menuFish, -1, 120);
            var glow = UiKit.Img(_menuFish, Prims.Glow, new Color(1f, 0.8f, 0.3f, 0.5f), "Glow");
            glow.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 320));
            var fish = UiKit.Img(_menuFish, Prims.Fish, UiKit.Gold, "Fish");
            fish.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150));

            var title = UiKit.Label(col, "КОТ-ТАНКИСТ", 130, UiKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Size(title, -1, 150);
            var o = title.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.25f, 0.1f, 0f, 0.9f);
            o.effectDistance = new Vector2(4, -4);
            Line(col, "операция «Рыбка»", 40, UiKit.TextColor);
            UiKit.Spacer(col, 20);

            var buttonsRow = UiKit.Node("ButtonsRow", col);
            UiKit.Size(buttonsRow, -1, 356);
            UiKit.HStack(buttonsRow, 0, 0, TextAnchor.MiddleCenter);
            var buttons = UiKit.Node("Buttons", buttonsRow);
            UiKit.Size(buttons, 480, 356);
            UiKit.VStack(buttons, 14);
            UiKit.Btn(buttons, $"ИГРАТЬ  {UiKit.Hotkey("Enter")}", () => Game.Manager?.StartRun(), UiKit.Accent, 36, 84);
            UiKit.Btn(buttons, "Гараж", () => _overlay = Overlay.Garage);
            UiKit.Btn(buttons, "Скины", () => _overlay = Overlay.Skins);
            UiKit.Btn(buttons, "Настройки", () => _overlay = Overlay.Settings);

            _menuRecord = Line(col, "", 28);
            _menuLoadout = Line(col, "", 26, UiKit.Accent);
            var hint = UiKit.Label(node, "WASD — ехать · мышь — прицел · ЛКМ / пробел — огонь · R — перезарядка · ПКМ / Q — орех · Esc — пауза",
                                   26, UiKit.Muted);
            hint.Rt().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1600, 40));

            _menu = node.gameObject.AddComponent<UiFade>();
            _menu.popTarget = col;
        }

        void BuildPause(Transform root)
        {
            _pause = MakeScreen(root, "Pause", out var panel, 560);
            Title(panel, "ПАУЗА", UiKit.TextColor);
            UiKit.Btn(panel, $"Продолжить  {UiKit.Hotkey("Esc")}", () => Game.Manager?.SetPaused(false), UiKit.Accent);
            UiKit.Btn(panel, "Заново", () => Game.Manager?.RestartLevel());
            UiKit.Btn(panel, "Гараж", () => _overlay = Overlay.Garage);
            UiKit.Btn(panel, "Скины", () => _overlay = Overlay.Skins);
            UiKit.Btn(panel, "Настройки", () => _overlay = Overlay.Settings);
            UiKit.Btn(panel, "В главное меню", () => Game.Manager?.GoToMenu());
        }

        void BuildSettings(Transform root)
        {
            _settings = MakeScreen(root, "Settings", out var panel, 680, new Color(0f, 0f, 0f, 0.5f));
            Title(panel, "НАСТРОЙКИ", UiKit.TextColor);

            _volumeLabel = UiKit.LabelOf(UiKit.Btn(panel, "", () =>
            {
                var gm = Game.Manager; if (gm == null) return;
                int i = 0;
                while (i < VolumeSteps.Length && VolumeSteps[i] <= gm.Save.sfxVolume + 0.01f) i++;
                gm.SetSfxVolume(VolumeSteps[i % VolumeSteps.Length]);
                Sfx.Play(SfxId.Pickup, 0.8f, 0f);
            }));
            _controlsLabel = UiKit.LabelOf(UiKit.Btn(panel, "", () =>
            {
                var gm = Game.Manager; if (gm != null) gm.SetTankControls(!gm.Save.tankControls);
            }));
            _controlsHint = Line(panel, "", 24);
            _shakeLabel = UiKit.LabelOf(UiKit.Btn(panel, "", () =>
            {
                var gm = Game.Manager; if (gm == null) return;
                gm.SetScreenShake(!gm.Save.screenShake);
                CameraFollow.Shake(0.5f);
            }));
            UiKit.Spacer(panel, 6);
            UiKit.Btn(panel, $"Назад  {UiKit.Hotkey("Esc")}", () => _overlay = Overlay.None, UiKit.InkSoft);
        }

        void BuildSkins(Transform root)
        {
            _skins = MakeScreen(root, "Skins", out var panel, 1000, new Color(0f, 0f, 0f, 0.5f));
            Title(panel, "СКИНЫ", UiKit.TextColor);

            var gridNode = UiKit.Node("Grid", panel);
            var grid = gridNode.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(210, 260);
            grid.spacing = new Vector2(16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;

            foreach (var skin in Game.Config.Skins)
            {
                var s = skin;
                var card = new SkinCard { skin = s };
                var img = UiKit.Panel(gridNode, UiKit.InkSoft, "SkinCard");
                img.raycastTarget = true;
                card.frame = img;
                card.button = img.gameObject.AddComponent<Button>();
                card.button.targetGraphic = img;
                card.button.onClick.AddListener(() =>
                {
                    Sfx.Play(SfxId.UiClick, 0.8f, 0f);
                    Game.Manager?.SelectSkin(s);
                });
                img.gameObject.AddComponent<UiButtonFx>();

                var preview = UiKit.Node("Preview", img.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(130, 130));
                TankPreview(preview, s);
                var name = UiKit.Label(img.transform, s.displayName, 28, UiKit.TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                name.Rt().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 62), new Vector2(200, 40));
                card.status = UiKit.Label(img.transform, "", 22, UiKit.Muted);
                card.status.Rt().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(200, 36));
                _skinCards.Add(card);
            }
            UiKit.Size(gridNode, -1, 260 * Mathf.CeilToInt(_skinCards.Count / 4f) + 16);
            UiKit.Btn(panel, $"Назад  {UiKit.Hotkey("Esc")}", () => _overlay = Overlay.None, UiKit.InkSoft);
        }

        /// <summary>Мини-танк из UI-картинок в цветах скина.</summary>
        static void TankPreview(RectTransform parent, SkinDefinition s)
        {
            void Box(Sprite sp, Color c, Vector2 pos, Vector2 size, float radius = 3f)
            {
                var i = UiKit.Img(parent, sp, c, "Part");
                i.preserveAspect = false;
                if (sp == Prims.RoundedRect) i.pixelsPerUnitMultiplier = radius;
                i.Rt().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            }
            Box(Prims.RoundedRect, s.trackColor, new Vector2(0, 46), new Vector2(116, 24));
            Box(Prims.RoundedRect, s.trackColor, new Vector2(0, -46), new Vector2(116, 24));
            Box(Prims.RoundedRect, s.bodyColor, Vector2.zero, new Vector2(96, 92));
            Box(Prims.RoundedRect, Prims.Dark(s.bodyColor, 0.65f), new Vector2(42, 0), new Vector2(60, 18), 6f);
            Box(Prims.Circle, s.catColor, new Vector2(-14, 22), new Vector2(18, 18));
            Box(Prims.Circle, s.catColor, new Vector2(-14, -22), new Vector2(18, 18));
            Box(Prims.Circle, s.catColor, Vector2.zero, new Vector2(52, 52));
        }

        // ------------------------------------------------------------------ Гараж

        void BuildGarage(Transform root)
        {
            _garage = MakeScreen(root, "Garage", out var panel, 1260, new Color(0f, 0f, 0f, 0.55f));
            panel.GetComponent<VerticalLayoutGroup>().spacing = 10;
            Title(panel, "ГАРАЖ", UiKit.TextColor);
            _garageNote = Line(panel, "", 24);

            var cfg = Game.Config;
            Line(panel, "КОРПУС", 26, UiKit.Accent);
            ModuleGrid(panel, cfg.Hulls, 4, new Vector2(272, 196));
            Line(panel, "ПУШКА", 26, UiKit.Accent);
            ModuleGrid(panel, cfg.Cannons, 5, new Vector2(216, 222));
            UiKit.Spacer(panel, 4);
            UiKit.Btn(panel, $"Готово  {UiKit.Hotkey("Esc")}", () => _overlay = Overlay.None, UiKit.InkSoft);
        }

        void ModuleGrid(RectTransform panel, IReadOnlyList<TankModuleDefinition> modules, int columns, Vector2 cell)
        {
            var node = UiKit.Node("Grid", panel);
            var grid = node.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(14, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperCenter;
            UiKit.Size(node, -1, (cell.y + 14) * Mathf.CeilToInt(modules.Count / (float)columns));

            foreach (var mod in modules)
            {
                var m = mod;
                var card = new ModuleCard { module = m };
                var img = UiKit.Panel(node, UiKit.InkSoft, "ModuleCard");
                img.raycastTarget = true;
                card.frame = img;
                card.button = img.gameObject.AddComponent<Button>();
                card.button.targetGraphic = img;
                card.button.onClick.AddListener(() =>
                {
                    Sfx.Play(SfxId.UiClick, 0.8f, 0f);
                    Game.Manager?.SelectModule(m);
                });
                img.gameObject.AddComponent<UiButtonFx>();

                float w = cell.x - 28;
                var name = UiKit.Label(img.transform, m.displayName, 27, UiKit.TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
                name.Rt().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -10), new Vector2(w, 34));
                var desc = UiKit.Label(img.transform, m.description, 19, UiKit.Muted, TextAnchor.UpperLeft);
                desc.Rt().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -44), new Vector2(w, 46));

                float y = -94;
                if (m.slot == ModuleSlot.Hull)
                {
                    StatBar(img.transform, "Броня", m.maxHp / 8f, $"{m.maxHp}", ref y, w);
                    StatBar(img.transform, "Скорость", m.moveSpeed / 5f, $"{m.moveSpeed:0.#}", ref y, w);
                    StatBar(img.transform, "Манёвр", m.turnSpeed / 280f, "", ref y, w);
                }
                else
                {
                    StatBar(img.transform, "Урон", m.damage / 5f, $"{m.damage}", ref y, w);
                    StatBar(img.transform, "Темп", (1f / m.fireCooldown) / 9f, $"{1f / m.fireCooldown:0.#}/с", ref y, w);
                    StatBar(img.transform, "Магазин", m.magazineSize / 12f, $"{m.magazineSize}", ref y, w);
                    StatBar(img.transform, "Отскоки", m.maxBounces / 3f, $"{m.maxBounces}", ref y, w);
                }
                card.status = UiKit.Label(img.transform, "", 19, UiKit.Muted, TextAnchor.LowerRight);
                card.status.Rt().Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 6), new Vector2(w, 26));
                _moduleCards.Add(card);
            }
        }

        static void StatBar(Transform card, string label, float value, string valueText, ref float y, float width)
        {
            var l = UiKit.Label(card, label, 18, UiKit.Muted, TextAnchor.MiddleLeft);
            l.Rt().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, y), new Vector2(90, 22));
            float barW = width - 90 - 44;
            var bg = UiKit.Panel(card, new Color(0f, 0f, 0f, 0.4f), "Bar", 6f);
            bg.Rt().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(104, y - 6), new Vector2(barW, 10));
            var fill = UiKit.Panel(bg.transform, UiKit.Accent, "Fill", 6f);
            var rt = fill.Rt();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp(value, 0.06f, 1f), 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (!string.IsNullOrEmpty(valueText))
            {
                var v = UiKit.Label(card, valueText, 18, UiKit.TextColor, TextAnchor.MiddleRight);
                v.Rt().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(104 + barW, y), new Vector2(44, 22));
            }
            y -= 26;
        }

        void RefreshGarage(GameManager gm)
        {
            _garageNote.text = gm.State == GameState.Menu
                ? "Выбери корпус и пушку — у каждой свои сильные стороны"
                : "Изменения вступят в силу со следующего уровня (или «Заново»)";
            foreach (var c in _moduleCards)
            {
                bool unlocked = gm.IsModuleUnlocked(c.module);
                var selected = c.module.slot == ModuleSlot.Hull ? gm.Save.selectedHull : gm.Save.selectedCannon;
                bool isSel = selected == c.module.id;
                c.button.interactable = unlocked;
                c.status.text = isSel ? $"<color={UiKit.Hex(UiKit.Good)}>✓ выбран</color>"
                              : unlocked ? "выбрать" : $"с ур. {c.module.unlockAtLevel}";
                c.frame.color = isSel ? Prims.Light(UiKit.InkSoft, 0.14f) : UiKit.InkSoft;
            }
        }

        void BuildWin(Transform root)
        {
            _win = MakeScreen(root, "Win", out var panel, 1180);
            Title(panel, "РЫБКА ДОБЫТА!", UiKit.Gold, 72);

            // три звезды: пройти · зачистить · успеть — выскакивают по очереди
            var starsRow = UiKit.Node("Stars", panel);
            UiKit.Size(starsRow, -1, 96);
            UiKit.HStack(starsRow, 60, 0, TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++)
            {
                var cell = UiKit.Node("Star", starsRow);
                UiKit.Size(cell, 200, 96);
                var img = UiKit.Img(cell, Prims.Star, new Color(1f, 1f, 1f, 0.12f), "Icon");
                img.Rt().Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(64, 64));
                var lbl = UiKit.Label(cell, "", 20, UiKit.Muted);
                lbl.Rt().Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(200, 28));
                _stars.Add(img);
                _starLabels.Add(lbl);
            }
            _winStats = Line(panel, "", 26, UiKit.TextColor);
            _winScore = Line(panel, "", 30, UiKit.Gold);

            var wallet = UiKit.Node("Wallet", panel);
            UiKit.Size(wallet, -1, 64);
            UiKit.HStack(wallet, 12, 0, TextAnchor.MiddleCenter);
            var icon = UiKit.Img(wallet, Prims.Fish, UiKit.Gold, "Fish");
            UiKit.Size(icon, 56, 56);
            _walletIcon = icon.Rt();
            _winWallet = UiKit.Label(wallet, "", 52, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            _winWallet.horizontalOverflow = HorizontalWrapMode.Overflow;
            Line(panel, "Прокачка до конца забега — потрать рыбок:", 26);

            var gridNode = UiKit.Node("Shop", panel);
            var grid = gridNode.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(262, 176);
            grid.spacing = new Vector2(16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;

            foreach (var up in Game.Config.Upgrades)
            {
                var u = up;
                var card = new ShopCard { up = u };
                var bg = UiKit.Panel(gridNode, UiKit.InkSoft, "Card");
                var title = UiKit.Label(bg.transform, u.title, 27, UiKit.TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
                title.Rt().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -12), new Vector2(228, 36));
                var desc = UiKit.Label(bg.transform, u.description, 21, UiKit.Muted, TextAnchor.UpperLeft);
                desc.Rt().Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -48), new Vector2(228, 56));

                var pips = UiKit.Node("Pips", bg.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 28), new Vector2(110, 20));
                UiKit.HStack(pips, 6, 0, TextAnchor.MiddleLeft);
                for (int i = 0; i < u.maxLevel; i++)
                {
                    var pip = UiKit.Panel(pips, UiKit.Accent, "Pip", 4f);
                    UiKit.Size(pip, 24, 12);
                    card.pips.Add(pip);
                }

                card.buy = UiKit.Btn(bg.transform, "", () => Game.Manager?.TryBuyUpgrade(u), UiKit.Gold, 24, 50);
                card.buy.Rt().Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, 14), new Vector2(116, 50));
                Destroy(card.buy.GetComponent<LayoutElement>());
                card.buyLabel = UiKit.LabelOf(card.buy);
                _shopCards.Add(card);
            }
            UiKit.Size(gridNode, -1, 192 * Mathf.CeilToInt(_shopCards.Count / 4f));
            UiKit.Spacer(panel, 4);
            var next = UiKit.Btn(panel, $"ДАЛЬШЕ  {UiKit.Hotkey("Enter")}", () => Game.Manager?.NextLevel(), UiKit.Accent, 36, 84);
            UiKit.Size(next, -1, 84);
        }

        void BuildLose(Transform root)
        {
            _lose = MakeScreen(root, "Lose", out var panel, 760, new Color(0.15f, 0f, 0f, 0.55f));
            _loseTitle = Title(panel, "", UiKit.Danger, 72);
            _loseStats = Line(panel, "", 28, UiKit.TextColor);
            _loseScore = Line(panel, "", 44, UiKit.Gold);
            _loseRecord = Line(panel, "", 26);
            var tip = UiKit.Panel(panel, UiKit.InkSoft, "Tip");
            UiKit.Size(tip, -1, 110);
            _loseHint = UiKit.Label(tip.transform, "", 28, UiKit.TextColor);
            _loseHint.Rt().Stretch(24, 24, 10, 10);
            UiKit.Btn(panel, $"ЕЩЁ РАЗ  {UiKit.Hotkey("R")}", () => Game.Manager?.RestartLevel(), UiKit.Accent, 34, 80);
            UiKit.Btn(panel, "Новый забег с 1 уровня", () => Game.Manager?.StartRun());
            UiKit.Btn(panel, "В главное меню", () => Game.Manager?.GoToMenu(), UiKit.InkSoft);
        }

        // ------------------------------------------------------------------ Кадр

        void Update()
        {
            var gm = Game.Manager;
            if (gm == null || gm.Run == null) return;
            float dt = Time.unscaledDeltaTime;
            var state = gm.State;
            if (state != _lastState)
            {
                if (state == GameState.LevelLost) _loseHint.text = "Совет: " + Hints[Random.Range(0, Hints.Length)];
                if (state == GameState.LevelWon) _starsShown = 0;
                if (!(state == GameState.Paused || state == GameState.Menu)) _overlay = Overlay.None;
                _lastState = state;
                _stateTime = 0f;
            }
            _stateTime += dt;

            if (_overlay != Overlay.None && GameInput.PausePressed()) _overlay = Overlay.None;
            Game.ModalOpen = _overlay != Overlay.None;

            _menu.Show(state == GameState.Menu && _overlay == Overlay.None);
            _pause.Show(state == GameState.Paused && _overlay == Overlay.None);
            _settings.Show(_overlay == Overlay.Settings);
            _skins.Show(_overlay == Overlay.Skins);
            _garage.Show(_overlay == Overlay.Garage);
            _win.Show(state == GameState.LevelWon && _stateTime > 0.7f);   // даём посмотреть на конфетти
            _lose.Show(state == GameState.LevelLost && _stateTime > 1.1f); // и на взрыв в замедлении

            if (_menu.Visible) RefreshMenu(gm);
            if (_settings.Visible) RefreshSettings(gm);
            if (_skins.Visible) RefreshSkins(gm);
            if (_garage.Visible) RefreshGarage(gm);
            if (_win.Visible) RefreshWin(gm);
            if (_lose.Visible) RefreshLose(gm);

            _fade.color = new Color(_fade.color.r, _fade.color.g, _fade.color.b, gm.Fade);
            _fade.raycastTarget = gm.Fade > 0.01f;
        }

        void RefreshMenu(GameManager gm)
        {
            _menuRecord.text = gm.Save.bestLevel > 1
                ? $"Рекорд: уровень {gm.Save.bestLevel} · телефонов разбито: {gm.Save.totalKills}"
                : "Кот-танкист отправляется за рыбкой. Телефоны против.";
            var hull = gm.SelectedHull; var gun = gm.SelectedCannon;
            _menuLoadout.text = hull != null && gun != null ? $"Корпус «{hull.displayName}» · Пушка «{gun.displayName}»" : "";
            float t = Time.unscaledTime;
            _menuFish.localRotation = Quaternion.Euler(0, 0, 8f * Mathf.Sin(t * 1.5f));
            _menuFish.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(t * 3f));
        }

        void RefreshSettings(GameManager gm)
        {
            var s = gm.Save;
            _volumeLabel.text = s.sfxVolume <= 0.001f ? "Звук: выкл" : $"Звук: {Mathf.RoundToInt(s.sfxVolume * 100)}%";
            _controlsLabel.text = s.tankControls ? "Управление: танковое" : "Управление: аркадное";
            _controlsHint.text = s.tankControls ? "W/S — вперёд/назад, A/D — поворот корпуса" : "WASD — ехать в любую сторону экрана";
            _shakeLabel.text = s.screenShake ? "Тряска экрана: вкл" : "Тряска экрана: выкл";
        }

        void RefreshSkins(GameManager gm)
        {
            foreach (var c in _skinCards)
            {
                bool unlocked = gm.IsSkinUnlocked(c.skin);
                bool selected = gm.Save.selectedSkin == c.skin.id;
                c.button.interactable = unlocked;
                c.status.text = selected ? $"<color={UiKit.Hex(UiKit.Good)}>✓ выбран</color>"
                              : unlocked ? "выбрать" : $"дойди до ур. {c.skin.unlockAtLevel}";
                c.frame.color = selected ? Prims.Light(UiKit.InkSoft, 0.12f) : UiKit.InkSoft;
            }
        }

        void RefreshWin(GameManager gm)
        {
            var run = gm.Run;
            float t = gm.LevelTime;
            _winStats.text = $"Уровень {run.levelNumber} · время {(int)t / 60}:{(int)t % 60:00} · разбито {run.levelKills} · лучшее комбо ×{run.bestCombo}" +
                             (gm.SecretsOnLevel > 0 ? $" · секреты {gm.SecretsOnLevel - CountSecretsLeft()}/{gm.SecretsOnLevel}" : "");
            _winScore.text = $"Счёт {Fmt(run.score)}   <size=22><color=#9AA3B8>+{run.lastClearBonus} уровень · +{run.lastTimeBonus} время · +{250 * (run.lastStars - 1)} звёзды · +{run.lastStarFish} рыб.</color></size>";

            bool[] earned = { true, run.lastCleared, run.lastFast };
            string[] labels = { "пройден", "арена зачищена", $"быстрее {Mathf.RoundToInt(gm.ParTime)} c" };
            // звёзды выскакивают по одной с «попом», каждая следующая выше по тону
            int due = Mathf.Clamp(Mathf.FloorToInt((_stateTime - 0.9f) / 0.35f) + 1, 0, 3);
            while (_starsShown < due)
            {
                int i = _starsShown++;
                if (earned[i]) { Sfx.Play(SfxId.Combo, 0.8f, 0f, 1f + 0.25f * i); _stars[i].rectTransform.localScale = Vector3.one * 1.8f; }
            }
            for (int i = 0; i < 3; i++)
            {
                bool shown = i < _starsShown;
                _stars[i].color = shown && earned[i] ? UiKit.Gold : new Color(1f, 1f, 1f, 0.12f);
                _stars[i].rectTransform.localScale = Vector3.Lerp(_stars[i].rectTransform.localScale, Vector3.one, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
                _starLabels[i].text = labels[i];
                _starLabels[i].color = shown && earned[i] ? UiKit.TextColor : UiKit.Muted;
            }
            _winWallet.text = run.fish.ToString();
            _walletIcon.localRotation = Quaternion.Euler(0, 0, 8f * Mathf.Sin(Time.unscaledTime * 2f));

            foreach (var c in _shopCards)
            {
                int lvl = run.GetUpgradeLevel(c.up.id);
                bool maxed = lvl >= c.up.maxLevel;
                c.buy.interactable = gm.CanBuy(c.up);
                c.buyLabel.text = maxed ? "МАКС" : $"{c.up.CostForLevel(lvl)} рыб.";
                for (int i = 0; i < c.pips.Count; i++) c.pips[i].color = i < lvl ? UiKit.Accent : new Color(1f, 1f, 1f, 0.12f);
            }
        }

        static string Fmt(int n) => n.ToString("N0").Replace(",", " ");

        static int CountSecretsLeft()
        {
            var root = Game.LevelRoot;
            return root != null ? root.GetComponentsInChildren<SecretFish>().Length : 0;
        }

        void RefreshLose(GameManager gm)
        {
            _loseTitle.text = gm.LastLoseReason == LoseReason.TimeOut ? "ВРЕМЯ ВЫШЛО!" : "КОТА ПОКУСАЛИ…";
            float t = gm.LevelTime;
            _loseStats.text = $"Уровень {gm.Run.levelNumber} · продержался {(int)t / 60}:{(int)t % 60:00} · разбито {gm.Run.levelKills} · комбо ×{gm.Run.bestCombo}";
            _loseScore.text = $"Счёт: {Fmt(gm.Run.score)}";
            _loseRecord.text = gm.IsNewRecord
                ? $"<color={UiKit.Hex(UiKit.Gold)}><b>НОВЫЙ РЕКОРД!</b></color>"
                : $"Рекорд: {Fmt(gm.Save.bestScore)} · осталось {Fmt(Mathf.Max(0, gm.Save.bestScore - gm.Run.score))} до него";
        }
    }
}
