using System;
using AutoBattler.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace AutoBattler.Game
{
    /// <summary>
    /// Start and pause menu: play or continue, watch the balance bot play, how to play, settings and credits.
    /// Built from the same Tiny Swords pieces as the HUD.
    /// </summary>
    public sealed class StartMenu
    {
        private enum Page
        {
            Main,
            HowToPlay,
            Settings,
            Credits,
        }

        private readonly VisualElement _overlay;
        private readonly VisualElement _main, _howTo, _settings, _credits;
        private readonly Button _play, _newRun, _volume, _speed;
        private readonly Label _subtitle;
        private Page _page;

        public event Action PlayClicked, NewRunClicked, WatchAiClicked, VolumeClicked, SpeedClicked;

        public StartMenu(VisualElement root)
        {
            _overlay = Hud.Overlay(root, 0.7f);

            var panel = new VisualElement();
            Hud.ApplySlices(panel, Art.WoodPanel());
            panel.style.width = 560;
            panel.style.paddingLeft = panel.style.paddingRight = 40;
            panel.style.paddingTop = 22;
            panel.style.paddingBottom = 60;
            _overlay.Add(panel);

            var title = Hud.Ribbon(panel, "AUTO BATTLER", row: 0);
            title.style.height = 60;
            title.style.fontSize = 30;
            _subtitle = Hud.Text(panel, 16, Hud.MessageText, "Build an army, let it fight, beat the final boss.");
            _subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _subtitle.style.marginBottom = 10;

            _main = PageContainer(panel);
            _play = MenuButton(_main, "PLAY", () => PlayClicked?.Invoke(), big: true);
            _newRun = MenuButton(_main, "NEW RUN", () => NewRunClicked?.Invoke());
            MenuButton(_main, "WATCH THE AI PLAY", () => WatchAiClicked?.Invoke());
            MenuButton(_main, "HOW TO PLAY", () => Show(Page.HowToPlay));
            MenuButton(_main, "SETTINGS", () => Show(Page.Settings));
            MenuButton(_main, "CREDITS", () => Show(Page.Credits));

            _howTo = PageContainer(panel);
            Hud.Heading(_howTo, "How to play");
            foreach (var line in new[]
            {
                "Buy units in the shop, then click a unit and a tile to place it on your (blue) half.",
                "Press FIGHT. Units battle on their own; losing costs health.",
                "Three copies of a unit merge into a stronger star level.",
                "Field 2 or 4 of a class for a synergy bonus.",
                "Win rounds to pick items and team augments. Rounds 5 and 10 are bosses: beat round 10 to win.",
                "Buy XP to field more units and find 3g units more often. Gold pays interest, but spending usually wins.",
            })
            {
                // The built-in runtime font has no bullet glyph, so use a dash.
                Hud.Text(_howTo, 14, Hud.LightText, "-  " + line).style.marginBottom = 4;
            }
            var keys = Hud.Text(_howTo, 12, Hud.DimText,
                "Keys: 1-5 buy · R reroll · F XP · S sell · Space fight · T speed · M mute · Esc menu");
            keys.style.marginTop = 8;
            keys.style.unityTextAlign = TextAnchor.MiddleCenter;
            BackButton(_howTo);

            _settings = PageContainer(panel);
            Hud.Heading(_settings, "Settings");
            _volume = MenuButton(_settings, "", () => VolumeClicked?.Invoke());
            _speed = MenuButton(_settings, "", () => SpeedClicked?.Invoke());
            Hud.Text(_settings, 12, Hud.DimText, "Saved between sessions. M mutes and T toggles speed at any time.")
                .style.unityTextAlign = TextAnchor.MiddleCenter;
            BackButton(_settings);

            _credits = PageContainer(panel);
            Hud.Heading(_credits, "Credits");
            foreach (var line in new[]
            {
                "Art: Tiny Swords by Pixel Frog (pixelfrog-assets.itch.io)",
                "Sound: RPG Audio, Impact Sounds, Interface Sounds and Music Jingles by Kenney (kenney.nl), CC0",
                "Made with Unity 6 (URP 2D) and UI Toolkit",
            })
            {
                Hud.Text(_credits, 14, Hud.LightText, line).style.marginBottom = 6;
            }
            BackButton(_credits);

            Open(runInProgress: false);
        }

        public bool IsVisible => _overlay.style.display == DisplayStyle.Flex;
        public bool IsOnMainPage => _page == Page.Main;

        /// <summary>Opens the menu on its main page. Mid-run it offers Continue and New Run instead of Play.</summary>
        public void Open(bool runInProgress)
        {
            _play.text = runInProgress ? "CONTINUE" : "PLAY";
            _subtitle.text = runInProgress ? "Paused" : "Build an army, let it fight, beat the final boss.";
            _newRun.style.display = runInProgress ? DisplayStyle.Flex : DisplayStyle.None;
            Show(Page.Main);
            _overlay.style.display = DisplayStyle.Flex;
        }

        public void Close() => _overlay.style.display = DisplayStyle.None;

        /// <summary>Returns to the main page from a sub-page.</summary>
        public void Back() => Show(Page.Main);

        public void ShowSettings(PlayerSettings settings)
        {
            _volume.text = settings.IsMuted ? "SOUND: OFF" : $"SOUND: {settings.VolumePercent}%";
            _speed.text = $"BATTLE SPEED: {settings.BattleSpeed}x";
        }

        private void Show(Page page)
        {
            _page = page;
            _main.style.display = page == Page.Main ? DisplayStyle.Flex : DisplayStyle.None;
            _howTo.style.display = page == Page.HowToPlay ? DisplayStyle.Flex : DisplayStyle.None;
            _settings.style.display = page == Page.Settings ? DisplayStyle.Flex : DisplayStyle.None;
            _credits.style.display = page == Page.Credits ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static VisualElement PageContainer(VisualElement parent)
        {
            var page = new VisualElement();
            parent.Add(page);
            return page;
        }

        private static Button MenuButton(VisualElement parent, string text, Action onClick, bool big = false)
        {
            var button = Hud.BigButton(parent, text, red: false, onClick);
            button.style.height = big ? 56 : 44;
            button.style.fontSize = big ? 22 : 16;
            button.style.marginLeft = button.style.marginRight = 90;
            return button;
        }

        private void BackButton(VisualElement page)
        {
            var back = MenuButton(page, "BACK  [Esc]", () => Show(Page.Main));
            back.style.marginTop = 12;
        }
    }
}
