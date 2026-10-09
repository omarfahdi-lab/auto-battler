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
        /// <summary>Total income for the next round (base + interest + streak).</summary>
        public readonly int GoldEarned;
        public readonly int Interest;
        public readonly int StreakBonus;

        public RoundResult(bool won, int damageTaken, int goldEarned, int interest, int streakBonus)
        {
            Won = won;
            DamageTaken = damageTaken;
            GoldEarned = goldEarned;
            Interest = interest;
            StreakBonus = streakBonus;
        }
    }

    /// <summary>Whole-run state: economy, owned units, and the shop/battle round loop.</summary>
    public sealed class GameSession
    {
        public const int BenchSize = 8;
        public const int StartingGold = Balance.StartingGold;
        public const int StartingHealth = Balance.StartingHealth;
        public const int FinalRound = Balance.FinalRound;
        public const int RerollCost = Balance.RerollCost;
        public const int BaseLossDamage = Balance.BaseLossDamage;

        private readonly IRandom _rng;
        private readonly OwnedUnit[] _bench = new OwnedUnit[BenchSize];
        private readonly OwnedUnit[] _board = new OwnedUnit[BoardLayout.PlayerCells];

        public Phase Phase { get; private set; } = Phase.Shop;
        public int Gold { get; internal set; } = StartingGold;
        public int Health { get; private set; } = StartingHealth;
        public int Round { get; private set; } = 1;
        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        /// <summary>Positive for consecutive wins, negative for consecutive losses.</summary>
        public int Streak { get; private set; }
        public Shop Shop { get; } = new Shop();

        public GameSession(IRandom rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Shop.Roll(_rng, Level);
        }

        public int BoardCapacity => Balance.UnitCap(Level);
        public int XpToNextLevel => Balance.XpToNext(Level);
        public bool IsMaxLevel => Level >= Balance.MaxLevel;

        public OwnedUnit Get(Slot slot) => slot.Area == SlotArea.Bench ? _bench[slot.Index] : _board[slot.Index];

        private void Set(Slot slot, OwnedUnit unit)
        {
            if (slot.Area == SlotArea.Bench) _bench[slot.Index] = unit;
            else _board[slot.Index] = unit;
        }

        /// <summary>Class synergies for the units currently on the board (inactive ones included).</summary>
        public IReadOnlyList<ActiveSynergy> ActiveSynergies => Synergies.Evaluate(_board);

        /// <summary>Every bonus that applies to one of the player's units in battle.</summary>
        public StatBonus BonusFor(OwnedUnit unit, IReadOnlyList<ActiveSynergy> synergies) =>
            Synergies.BonusFor(unit.Definition, synergies);

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
            Shop.Roll(_rng, Level);
            return true;
        }

        public bool CanBuyXp => Phase == Phase.Shop && !IsMaxLevel && Gold >= Balance.XpCost;

        public bool TryBuyXp()
        {
            if (!CanBuyXp) return false;
            Gold -= Balance.XpCost;
            AddXp(Balance.XpPerPurchase);
            return true;
        }

        private void AddXp(int amount)
        {
            Xp += amount;
            while (!IsMaxLevel && Xp >= XpToNextLevel)
            {
                Xp -= XpToNextLevel;
                Level++;
            }
            if (IsMaxLevel) Xp = 0;
        }

        /// <summary>The income the player would receive for finishing a round with the given streak.</summary>
        public static int Income(int gold, int streak, out int interest, out int streakBonus)
        {
            interest = Balance.Interest(gold);
            streakBonus = Balance.StreakBonus(Math.Abs(streak));
            return Balance.BaseIncome + interest + streakBonus;
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
            var synergies = ActiveSynergies;
            for (int i = 0; i < _board.Length; i++)
            {
                var unit = _board[i];
                if (unit == null) continue;
                combatants.Add(new Combatant(id++, Team.Player, unit, BoardLayout.PlayerIndexToPos(i),
                    CombatStats.For(unit, BonusFor(unit, synergies))));
            }

            var wave = EnemyWaves.Generate(Round, _rng);
            var waveUnits = new List<OwnedUnit>();
            foreach (var enemy in wave) waveUnits.Add(enemy.Unit);
            var enemySynergies = Synergies.Evaluate(waveUnits);
            foreach (var enemy in wave)
                combatants.Add(new Combatant(id++, Team.Enemy, enemy.Unit, enemy.Position,
                    CombatStats.For(enemy.Unit, Synergies.BonusFor(enemy.Unit.Definition, enemySynergies))));

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
            // Draws count as losses.
            Streak = won ? Math.Max(Streak, 0) + 1 : Math.Min(Streak, 0) - 1;

            if (Health == 0)
            {
                Phase = Phase.Defeat;
                return new RoundResult(false, damage, 0, 0, 0);
            }
            if (Round == FinalRound)
            {
                // The final round is a boss fight: beat it to win the run.
                Phase = won ? Phase.Victory : Phase.Defeat;
                return new RoundResult(won, damage, 0, 0, 0);
            }

            int income = Income(Gold, Streak, out int interest, out int streakBonus);
            Gold += income;
            Round++;
            AddXp(Balance.PassiveXpPerRound);
            Shop.Roll(_rng, Level);
            Phase = Phase.Shop;
            return new RoundResult(won, damage, income, interest, streakBonus);
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
