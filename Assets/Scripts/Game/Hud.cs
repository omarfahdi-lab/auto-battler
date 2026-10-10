using System;
using System.Collections.Generic;
using System.Linq;
using AutoBattler.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace AutoBattler.Game
{
    /// <summary>
    /// UI Toolkit overlay built in code from the Tiny Swords UI pieces: run status, synergies and items on the left,
    /// the shop on the right, and a pick-one-of-three reward screen after wins.
    /// </summary>
    public sealed class Hud
    {
        private const float SliceScale = 0.5f;

        private static readonly Color LightText = new Color(1f, 0.97f, 0.88f);
        private static readonly Color DimText = new Color(0.93f, 0.86f, 0.74f, 0.75f);
        private static readonly Color InkText = new Color(0.25f, 0.17f, 0.12f);
        private static readonly Color MessageText = new Color(1f, 0.86f, 0.4f);
        private static readonly Color ActiveSynergy = new Color(1f, 0.84f, 0.3f);
        private static readonly Color Outline = new Color(0.12f, 0.08f, 0.06f);
        private static readonly Color Selected = new Color(1f, 0.84f, 0.3f);

        private readonly UIDocument _document;
        private readonly Label _round, _health, _gold, _level, _streak, _board, _message, _augments;
        private readonly Label[] _synergies = new Label[Synergies.All.Count];
        private readonly VisualElement _inventory;
        private readonly Button[] _shopButtons = new Button[Shop.SlotCount];
        private readonly VisualElement[] _portraits = new VisualElement[Shop.SlotCount];
        private readonly Label[] _shopLabels = new Label[Shop.SlotCount];
        private readonly Button _reroll, _xp, _sell, _fight, _restart, _volume, _speed;
        private readonly VisualElement _settingsRow;
        private readonly VisualElement _rewardOverlay, _rewardCards;
        private readonly Label _rewardTitle;

        public event Action<int> BuyClicked, ItemClicked, RewardChosen;
        public event Action RerollClicked, BuyXpClicked, SellClicked, FightClicked, RestartClicked, VolumeClicked, SpeedClicked;

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
            // Without a theme the root doesn't stretch to the screen; the reward overlay needs it to.
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            root.style.flexDirection = FlexDirection.Row;
            root.style.justifyContent = Justify.SpaceBetween;
            root.style.color = LightText;
            root.style.fontSize = 16;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);

            // ---- Left: run status ----
            var left = Panel(root);
            Ribbon(left, "AUTO BATTLER", row: 0);
            _round = Stat(left, Art.Icon(5));
            _health = Stat(left, Art.Icon(6));
            _gold = Stat(left, Art.Icon(3));
            _level = Stat(left, null);
            _streak = Stat(left, null);
            _board = Stat(left, null);

            Heading(left, "Synergies");
            for (int i = 0; i < _synergies.Length; i++) _synergies[i] = Text(left, 13, DimText);

            Heading(left, "Augments");
            _augments = Text(left, 13, LightText);

            Heading(left, "Items (click, then a unit)");
            _inventory = new VisualElement();
            _inventory.style.flexDirection = FlexDirection.Row;
            _inventory.style.flexWrap = Wrap.Wrap;
            _inventory.style.minHeight = 36;
            left.Add(_inventory);

            _message = Text(left, 14, MessageText);
            _message.style.marginTop = 8;
            var help = Text(left, 12, DimText);
            help.style.marginTop = 6;
            help.text = "1-5 buy · R reroll · F buy XP · S sell\nSpace fight · T speed · M mute · Right-click cancel";

            // ---- Right: shop ----
            var right = Panel(root);
            Ribbon(right, "SHOP", row: 1);
            for (int i = 0; i < Shop.SlotCount; i++) BuildShopCard(right, i);

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 6;
            right.Add(actions);
            _reroll = BigButton(actions, $"Reroll {Balance.RerollCost}g", red: false, () => RerollClicked?.Invoke());
            _xp = BigButton(actions, $"XP {Balance.XpCost}g", red: false, () => BuyXpClicked?.Invoke());
            _reroll.style.flexGrow = _xp.style.flexGrow = 1;
            _sell = BigButton(right, "Sell", red: true, () => SellClicked?.Invoke());
            _fight = BigButton(right, "FIGHT!", red: false, () => FightClicked?.Invoke());
            _fight.style.height = 54;
            _fight.style.fontSize = 22;
            _restart = BigButton(right, "New run", red: false, () => RestartClicked?.Invoke());

            _settingsRow = new VisualElement();
            _settingsRow.style.flexDirection = FlexDirection.Row;
            _settingsRow.style.marginTop = 6;
            right.Add(_settingsRow);
            _volume = BigButton(_settingsRow, "", red: false, () => VolumeClicked?.Invoke());
            _speed = BigButton(_settingsRow, "", red: false, () => SpeedClicked?.Invoke());
            foreach (var b in new[] { _volume, _speed })
            {
                b.style.flexGrow = 1;
                b.style.height = 36;
                b.style.fontSize = 14;
            }

            // ---- Reward picker (hidden until a win) ----
            _rewardOverlay = new VisualElement();
            _rewardOverlay.style.position = Position.Absolute;
            _rewardOverlay.style.left = _rewardOverlay.style.right = 0;
            _rewardOverlay.style.top = _rewardOverlay.style.bottom = 0;
            _rewardOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            _rewardOverlay.style.alignItems = Align.Center;
            _rewardOverlay.style.justifyContent = Justify.Center;
            _rewardOverlay.style.display = DisplayStyle.None;
            root.Add(_rewardOverlay);

            var rewardPanel = new VisualElement();
            ApplySlices(rewardPanel, Art.WoodPanel());
            rewardPanel.style.width = 700;
            rewardPanel.style.paddingLeft = rewardPanel.style.paddingRight = 30;
            rewardPanel.style.paddingTop = 20;
            rewardPanel.style.paddingBottom = 44;
            _rewardOverlay.Add(rewardPanel);
            _rewardTitle = Ribbon(rewardPanel, "CHOOSE A REWARD", row: 2);
            _rewardCards = new VisualElement();
            _rewardCards.style.flexDirection = FlexDirection.Row;
            _rewardCards.style.justifyContent = Justify.SpaceBetween;
            rewardPanel.Add(_rewardCards);
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

        public void ShowSettings(PlayerSettings settings)
        {
            _volume.text = settings.IsMuted ? "Sound off [M]" : $"Sound {settings.VolumePercent}% [M]";
            _speed.text = $"Speed {settings.BattleSpeed}x [T]";
        }

        public void ShowRewards(IReadOnlyList<RewardOption> options, bool bossReward)
        {
            _rewardTitle.text = bossReward ? "BOSS REWARD" : "CHOOSE A REWARD";
            _rewardCards.Clear();
            for (int i = 0; i < options.Count; i++) BuildRewardCard(options[i], i);
            _rewardOverlay.style.display = DisplayStyle.Flex;
        }

        public void HideRewards() => _rewardOverlay.style.display = DisplayStyle.None;

        public void Refresh(GameSession session, OwnedUnit selectedUnit, int selectedItem)
        {
            bool shopping = session.Phase == Phase.Shop;
            bool boss = EnemyWaves.IsBossRound(session.Round);
            _round.text = $"Round {session.Round} / {Balance.FinalRound}{(boss ? "  BOSS" : "")}";
            _health.text = $"Health {session.Health}";
            // Interest is paid on the gold you still hold when the round ends.
            _gold.text = $"Gold {session.Gold}  ·  +{Balance.Interest(session.Gold)} interest";
            _level.text = session.IsMaxLevel
                ? $"Level {session.Level} (max)"
                : $"Level {session.Level}   XP {session.Xp}/{session.XpToNextLevel}";
            _streak.text = session.Streak == 0
                ? "No streak"
                : $"{(session.Streak > 0 ? "Win" : "Loss")} streak {Math.Abs(session.Streak)}   (+{Balance.StreakBonus(Math.Abs(session.Streak))}g)";
            _board.text = $"Units on board {session.BoardCount} / {session.BoardCapacity}";

            var synergies = session.ActiveSynergies;
            for (int i = 0; i < _synergies.Length; i++)
            {
                var s = synergies[i];
                string next = s.NextThreshold > 0 ? $"/{s.NextThreshold}" : "";
                string effect = s.IsActive ? s.Bonus.Describe() : s.Definition.Bonuses[0].Describe() + " at 2";
                _synergies[i].text = $"{s.Definition.Name} {s.Count}{next}  ·  {effect}";
                _synergies[i].style.color = s.IsActive ? ActiveSynergy : DimText;
            }

            _augments.text = session.Augments.Count == 0
                ? "None yet: win rounds to earn them"
                : string.Join("\n", session.Augments.GroupBy(a => a.Name)
                    .Select(g => $"{g.Key}{(g.Count() > 1 ? $" x{g.Count()}" : "")}: {g.First().Description}"));

            RefreshInventory(session.Inventory, selectedItem, shopping);

            for (int i = 0; i < Shop.SlotCount; i++)
            {
                var def = session.Shop[i];
                _portraits[i].style.backgroundImage = def != null
                    ? new StyleBackground(Art.Unit(def, Team.Player).Portrait)
                    : new StyleBackground(StyleKeyword.None);
                _shopLabels[i].text = def == null ? "Sold" : Describe(i, def);
                SetEnabled(_shopButtons[i], shopping && session.CanBuy(i));
            }

            SetEnabled(_reroll, shopping && session.Gold >= Balance.RerollCost);
            SetEnabled(_xp, session.CanBuyXp);
            SetEnabled(_sell, shopping && selectedUnit != null);
            _sell.text = selectedUnit != null ? $"Sell {selectedUnit.Definition.Name} +{selectedUnit.SellValue}g" : "Sell";
            SetEnabled(_fight, shopping && session.BoardCount > 0);

            bool over = session.Phase == Phase.Victory || session.Phase == Phase.Defeat;
            _restart.style.display = over ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshInventory(IReadOnlyList<ItemDefinition> items, int selectedItem, bool shopping)
        {
            _inventory.Clear();
            if (items.Count == 0)
            {
                _inventory.Add(Text(null, 13, DimText, "No items"));
                return;
            }
            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                var item = items[i];
                var button = new Button(() => ItemClicked?.Invoke(index)) { tooltip = $"{item.Name}: {item.Description}" };
                ApplySlices(button, Art.Paper());
                ClearButtonChrome(button);
                button.style.width = button.style.height = 36;
                button.style.marginRight = 4;
                button.style.marginBottom = 4;
                button.style.paddingLeft = button.style.paddingRight = 0;
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.style.flexGrow = 1;
                icon.style.backgroundImage = new StyleBackground(Art.IconFor(item));
                button.Add(icon);
                if (i == selectedItem)
                {
                    button.style.borderLeftWidth = button.style.borderRightWidth = 2;
                    button.style.borderTopWidth = button.style.borderBottomWidth = 2;
                    button.style.borderLeftColor = button.style.borderRightColor = Selected;
                    button.style.borderTopColor = button.style.borderBottomColor = Selected;
                }
                SetEnabled(button, shopping);
                _inventory.Add(button);
            }
        }

        private void BuildRewardCard(RewardOption option, int index)
        {
            var card = new Button(() => RewardChosen?.Invoke(index));
            ApplySlices(card, Art.Paper());
            ClearButtonChrome(card);
            card.style.width = 196;
            card.style.height = 210;
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.Center;
            card.style.paddingTop = 14;

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.width = icon.style.height = 64;
            icon.style.backgroundImage = new StyleBackground(Art.IconFor(option));
            card.Add(icon);

            var kind = Text(card, 12, InkText, option.Kind == RewardKind.Item ? "ITEM" : "TEAM AUGMENT");
            kind.style.marginTop = 6;
            var name = Text(card, 18, InkText, option.Name);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (option.Greater) name.style.color = new Color(0.6f, 0.3f, 0.05f);
            var description = Text(card, 14, InkText, option.Description);
            description.style.unityTextAlign = TextAnchor.UpperCenter;
            description.style.marginLeft = description.style.marginRight = 10;
            foreach (var child in new VisualElement[] { kind, name, description }) child.pickingMode = PickingMode.Ignore;

            _rewardCards.Add(card);
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

        private static void SetEnabled(VisualElement button, bool enabled)
        {
            button.SetEnabled(enabled);
            button.style.opacity = enabled ? 1f : 0.5f;
        }

        private static VisualElement Panel(VisualElement root)
        {
            var panel = new VisualElement();
            ApplySlices(panel, Art.WoodPanel());
            panel.style.width = 290;
            panel.style.marginLeft = panel.style.marginRight = 8;
            panel.style.marginTop = panel.style.marginBottom = 8;
            panel.style.paddingLeft = panel.style.paddingRight = 24;
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 40;
            panel.style.alignSelf = Align.FlexStart;
            root.Add(panel);
            return panel;
        }

        private static Label Ribbon(VisualElement parent, string text, int row)
        {
            var label = new Label(text);
            ApplySlices(label, Art.Ribbon(row));
            label.style.height = 44;
            label.style.marginLeft = label.style.marginRight = -12;
            label.style.marginBottom = 6;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.fontSize = 20;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = LightText;
            Outlined(label);
            parent.Add(label);
            return label;
        }

        private static void Heading(VisualElement parent, string text)
        {
            var label = Text(parent, 13, MessageText, text.ToUpperInvariant());
            label.style.marginTop = 6;
            label.style.marginBottom = 2;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private static Label Stat(VisualElement parent, Texture2D icon)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 24;
            parent.Add(row);
            var image = new VisualElement();
            image.style.width = image.style.height = 24;
            image.style.marginRight = 6;
            if (icon != null) image.style.backgroundImage = new StyleBackground(icon);
            row.Add(image);
            var label = Text(row, 16, LightText);
            label.style.marginBottom = 0;
            Outlined(label);
            return label;
        }

        private static Label Text(VisualElement parent, int size, Color color, string text = "")
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 2;
            parent?.Add(label);
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
            button.style.fontSize = 16;
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
