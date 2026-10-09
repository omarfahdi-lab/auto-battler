using System;
using System.Collections.Generic;

namespace AutoBattler.Core
{
    public enum Phase
    {
        Shop,
        Battle,
        Victory,
        Defeat,
    }

    public enum SlotArea
    {
        Bench,
        Board,
    }

    /// <summary>A place a player unit can sit: a bench slot or a cell on the player's half of the board.</summary>
    public readonly struct Slot : IEquatable<Slot>
    {
        public readonly SlotArea Area;
        public readonly int Index;

        public Slot(SlotArea area, int index)
        {
            Area = area;
            Index = index;
        }

        public static Slot Bench(int index) => new Slot(SlotArea.Bench, index);
        public static Slot Board(GridPos pos) => new Slot(SlotArea.Board, BoardLayout.PlayerPosToIndex(pos));

        public GridPos BoardPos => BoardLayout.PlayerIndexToPos(Index);

        public bool Equals(Slot other) => Area == other.Area && Index == other.Index;
        public override bool Equals(object obj) => obj is Slot other && Equals(other);
        public override int GetHashCode() => ((int)Area * 397) ^ Index;
        public override string ToString() => Area == SlotArea.Bench ? $"Bench[{Index}]" : $"Board{BoardPos}";
    }

    public readonly struct RoundResult
    {
        public readonly bool Won;
        public readonly int DamageTaken;
        public readonly int GoldEarned;

        public RoundResult(bool won, int damageTaken, int goldEarned)
        {
            Won = won;
            DamageTaken = damageTaken;
            GoldEarned = goldEarned;
        }
    }

    /// <summary>Whole-run state: economy, owned units, and the shop/battle round loop.</summary>
    public sealed class GameSession
    {
        public const int BenchSize = 8;
        public const int StartingGold = 10;
        public const int StartingHealth = 20;
        public const int FinalRound = 10;
        public const int RerollCost = 1;
        public const int BaseIncome = 5;
        public const int MaxInterest = 3;
        public const int WinBonus = 1;
        public const int BaseLossDamage = 2;

        private readonly IRandom _rng;
        private readonly OwnedUnit[] _bench = new OwnedUnit[BenchSize];
        private readonly OwnedUnit[] _board = new OwnedUnit[BoardLayout.PlayerCells];

        public Phase Phase { get; private set; } = Phase.Shop;
        public int Gold { get; private set; } = StartingGold;
        public int Health { get; private set; } = StartingHealth;
        public int Round { get; private set; } = 1;
        public Shop Shop { get; } = new Shop();

        public GameSession(IRandom rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Shop.Roll(_rng, Round);
        }

        public int BoardCapacity => Math.Min(2 + Round, 8);

        public OwnedUnit Get(Slot slot) => slot.Area == SlotArea.Bench ? _bench[slot.Index] : _board[slot.Index];

        private void Set(Slot slot, OwnedUnit unit)
        {
            if (slot.Area == SlotArea.Bench) _bench[slot.Index] = unit;
            else _board[slot.Index] = unit;
        }

        public int BoardCount
        {
            get
            {
                int n = 0;
                foreach (var u in _board) if (u != null) n++;
                return n;
            }
        }

        public IEnumerable<Slot> OccupiedSlots()
        {
            for (int i = 0; i < _board.Length; i++)
                if (_board[i] != null) yield return new Slot(SlotArea.Board, i);
            for (int i = 0; i < _bench.Length; i++)
                if (_bench[i] != null) yield return Slot.Bench(i);
        }

        public bool TryReroll()
        {
            if (Phase != Phase.Shop || Gold < RerollCost) return false;
            Gold -= RerollCost;
            Shop.Roll(_rng, Round);
            return true;
        }

        public bool CanBuy(int shopSlot)
        {
            if (Phase != Phase.Shop) return false;
            var def = Shop[shopSlot];
            if (def == null || Gold < def.Cost) return false;
            return FreeBenchSlot() >= 0 || CountCopies(def, 1) >= OwnedUnit.MaxStars - 1;
        }

        public bool TryBuy(int shopSlot)
        {
            if (!CanBuy(shopSlot)) return false;
            var def = Shop.Take(shopSlot);
            Gold -= def.Cost;

            var bought = new OwnedUnit(def);
            int free = FreeBenchSlot();
            if (free >= 0)
            {
                _bench[free] = bought;
                ResolveMerges(def);
            }
            else
            {
                // Bench is full but this purchase completes a set of three: merge straight away.
                MergeInto(def, 1, extra: bought);
                ResolveMerges(def);
            }
            return true;
        }

        public bool TrySell(Slot slot)
        {
            if (Phase != Phase.Shop) return false;
            var unit = Get(slot);
            if (unit == null) return false;
            Set(slot, null);
            Gold += unit.SellValue;
            return true;
        }

        /// <summary>Moves a unit to another slot, swapping if the destination is occupied.</summary>
        public bool TryMove(Slot from, Slot to)
        {
            if (Phase != Phase.Shop || from.Equals(to)) return false;
            var moving = Get(from);
            if (moving == null) return false;
            var displaced = Get(to);

            bool addsToBoard = from.Area == SlotArea.Bench && to.Area == SlotArea.Board && displaced == null;
            if (addsToBoard && BoardCount >= BoardCapacity) return false;

            Set(to, moving);
            Set(from, displaced);
            return true;
        }

        /// <summary>Builds the battle for this round. Returns null if the player has no units on the board.</summary>
        public BattleSimulator StartBattle()
        {
            if (Phase != Phase.Shop || BoardCount == 0) return null;

            var combatants = new List<Combatant>();
            int id = 0;
            for (int i = 0; i < _board.Length; i++)
                if (_board[i] != null)
                    combatants.Add(new Combatant(id++, Team.Player, _board[i], BoardLayout.PlayerIndexToPos(i)));
            foreach (var enemy in EnemyWaves.Generate(Round, _rng))
                combatants.Add(new Combatant(id++, Team.Enemy, enemy.Unit, enemy.Position));

            Phase = Phase.Battle;
            return new BattleSimulator(combatants);
        }

        /// <summary>Applies the battle outcome and advances to the next round (or ends the run).</summary>
        public RoundResult CompleteBattle(BattleSimulator battle)
        {
            if (Phase != Phase.Battle) throw new InvalidOperationException("No battle in progress.");
            if (!battle.IsOver) throw new InvalidOperationException("Battle has not finished.");

            bool won = battle.Winner == Team.Player;
            int damage = won ? 0 : BaseLossDamage + battle.AliveCount(Team.Enemy);
            Health = Math.Max(0, Health - damage);

            if (Health == 0)
            {
                Phase = Phase.Defeat;
                return new RoundResult(false, damage, 0);
            }
            if (won && Round == FinalRound)
            {
                Phase = Phase.Victory;
                return new RoundResult(true, 0, 0);
            }

            int income = BaseIncome + Math.Min(Gold / 10, MaxInterest) + (won ? WinBonus : 0);
            Gold += income;
            Round++;
            Shop.Roll(_rng, Round);
            Phase = Phase.Shop;
            return new RoundResult(won, damage, income);
        }

        private int FreeBenchSlot() => Array.IndexOf(_bench, null);

        private int CountCopies(UnitDefinition def, int stars)
        {
            int n = 0;
            foreach (var slot in OccupiedSlots())
            {
                var u = Get(slot);
                if (u.Definition == def && u.Stars == stars) n++;
            }
            return n;
        }

        private void ResolveMerges(UnitDefinition def)
        {
            for (int stars = 1; stars < OwnedUnit.MaxStars; stars++)
                if (CountCopies(def, stars) >= 3) MergeInto(def, stars, extra: null);
        }

        /// <summary>
        /// Replaces three copies (two if <paramref name="extra"/> supplies the third) with one upgraded unit.
        /// Board copies are consumed first so the upgraded unit stays deployed.
        /// </summary>
        private void MergeInto(UnitDefinition def, int stars, OwnedUnit extra)
        {
            int needed = extra == null ? 3 : 2;
            Slot? keep = null;
            foreach (var slot in new List<Slot>(OccupiedSlots()))
            {
                if (needed == 0) break;
                var u = Get(slot);
                if (u.Definition != def || u.Stars != stars) continue;
                Set(slot, null);
                if (keep == null) keep = slot;
                needed--;
            }
            Set(keep.Value, new OwnedUnit(def, stars + 1));
        }
    }
}
