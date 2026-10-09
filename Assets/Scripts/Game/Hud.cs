using System;
using AutoBattler.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace AutoBattler.Game
{
    /// <summary>UI Toolkit overlay built entirely in code: stats on the left, shop and actions on the right.</summary>
    public sealed class Hud
    {
        private static readonly Color PanelColor = new Color(0.12f, 0.13f, 0.18f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.25f, 0.33f);
        private static readonly Color DisabledColor = new Color(0.16f, 0.17f, 0.21f);
        private static readonly Color AccentColor = new Color(0.85f, 0.35f, 0.25f);
        private static readonly Color TextColor = new Color(0.93f, 0.94f, 0.96f);

        private readonly UIDocument _document;
        private readonly Label _round, _health, _gold, _board, _message, _help;
        private readonly Button[] _shopButtons = new Button[Shop.SlotCount];
        private readonly Button _reroll, _sell, _fight, _restart;

        public event Action<int> BuyClicked;
        public event Action RerollClicked, SellClicked, FightClicked, RestartClicked;

        public Hud(Transform parent)
        {
            var go = new GameObject("HUD");
            go.SetActive(false);
            go.transform.SetParent(parent, false);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.match = 0.5f;

            _document = go.AddComponent<UIDocument>();
            _document.panelSettings = settings;
            go.SetActive(true);

            var root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.flexDirection = FlexDirection.Row;
            root.style.justifyContent = Justify.SpaceBetween;
            root.style.color = TextColor;
            root.style.fontSize = 18;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);

            var left = Panel(root);
            Title(left, "AUTO BATTLER");
            _round = Text(left);
            _health = Text(left);
            _gold = Text(left);
            _board = Text(left);
            _message = Text(left);
            _message.style.marginTop = 16;
            _message.style.color = new Color(1f, 0.85f, 0.4f);
            _message.style.whiteSpace = WhiteSpace.Normal;
            _help = Text(left);
            _help.style.marginTop = 16;
            _help.style.fontSize = 13;
            _help.style.whiteSpace = WhiteSpace.Normal;
            _help.style.color = new Color(0.7f, 0.72f, 0.78f);
            _help.text = "Click a unit, then a cell or bench slot to place it.\n" +
                         "1-5 buy  ·  R reroll  ·  S sell  ·  Space fight  ·  Right-click cancel";

            var right = Panel(root);
            Title(right, "SHOP");
            for (int i = 0; i < Shop.SlotCount; i++)
            {
                int slot = i;
                _shopButtons[i] = MakeButton(right, "", () => BuyClicked?.Invoke(slot));
                _shopButtons[i].style.height = 56;
            }
            _reroll = MakeButton(right, $"Reroll ({GameSession.RerollCost}g)  [R]", () => RerollClicked?.Invoke());
            _reroll.style.marginTop = 12;
            _sell = MakeButton(right, "Sell selected  [S]", () => SellClicked?.Invoke());
            _fight = MakeButton(right, "FIGHT!  [Space]", () => FightClicked?.Invoke());
            _fight.style.marginTop = 12;
            _fight.style.height = 48;
            _restart = MakeButton(right, "New run", () => RestartClicked?.Invoke());
            _restart.style.marginTop = 12;
        }

        /// <summary>True if the screen-space point (origin bottom-left) is over an interactive UI element.</summary>
        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            var panel = _document.rootVisualElement.panel;
            if (panel == null) return false;
            var panelPos = RuntimePanelUtils.ScreenToPanel(panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            var picked = panel.Pick(panelPos);
            return picked != null && picked != _document.rootVisualElement;
        }

        public void SetMessage(string message) => _message.text = message;

        public void Refresh(GameSession session, OwnedUnit selected)
        {
            bool shopping = session.Phase == Phase.Shop;
            _round.text = $"Round {session.Round} / {GameSession.FinalRound}";
            _health.text = $"Health  {session.Health}";
            _gold.text = $"Gold  {session.Gold}";
            _board.text = $"Board  {session.BoardCount} / {session.BoardCapacity}";

            for (int i = 0; i < Shop.SlotCount; i++)
            {
                var def = session.Shop[i];
                var button = _shopButtons[i];
                button.text = def == null
                    ? "—"
                    : $"[{i + 1}] {def.Name}  ·  {def.Cost}g\nHP {def.MaxHp}  ATK {def.Attack}  RNG {def.Range}";
                bool enabled = shopping && session.CanBuy(i);
                SetEnabled(button, enabled);
                button.style.borderLeftColor = def != null ? SpriteFactory.FromRgb(def.Color) : DisabledColor;
            }

            SetEnabled(_reroll, shopping && session.Gold >= GameSession.RerollCost);
            SetEnabled(_sell, shopping && selected != null);
            _sell.text = selected != null ? $"Sell {selected} (+{selected.SellValue}g)  [S]" : "Sell selected  [S]";
            SetEnabled(_fight, shopping && session.BoardCount > 0);

            bool over = session.Phase == Phase.Victory || session.Phase == Phase.Defeat;
            _restart.style.display = over ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void SetEnabled(Button button, bool enabled)
        {
            button.SetEnabled(enabled);
            button.style.backgroundColor = enabled ? ButtonColor : DisabledColor;
            button.style.opacity = enabled ? 1f : 0.55f;
        }

        private static VisualElement Panel(VisualElement root)
        {
            var panel = new VisualElement();
            panel.style.width = 270;
            panel.style.marginLeft = panel.style.marginRight = 12;
            panel.style.marginTop = panel.style.marginBottom = 12;
            panel.style.paddingLeft = panel.style.paddingRight = 14;
            panel.style.paddingTop = panel.style.paddingBottom = 14;
            panel.style.backgroundColor = PanelColor;
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius = 8;
            panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 8;
            panel.style.alignSelf = Align.FlexStart;
            root.Add(panel);
            return panel;
        }

        private static void Title(VisualElement parent, string text)
        {
            var label = Text(parent);
            label.text = text;
            label.style.fontSize = 22;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = AccentColor;
            label.style.marginBottom = 8;
        }

        private static Label Text(VisualElement parent)
        {
            var label = new Label();
            label.style.marginBottom = 4;
            parent.Add(label);
            return label;
        }

        private static Button MakeButton(VisualElement parent, string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.color = TextColor;
            button.style.fontSize = 15;
            button.style.minHeight = 36;
            button.style.marginTop = 4;
            button.style.paddingLeft = 10;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 6;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 6;
            button.style.borderLeftWidth = 5;
            button.style.borderLeftColor = ButtonColor;
            button.style.borderRightWidth = button.style.borderTopWidth = button.style.borderBottomWidth = 0;
            parent.Add(button);
            return button;
        }
    }
}
