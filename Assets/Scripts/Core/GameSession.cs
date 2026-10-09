using System;
using System.Collections.Generic;

namespace AutoBattler.Core
{
    public enum Phase
    {
        Shop,
        Battle,
        /// <summary>After a win: choose one of <see cref="GameSession.PendingRewards"/> before shopping.</summary>
        Reward,
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
        /// <summary>Total income for the next round (base + interest + streak + augments).</summary>
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

        private readonly List<ItemDefinition> _inventory = new List<ItemDefinition>();
        private readonly List<AugmentDefinition> _augments = new List<AugmentDefinition>();
        private List<RewardOption> _pendingRewards = new List<RewardOption>();

        /// <summary>Items won but not equipped.</summary>
        public IReadOnlyList<ItemDefinition> Inventory => _inventory;
        public IReadOnlyList<AugmentDefinition> Augments => _augments;
        public IReadOnlyList<RewardOption> PendingRewards => _pendingRewards;

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
        public StatBonus BonusFor(OwnedUnit unit, IReadOnlyList<ActiveSynergy> synergies)
        {
            var bonus = Synergies.BonusFor(unit.Definition, synergies) + TeamBonus;
            if (unit.Item != null) bonus += unit.Item.Bonus;
            return bonus;
        }

        /// <summary>Sum of augment bonuses that apply to every unit.</summary>
        public StatBonus TeamBonus
        {
            get
            {
                StatBonus total = default;
                foreach (var a in _augments) total += a.TeamBonus;
                return total;
            }
        }

        public int AugmentGoldPerRound
        {
            get
            {
                int total = 0;
                foreach (var a in _augments) total += a.GoldPerRound;
                return total;
            }
        }

        /// <summary>Applies one of the pending rewards: items go to the inventory, augments take effect immediately.</summary>
        public bool ChooseReward(int index)
        {
            if (Phase != Phase.Reward || index < 0 || index >= _pendingRewards.Count) return false;
            var choice = _pendingRewards[index];
            if (choice.Kind == RewardKind.Item) _inventory.Add(choice.Item);
            else _augments.Add(choice.Augment);
            _pendingRewards = new List<RewardOption>();
            Phase = Phase.Shop;
            return true;
        }

        /// <summary>Grants a reward outside the reward phase (tests and tooling).</summary>
        internal void GrantForTests(RewardOption reward)
        {
            if (reward.Kind == RewardKind.Item) _inventory.Add(reward.Item);
            else _augments.Add(reward.Augment);
        }

        /// <summary>Equips an inventory item on a unit; an item the unit already held goes back to the inventory.</summary>
        public bool TryEquip(int inventoryIndex, Slot slot)
        {
            if (Phase != Phase.Shop || inventoryIndex < 0 || inventoryIndex >= _inventory.Count) return false;
            var unit = Get(slot);
            if (unit == null) return false;

            var item = _inventory[inventoryIndex];
            _inventory.RemoveAt(inventoryIndex);
            if (unit.Item != null) _inventory.Add(unit.Item);
            unit.Item = item;
            return true;
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
            if (unit.Item != null) _inventory.Add(unit.Item);
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
            {
                var bonus = Synergies.BonusFor(enemy.Unit.Definition, enemySynergies);
                if (enemy.IsBoss) bonus += Balance.BossBonus;
                combatants.Add(new Combatant(id++, Team.Enemy, enemy.Unit, enemy.Position,
                    CombatStats.For(enemy.Unit, bonus), enemy.IsBoss));
            }

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

            int income = Income(Gold, Streak, out int interest, out int streakBonus) + AugmentGoldPerRound;
            Gold += income;
            bool bossWin = won && EnemyWaves.IsBossRound(Round);
            Round++;
            AddXp(Balance.PassiveXpPerRound);
            Shop.Roll(_rng, Level);

            if (won)
            {
                _pendingRewards = new List<RewardOption>(Rewards.Generate(_rng, bossWin));
                Phase = Phase.Reward;
            }
            else
            {
                Phase = Phase.Shop;
            }
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
            var items = new List<ItemDefinition>();
            foreach (var slot in new List<Slot>(OccupiedSlots()))
            {
                if (needed == 0) break;
                var u = Get(slot);
                if (u.Definition != def || u.Stars != stars) continue;
                Set(slot, null);
                if (u.Item != null) items.Add(u.Item);
                if (keep == null) keep = slot;
                needed--;
            }

            // The upgraded unit keeps the first item; any others return to the inventory.
            var merged = new OwnedUnit(def, stars + 1);
            if (items.Count > 0)
            {
                merged.Item = items[0];
                for (int i = 1; i < items.Count; i++) _inventory.Add(items[i]);
            }
            Set(keep.Value, merged);
        }
    }
}
