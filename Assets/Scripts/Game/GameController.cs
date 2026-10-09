using System.Collections.Generic;
using AutoBattler.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AutoBattler.Game
{
    /// <summary>Drives a GameSession: draws the board, handles input, and plays battles back tick by tick.</summary>
    public sealed class GameController : MonoBehaviour
    {
        private const float TickSeconds = 0.18f;
        private const float EndOfBattlePause = 1.2f;
        private const float BenchY = -1.6f;

        private static readonly Color PlayerTile = new Color(0.20f, 0.26f, 0.36f);
        private static readonly Color PlayerTileAlt = new Color(0.17f, 0.22f, 0.31f);
        private static readonly Color EnemyTile = new Color(0.30f, 0.19f, 0.21f);
        private static readonly Color EnemyTileAlt = new Color(0.26f, 0.16f, 0.18f);
        private static readonly Color BenchTile = new Color(0.22f, 0.22f, 0.24f);
        private static readonly Color HighlightTile = new Color(0.95f, 0.8f, 0.3f, 0.35f);

        private Camera _camera;
        private Hud _hud;
        private Transform _unitRoot;
        private SpriteRenderer _highlight;
        private GameSession _session;
        private int _seed;

        private readonly Dictionary<Slot, UnitView> _slotViews = new Dictionary<Slot, UnitView>();
        private readonly Dictionary<int, UnitView> _battleViews = new Dictionary<int, UnitView>();
        private Slot? _selected;

        private BattleSimulator _battle;
        private float _tickTimer;
        private float _endTimer;

        public void Initialize(Camera camera, int seed)
        {
            _camera = camera;
            _seed = seed;
            // Frame the 8x6 board plus bench; side panels fit in the remaining width.
            _camera.orthographicSize = 5.2f;
            _camera.transform.position = new Vector3(0f, 1.0f, -10f);

            BuildTiles();
            _unitRoot = new GameObject("Units").transform;
            _unitRoot.SetParent(transform, false);

            _hud = new Hud(transform);
            _hud.BuyClicked += Buy;
            _hud.RerollClicked += Reroll;
            _hud.SellClicked += SellSelected;
            _hud.FightClicked += StartBattle;
            _hud.RestartClicked += NewRun;

            NewRun();
        }

        private void NewRun()
        {
            _session = new GameSession(new SeededRandom(_seed++));
            _battle = null;
            _selected = null;
            ClearBattleViews();
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
            var tiles = new GameObject("Tiles").transform;
            tiles.SetParent(transform, false);
            for (int y = 0; y < BoardLayout.Height; y++)
            for (int x = 0; x < BoardLayout.Width; x++)
            {
                var p = new GridPos(x, y);
                bool alt = (x + y) % 2 == 0;
                var color = BoardLayout.IsPlayerCell(p) ? (alt ? PlayerTile : PlayerTileAlt) : (alt ? EnemyTile : EnemyTileAlt);
                SpriteFactory.CreateChild(tiles, $"Cell {p}", SpriteFactory.Square, color, 0, CellToWorld(p), Vector2.one * 0.96f);
            }
            for (int i = 0; i < GameSession.BenchSize; i++)
                SpriteFactory.CreateChild(tiles, $"Bench {i}", SpriteFactory.Square, BenchTile, 0, BenchToWorld(i), Vector2.one * 0.9f);

            _highlight = SpriteFactory.CreateChild(transform, "Highlight", SpriteFactory.Square, HighlightTile, 1,
                Vector3.zero, Vector2.one * 0.96f);
            _highlight.enabled = false;
        }

        // ---- Shop phase ---------------------------------------------------------------------------------------

        private void Buy(int shopSlot)
        {
            if (_session.TryBuy(shopSlot)) RefreshShopPhase();
            else if (_session.Phase == Phase.Shop) _hud.SetMessage("Can't buy that: not enough gold or bench space.");
        }

        private void Reroll()
        {
            if (_session.TryReroll()) RefreshShopPhase();
        }

        private void SellSelected()
        {
            if (_selected.HasValue && _session.TrySell(_selected.Value))
            {
                _selected = null;
                RefreshShopPhase();
            }
        }

        private void ClickSlot(Slot slot)
        {
            if (_session.Phase != Phase.Shop) return;

            if (_selected.HasValue)
            {
                var from = _selected.Value;
                _selected = null;
                if (!from.Equals(slot) && !_session.TryMove(from, slot))
                    _hud.SetMessage($"Board is full ({_session.BoardCapacity} units this round).");
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
            _hud.Refresh(_session, _selected.HasValue ? _session.Get(_selected.Value) : null);
        }

        // ---- Battle phase -------------------------------------------------------------------------------------

        private void StartBattle()
        {
            _battle = _session.StartBattle();
            if (_battle == null)
            {
                _hud.SetMessage("Place at least one unit on the board first.");
                return;
            }

            _selected = null;
            RefreshShopPhase(); // clears shop-phase views since the phase is now Battle
            foreach (var c in _battle.Combatants)
            {
                var view = UnitView.Create(_unitRoot, c.Unit, c.Team, CellToWorld(c.Position));
                view.SetHealth(c.Hp, c.MaxHp);
                _battleViews[c.Id] = view;
            }
            _tickTimer = 0f;
            _endTimer = EndOfBattlePause;
            _hud.SetMessage($"Round {_session.Round}: fight!");
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

            var result = _session.CompleteBattle(_battle);
            _battle = null;
            ClearBattleViews();
            _hud.SetMessage(Describe(result));
            RefreshShopPhase();
        }

        private void Apply(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.Move:
                    if (_battleViews.TryGetValue(e.SourceId, out var mover)) mover.MoveTo(CellToWorld(e.To));
                    break;
                case BattleEventType.Attack:
                    if (_battleViews.TryGetValue(e.TargetId, out var target))
                    {
                        var c = _battle.Find(e.TargetId);
                        target.SetHealth(c.Hp, c.MaxHp);
                        target.Flash();
                    }
                    break;
                case BattleEventType.Death:
                    if (_battleViews.TryGetValue(e.SourceId, out var dead))
                    {
                        Destroy(dead.gameObject);
                        _battleViews.Remove(e.SourceId);
                    }
                    break;
            }
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
                case Phase.Victory: return "VICTORY! You survived every round.";
                case Phase.Defeat: return $"DEFEAT. You lost {result.DamageTaken} health and fell in round {_session.Round}.";
                default:
                    return result.Won
                        ? $"Round won! +{result.GoldEarned} gold."
                        : $"Round lost: -{result.DamageTaken} health, +{result.GoldEarned} gold.";
            }
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
            if (kb == null || _session.Phase != Phase.Shop) return;

            for (int i = 0; i < Shop.SlotCount; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
                    Buy(i);
            if (kb.rKey.wasPressedThisFrame) Reroll();
            if (kb.sKey.wasPressedThisFrame) SellSelected();
            if (kb.spaceKey.wasPressedThisFrame) StartBattle();
            if (kb.escapeKey.wasPressedThisFrame) Deselect();
        }

        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || _session.Phase != Phase.Shop) return;

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
            if (!_selected.HasValue) return;
            _selected = null;
            RefreshShopPhase();
        }
    }
}
