using System.Collections.Generic;
using AutoBattler.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoBattler.Game
{
    /// <summary>Drives a GameSession: draws the board, handles input, and plays battles back tick by tick.</summary>
    public sealed class GameController : MonoBehaviour
    {
        private const float TickSeconds = 1f / BattleSimulator.TicksPerSecond;
        private const float EndOfBattlePause = 1.2f;
        private const float BenchY = -1.6f;

        private static readonly Color PlayerTint = new Color(0.25f, 0.45f, 1f, 0.12f);
        private static readonly Color EnemyTint = new Color(1f, 0.25f, 0.2f, 0.12f);
        private static readonly Color GridLine = new Color(0.1f, 0.2f, 0.05f, 0.18f);
        private static readonly Color SlotMark = new Color(0.1f, 0.2f, 0.05f, 0.15f);
        private static readonly Color HighlightTile = new Color(1f, 0.9f, 0.35f, 0.4f);

        private const string SettingsKey = "autobattler.settings";

        private Camera _camera;
        private AudioManager _audio;
        private CameraShake _shake;
        private PlayerSettings _settings;
        private Hud _hud;
        private Transform _unitRoot;
        private SpriteRenderer _highlight;
        private GameSession _session;
        private int _seed;

        private readonly Dictionary<Slot, UnitView> _slotViews = new Dictionary<Slot, UnitView>();
        private readonly Dictionary<int, UnitView> _battleViews = new Dictionary<int, UnitView>();
        private Slot? _selected;
        private int _selectedItem = -1;

        private BattleSimulator _battle;
        private float _tickTimer;
        private float _endTimer;

        public void Initialize(Camera camera, int seed, AudioManager audio)
        {
            _camera = camera;
            _audio = audio;
            _seed = seed;
            // Frame the 8x6 board plus bench; side panels fit in the remaining width.
            _camera.orthographicSize = 5.2f;
            _shake = _camera.gameObject.AddComponent<CameraShake>();
            _shake.SetRestPosition(new Vector3(0f, 1.0f, -10f));
            _camera.backgroundColor = Art.WaterColor();

            BuildTiles();
            _unitRoot = new GameObject("Units").transform;
            _unitRoot.SetParent(transform, false);

            _hud = new Hud(transform);
            _hud.BuyClicked += Buy;
            _hud.RerollClicked += Reroll;
            _hud.SellClicked += SellSelected;
            _hud.FightClicked += StartBattle;
            _hud.RestartClicked += NewRun;
            _hud.BuyXpClicked += BuyXp;
            _hud.ItemClicked += SelectItem;
            _hud.RewardChosen += ChooseReward;
            _hud.VolumeClicked += CycleVolume;
            _hud.SpeedClicked += ToggleSpeed;
            _hud.PlayClicked += StartFromTitle;

            _settings = PlayerSettings.Deserialize(PlayerPrefs.GetString(SettingsKey, ""));
            ApplySettings();

            NewRun();
        }

        private void NewRun()
        {
            _session = new GameSession(new SeededRandom(_seed++));
            _battle = null;
            Time.timeScale = 1f;
            _selected = null;
            _selectedItem = -1;
            ClearBattleViews();
            _hud.HideRewards();
            _hud.SetMessage("Buy units and place them on the blue side.");
            RefreshShopPhase();
        }

        // ---- Layout -------------------------------------------------------------------------------------------

        private static Vector3 CellToWorld(GridPos p) =>
            new Vector3(p.X - (BoardLayout.Width - 1) / 2f, p.Y + 0.4f, 0f);

        private static Vector3 BenchToWorld(int index) =>
            new Vector3(index - (GameSession.BenchSize - 1) / 2f, BenchY, 0f);

        private static Vector3 SlotToWorld(Slot slot) =>
            slot.Area == SlotArea.Bench ? BenchToWorld(slot.Index) : CellToWorld(slot.BoardPos);

        private static Slot? WorldToSlot(Vector2 world)
        {
            int benchIndex = Mathf.RoundToInt(world.x + (GameSession.BenchSize - 1) / 2f);
            if (Mathf.Abs(world.y - BenchY) <= 0.5f && benchIndex >= 0 && benchIndex < GameSession.BenchSize)
                return Slot.Bench(benchIndex);

            var cell = new GridPos(Mathf.RoundToInt(world.x + (BoardLayout.Width - 1) / 2f),
                Mathf.RoundToInt(world.y - 0.4f));
            return BoardLayout.IsPlayerCell(cell) ? Slot.Board(cell) : (Slot?)null;
        }

        private void BuildTiles()
        {
            var tiles = new GameObject("Terrain").transform;
            tiles.SetParent(transform, false);

            // The board is one grass island built from the tileset's 3x3 flat-ground pieces.
            for (int y = 0; y < BoardLayout.Height; y++)
            for (int x = 0; x < BoardLayout.Width; x++)
            {
                int col = x == 0 ? 0 : x == BoardLayout.Width - 1 ? 2 : 1;
                int row = y == BoardLayout.Height - 1 ? 0 : y == 0 ? 2 : 1;
                Tile(tiles, $"Cell {x},{y}", Art.GrassTile(col, row), CellToWorld(new GridPos(x, y)));
            }

            // Tint each half by team and draw faint cell lines so the grid reads on top of the grass.
            float halfHeight = BoardLayout.Height / 2f;
            var boardCenter = (CellToWorld(new GridPos(0, 0)) + CellToWorld(new GridPos(BoardLayout.Width - 1, BoardLayout.Height - 1))) / 2f;
            SpriteFactory.CreateChild(tiles, "PlayerTint", SpriteFactory.Square, PlayerTint, 1,
                boardCenter + Vector3.down * halfHeight / 2f, new Vector2(BoardLayout.Width - 0.3f, halfHeight - 0.15f));
            SpriteFactory.CreateChild(tiles, "EnemyTint", SpriteFactory.Square, EnemyTint, 1,
                boardCenter + Vector3.up * halfHeight / 2f, new Vector2(BoardLayout.Width - 0.3f, halfHeight - 0.15f));
            for (int x = 1; x < BoardLayout.Width; x++)
                SpriteFactory.CreateChild(tiles, "GridLine", SpriteFactory.Square, GridLine, 1,
                    new Vector3(boardCenter.x + x - BoardLayout.Width / 2f, boardCenter.y, 0f),
                    new Vector2(0.03f, BoardLayout.Height - 0.4f));
            for (int y = 1; y < BoardLayout.Height; y++)
                SpriteFactory.CreateChild(tiles, "GridLine", SpriteFactory.Square, GridLine, 1,
                    new Vector3(boardCenter.x, boardCenter.y + y - BoardLayout.Height / 2f, 0f),
                    new Vector2(BoardLayout.Width - 0.4f, y == BoardLayout.PlayerRows ? 0.06f : 0.03f));

            // The bench is a one-tile-high grass strip below the board.
            for (int i = 0; i < GameSession.BenchSize; i++)
            {
                int piece = i == 0 ? 0 : i == GameSession.BenchSize - 1 ? 2 : 1;
                Tile(tiles, $"Bench {i}", Art.BenchTile(piece), BenchToWorld(i));
                SpriteFactory.CreateChild(tiles, "BenchSlot", SpriteFactory.Square, SlotMark, 1, BenchToWorld(i),
                    Vector2.one * 0.78f);
            }

            _highlight = SpriteFactory.CreateChild(transform, "Highlight", SpriteFactory.Square, HighlightTile, 2,
                Vector3.zero, Vector2.one * 0.92f);
            _highlight.enabled = false;
        }

        private static void Tile(Transform parent, string name, Sprite sprite, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            SpriteFactory.AddRenderer(go, sprite, Color.white, 0);
        }

        // ---- Shop phase ---------------------------------------------------------------------------------------

        private void Buy(int shopSlot)
        {
            if (_session.TryBuy(shopSlot))
            {
                _audio.Play(Sfx.Buy);
                RefreshShopPhase();
            }
            else if (_session.Phase == Phase.Shop)
            {
                _audio.Play(Sfx.Error);
                _hud.SetMessage("Can't buy that: not enough gold or bench space.");
            }
        }

        private void Reroll()
        {
            if (_session.Phase != Phase.Shop) return;
            if (_session.TryReroll())
            {
                _audio.Play(Sfx.Reroll);
                RefreshShopPhase();
            }
            else _audio.Play(Sfx.Error);
        }

        private void BuyXp()
        {
            if (_session.Phase != Phase.Shop) return;
            int level = _session.Level;
            if (!_session.TryBuyXp())
            {
                _audio.Play(Sfx.Error);
                return;
            }
            _audio.Play(Sfx.Xp);
            if (_session.Level > level)
            {
                _audio.Play(Sfx.Confirm);
                _hud.SetMessage($"Level {_session.Level}! You can field {_session.BoardCapacity} units, and 3g units show up more often.");
            }
            RefreshShopPhase();
        }

        private void SelectItem(int index)
        {
            if (_session.Phase != Phase.Shop) return;
            _selected = null;
            _selectedItem = _selectedItem == index ? -1 : index;
            _audio.Play(Sfx.Click);
            if (_selectedItem >= 0) _hud.SetMessage($"Click a unit to give it the {_session.Inventory[index].Name}.");
            RefreshShopPhase();
        }

        private void ChooseReward(int index)
        {
            var options = _session.PendingRewards;
            if (index < 0 || index >= options.Count) return;
            var choice = options[index];
            if (!_session.ChooseReward(index)) return;
            _audio.Play(Sfx.Confirm);
            _hud.HideRewards();
            _hud.SetMessage(choice.Kind == RewardKind.Item
                ? $"Got the {choice.Name}. Click it under Items, then a unit to equip it."
                : $"{choice.Name} is active for the rest of the run.");
            RefreshShopPhase();
        }

        private void SellSelected()
        {
            if (_selected.HasValue && _session.TrySell(_selected.Value))
            {
                _audio.Play(Sfx.Sell);
                _selected = null;
                RefreshShopPhase();
            }
        }

        private void ClickSlot(Slot slot)
        {
            if (_session.Phase != Phase.Shop) return;
            _audio.Play(Sfx.Click);

            if (_selectedItem >= 0)
            {
                var item = _session.Inventory[_selectedItem];
                if (_session.TryEquip(_selectedItem, slot))
                    _hud.SetMessage($"{_session.Get(slot).Definition.Name} equipped the {item.Name}.");
                _selectedItem = -1;
            }
            else if (_selected.HasValue)
            {
                var from = _selected.Value;
                _selected = null;
                if (!from.Equals(slot) && !_session.TryMove(from, slot))
                {
                    _audio.Play(Sfx.Error);
                    _hud.SetMessage($"Board is full ({_session.BoardCapacity} units this round).");
                }
            }
            else if (_session.Get(slot) != null)
            {
                _selected = slot;
            }
            RefreshShopPhase();
        }

        private void RefreshShopPhase()
        {
            foreach (var view in _slotViews.Values) Destroy(view.gameObject);
            _slotViews.Clear();

            if (_session.Phase == Phase.Shop)
            {
                foreach (var slot in _session.OccupiedSlots())
                {
                    var unit = _session.Get(slot);
                    var view = UnitView.Create(_unitRoot, unit, Team.Player, SlotToWorld(slot));
                    view.SetHealth(unit.MaxHp, unit.MaxHp);
                    view.SetSelected(_selected.HasValue && _selected.Value.Equals(slot));
                    _slotViews[slot] = view;
                }
            }

            _highlight.enabled = _selected.HasValue;
            if (_selected.HasValue) _highlight.transform.position = SlotToWorld(_selected.Value);
            _hud.Refresh(_session, _selected.HasValue ? _session.Get(_selected.Value) : null, _selectedItem);
        }

        // ---- Battle phase -------------------------------------------------------------------------------------

        private void StartBattle()
        {
            if (_session.Phase != Phase.Shop) return;
            _battle = _session.StartBattle();
            if (_battle == null)
            {
                _audio.Play(Sfx.Error);
                _hud.SetMessage("Place at least one unit on the board first.");
                return;
            }
            _audio.PlayJingle(EnemyWaves.IsBossRound(_session.Round) ? Jingle.BossRound : Jingle.RoundStart);
            Time.timeScale = _settings.BattleSpeed;

            _selected = null;
            _selectedItem = -1;
            RefreshShopPhase(); // clears shop-phase views since the phase is now Battle
            foreach (var c in _battle.Combatants)
            {
                var view = UnitView.Create(_unitRoot, c.Unit, c.Team, CellToWorld(c.Position), c.IsBoss);
                view.SetHealth(c.Hp, c.MaxHp);
                _battleViews[c.Id] = view;
            }
            _tickTimer = 0f;
            _endTimer = EndOfBattlePause;
            _hud.SetMessage(EnemyWaves.IsBossRound(_session.Round)
                ? $"Round {_session.Round}: BOSS FIGHT!"
                : $"Round {_session.Round}: fight!");
        }

        private void UpdateBattle()
        {
            if (!_battle.IsOver)
            {
                _tickTimer += Time.deltaTime;
                while (_tickTimer >= TickSeconds && !_battle.IsOver)
                {
                    _tickTimer -= TickSeconds;
                    foreach (var e in _battle.Step()) Apply(e);
                }
                return;
            }

            _endTimer -= Time.deltaTime;
            if (_endTimer > 0f) return;

            int round = _session.Round;
            var result = _session.CompleteBattle(_battle);
            _battle = null;
            Time.timeScale = 1f;
            ClearBattleViews();
            _hud.SetMessage(Describe(result));
            if (_session.Phase == Phase.Victory) _audio.PlayJingle(Jingle.Victory);
            else if (_session.Phase == Phase.Defeat) _audio.PlayJingle(Jingle.Defeat);
            if (_session.Phase == Phase.Reward)
                _hud.ShowRewards(_session.PendingRewards, bossReward: EnemyWaves.IsBossRound(round));
            RefreshShopPhase();
        }

        private void Apply(BattleEvent e)
        {
            _battleViews.TryGetValue(e.SourceId, out var source);
            UnitView target = null;
            if (e.TargetId >= 0) _battleViews.TryGetValue(e.TargetId, out target);

            switch (e.Type)
            {
                case BattleEventType.Move:
                    if (source != null) source.MoveTo(CellToWorld(e.To), e.Ticks * TickSeconds);
                    break;
                case BattleEventType.ActionStart:
                    if (source != null) source.PlayAction(CellToWorld(e.To));
                    PlaySwing(_battle.Find(e.SourceId));
                    break;
                case BattleEventType.ProjectileLaunch:
                    _audio.Play(Sfx.BowRelease);
                    if (source != null && target != null)
                        ArrowView.Launch(_unitRoot, Art.Arrow(_battle.Find(e.SourceId).Team),
                            source.transform.position + ArrowView.ArrowHeight, target.transform, e.Ticks * TickSeconds);
                    break;
                case BattleEventType.Damage:
                    var attacker = _battle.Find(e.SourceId);
                    PlayHit(attacker);
                    if (attacker != null && attacker.IsBoss) _shake.Shake(0.12f, 0.22f);
                    if (target != null)
                    {
                        var c = _battle.Find(e.TargetId);
                        target.SetHealth(c.Hp, c.MaxHp);
                        target.Hit();
                    }
                    break;
                case BattleEventType.Heal:
                    if (target != null)
                    {
                        var c = _battle.Find(e.TargetId);
                        target.SetHealth(c.Hp, c.MaxHp);
                        _audio.Play(Sfx.Heal, 0.7f);
                        OneShotEffect.Play(_unitRoot, Art.HealEffect(c.Team), target.transform.position + Vector3.up * 0.2f, 6000);
                    }
                    break;
                case BattleEventType.Death:
                    _audio.Play(Sfx.Death);
                    if (_battle.Find(e.SourceId)?.IsBoss == true) _shake.Shake(0.3f, 0.5f);
                    if (source != null)
                    {
                        source.Die();
                        _battleViews.Remove(e.SourceId);
                    }
                    break;
            }
        }

        private void PlaySwing(Combatant attacker)
        {
            if (attacker == null) return;
            if (attacker.Definition == UnitCatalog.Warrior) _audio.Play(Sfx.WarriorSwing, 0.8f);
            else if (attacker.Definition == UnitCatalog.Lancer) _audio.Play(Sfx.LancerSwing, 0.8f);
        }

        private void PlayHit(Combatant attacker)
        {
            if (attacker == null) return;
            if (attacker.Definition.UsesProjectile) _audio.Play(Sfx.ArrowImpact);
            else if (attacker.Definition == UnitCatalog.Lancer || attacker.IsBoss) _audio.Play(Sfx.HeavyHit);
            else _audio.Play(Sfx.MeleeHit);
        }

        private void ClearBattleViews()
        {
            foreach (var view in _battleViews.Values) Destroy(view.gameObject);
            _battleViews.Clear();
        }

        private string Describe(RoundResult result)
        {
            switch (_session.Phase)
            {
                case Phase.Victory: return "VICTORY! You beat the final boss.";
                case Phase.Defeat:
                    return _session.Health > 0
                        ? "DEFEAT. The final boss held the field."
                        : $"DEFEAT. You lost {result.DamageTaken} health and fell in round {_session.Round}.";
                default:
                    string income = $"+{result.GoldEarned}g ({Balance.BaseIncome} base";
                    if (result.Interest > 0) income += $", {result.Interest} interest";
                    if (result.StreakBonus > 0) income += $", {result.StreakBonus} streak";
                    int augmentGold = result.GoldEarned - Balance.BaseIncome - result.Interest - result.StreakBonus;
                    if (augmentGold > 0) income += $", {augmentGold} augments";
                    income += ")";
                    return result.Won
                        ? $"Round won! {income}"
                        : $"Round lost: -{result.DamageTaken} health. {income}";
            }
        }

        private void StartFromTitle()
        {
            if (!_hud.IsTitleVisible) return;
            _hud.HideTitle();
            _audio.Play(Sfx.Confirm);
        }

        // ---- Settings ------------------------------------------------------------------------------------------

        private void CycleVolume()
        {
            _settings.CycleVolume();
            SaveSettings();
            _audio.Play(Sfx.Click);
        }

        private void ToggleMute()
        {
            _settings.ToggleMute();
            SaveSettings();
            _audio.Play(Sfx.Click);
        }

        private void ToggleSpeed()
        {
            _settings.ToggleBattleSpeed();
            SaveSettings();
            _audio.Play(Sfx.Click);
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetString(SettingsKey, _settings.Serialize());
            PlayerPrefs.Save();
            ApplySettings();
        }

        private void ApplySettings()
        {
            AudioManager.SetMasterVolume(_settings.VolumePercent);
            _hud.ShowSettings(_settings);
            // Speed only applies while a battle plays out; everything in it runs on scaled time, so impact frames
            // still line up with the simulation at 2x.
            Time.timeScale = _battle != null ? _settings.BattleSpeed : 1f;
        }

        // ---- Input --------------------------------------------------------------------------------------------

        private void Update()
        {
            if (_session == null) return;
            if (_battle != null) UpdateBattle();
            HandleKeyboard();
            HandleMouse();
        }

        private void HandleKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.mKey.wasPressedThisFrame) ToggleMute();
            if (kb.tKey.wasPressedThisFrame) ToggleSpeed();

            if (_hud.IsTitleVisible)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) StartFromTitle();
                return;
            }

            if (_session.Phase == Phase.Reward)
            {
                for (int i = 0; i < _session.PendingRewards.Count; i++)
                    if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
                        ChooseReward(i);
                return;
            }
            if (_session.Phase != Phase.Shop) return;

            for (int i = 0; i < Shop.SlotCount; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
                    Buy(i);
            if (kb.rKey.wasPressedThisFrame) Reroll();
            if (kb.fKey.wasPressedThisFrame) BuyXp();
            if (kb.sKey.wasPressedThisFrame) SellSelected();
            if (kb.spaceKey.wasPressedThisFrame) StartBattle();
            if (kb.escapeKey.wasPressedThisFrame) Deselect();
        }

        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || _session.Phase != Phase.Shop || _hud.IsTitleVisible) return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                Deselect();
                return;
            }
            if (!mouse.leftButton.wasPressedThisFrame) return;

            var screen = mouse.position.ReadValue();
            if (_hud.IsPointerOverUi(screen)) return;

            var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            var slot = WorldToSlot(world);
            if (slot.HasValue) ClickSlot(slot.Value);
            else Deselect();
        }

        private void Deselect()
        {
            if (!_selected.HasValue && _selectedItem < 0) return;
            _selected = null;
            _selectedItem = -1;
            RefreshShopPhase();
        }
    }
}
