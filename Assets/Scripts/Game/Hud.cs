using System;
using AutoBattler.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace AutoBattler.Game
{
    /// <summary>UI Toolkit overlay built in code from the Tiny Swords UI pieces: stats on the left, shop on the right.</summary>
    public sealed class Hud
    {
        private const float SliceScale = 0.5f;

        private static readonly Color LightText = new Color(1f, 0.97f, 0.88f);
        private static readonly Color InkText = new Color(0.25f, 0.17f, 0.12f);
        private static readonly Color MessageText = new Color(1f, 0.86f, 0.4f);
        private static readonly Color Outline = new Color(0.12f, 0.08f, 0.06f);

        private readonly UIDocument _document;
        private readonly Label _round, _health, _gold, _board, _message;
        private readonly Button[] _shopButtons = new Button[Shop.SlotCount];
        private readonly VisualElement[] _portraits = new VisualElement[Shop.SlotCount];
        private readonly Label[] _shopLabels = new Label[Shop.SlotCount];
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
            root.style.color = LightText;
            root.style.fontSize = 18;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);

            var left = Panel(root);
            Ribbon(left, "AUTO BATTLER", row: 0);
            _round = Stat(left, Art.Icon(5));
            _health = Stat(left, Art.Icon(6));
            _gold = Stat(left, Art.Icon(3));
            _board = Stat(left, null);
            _message = Text(left, 16, MessageText);
            _message.style.marginTop = 12;
            var help = Text(left, 13, new Color(0.93f, 0.86f, 0.74f));
            help.style.marginTop = 12;
            help.text = "Click a unit, then a cell or bench slot to place it.\n" +
                        "1-5 buy  ·  R reroll  ·  S sell\nSpace fight  ·  Right-click cancel";

            var right = Panel(root);
            Ribbon(right, "SHOP", row: 1);
            for (int i = 0; i < Shop.SlotCount; i++) BuildShopCard(right, i);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 6;
            right.Add(actions);
            _reroll = BigButton(actions, $"Reroll {GameSession.RerollCost}g", red: false, () => RerollClicked?.Invoke());
            _sell = BigButton(actions, "Sell", red: true, () => SellClicked?.Invoke());
            _reroll.style.flexGrow = _sell.style.flexGrow = 1;
            _fight = BigButton(right, "FIGHT!", red: false, () => FightClicked?.Invoke());
            _fight.style.height = 54;
            _fight.style.fontSize = 22;
            _restart = BigButton(right, "New run", red: false, () => RestartClicked?.Invoke());
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
            _health.text = $"Health {session.Health}";
            _gold.text = $"Gold {session.Gold}";
            _board.text = $"Units on board {session.BoardCount} / {session.BoardCapacity}";

            for (int i = 0; i < Shop.SlotCount; i++)
            {
                var def = session.Shop[i];
                _portraits[i].style.backgroundImage = def != null
                    ? new StyleBackground(Art.Unit(def, Team.Player).Portrait)
                    : new StyleBackground(StyleKeyword.None);
                _shopLabels[i].text = def == null ? "Sold" : Describe(i, def);
                SetEnabled(_shopButtons[i], shopping && session.CanBuy(i));
            }

            SetEnabled(_reroll, shopping && session.Gold >= GameSession.RerollCost);
            SetEnabled(_sell, shopping && selected != null);
            _sell.text = selected != null ? $"Sell +{selected.SellValue}g" : "Sell";
            SetEnabled(_fight, shopping && session.BoardCount > 0);

            bool over = session.Phase == Phase.Victory || session.Phase == Phase.Defeat;
            _restart.style.display = over ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string Describe(int slot, UnitDefinition def)
        {
            string power = def.Role == UnitRole.Healer ? $"Heal {def.Power}" : $"Atk {def.Power}";
            return $"[{slot + 1}] {def.Name} · {def.Cost}g\nHP {def.MaxHp}  {power}  Rng {def.Range}";
        }

        private void BuildShopCard(VisualElement parent, int slot)
        {
            var card = new Button(() => BuyClicked?.Invoke(slot));
            ApplySlices(card, Art.Paper());
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            card.style.height = 58;
            card.style.marginTop = 3;
            card.style.paddingLeft = 6;
            ClearButtonChrome(card);

            var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
            portrait.style.width = portrait.style.height = 48;
            portrait.style.flexShrink = 0;
            card.Add(portrait);

            var label = Text(card, 14, InkText);
            label.pickingMode = PickingMode.Ignore;
            label.style.marginLeft = 6;
            label.style.marginBottom = 0;

            parent.Add(card);
            _shopButtons[slot] = card;
            _portraits[slot] = portrait;
            _shopLabels[slot] = label;
        }

        private static void SetEnabled(Button button, bool enabled)
        {
            button.SetEnabled(enabled);
            button.style.opacity = enabled ? 1f : 0.5f;
        }

        private static VisualElement Panel(VisualElement root)
        {
            var panel = new VisualElement();
            ApplySlices(panel, Art.WoodPanel());
            panel.style.width = 280;
            panel.style.marginLeft = panel.style.marginRight = 8;
            panel.style.marginTop = panel.style.marginBottom = 8;
            panel.style.paddingLeft = panel.style.paddingRight = 26;
            panel.style.paddingTop = 18;
            panel.style.paddingBottom = 42;
            panel.style.alignSelf = Align.FlexStart;
            root.Add(panel);
            return panel;
        }

        private static void Ribbon(VisualElement parent, string text, int row)
        {
            var label = new Label(text);
            ApplySlices(label, Art.Ribbon(row));
            label.style.height = 44;
            label.style.marginLeft = label.style.marginRight = -12;
            label.style.marginBottom = 8;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = 20;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = LightText;
            Outlined(label);
            parent.Add(label);
        }

        private static Label Stat(VisualElement parent, Texture2D icon)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 30;
            parent.Add(row);
            if (icon != null)
            {
                var image = new VisualElement();
                image.style.backgroundImage = new StyleBackground(icon);
                image.style.width = image.style.height = 30;
                image.style.marginRight = 6;
                row.Add(image);
            }
            var label = Text(row, 18, LightText);
            label.style.marginBottom = 0;
            Outlined(label);
            return label;
        }

        private static Label Text(VisualElement parent, int size, Color color)
        {
            var label = new Label();
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 4;
            parent.Add(label);
            return label;
        }

        private static Button BigButton(VisualElement parent, string text, bool red, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            var regular = Art.BigButton(red, pressed: false);
            var pressed = Art.BigButton(red, pressed: true);
            ApplySlices(button, regular);
            ClearButtonChrome(button);
            button.style.height = 44;
            button.style.marginTop = 4;
            button.style.marginLeft = button.style.marginRight = 2;
            button.style.fontSize = 17;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.color = LightText;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            Outlined(button);

            // Swap to the pressed artwork while held.
            button.RegisterCallback<PointerDownEvent>(_ => ApplySlices(button, pressed), TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(_ => ApplySlices(button, regular));
            button.RegisterCallback<PointerLeaveEvent>(_ => ApplySlices(button, regular));
            parent.Add(button);
            return button;
        }

        private static void ApplySlices(VisualElement element, SlicedTexture art)
        {
            element.style.backgroundImage = new StyleBackground(art.Texture);
            element.style.unitySliceLeft = art.Left;
            element.style.unitySliceRight = art.Right;
            element.style.unitySliceTop = art.Top;
            element.style.unitySliceBottom = art.Bottom;
            element.style.unitySliceScale = SliceScale;
        }

        private static void ClearButtonChrome(VisualElement button)
        {
            button.style.backgroundColor = Color.clear;
            button.style.borderLeftWidth = button.style.borderRightWidth = 0;
            button.style.borderTopWidth = button.style.borderBottomWidth = 0;
        }

        private static void Outlined(VisualElement element)
        {
            element.style.unityTextOutlineColor = Outline;
            element.style.unityTextOutlineWidth = 0.15f;
        }
    }
}
