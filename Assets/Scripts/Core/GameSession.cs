using System;
using System.Collections.Generic;

namespace AutoBattler.Core
{
    public enum Phase
    {
        Shop,
        Battle,
        // won a fight, waiting on a reward pick
        Reward,
        Victory,
        Defeat,
    }

    public enum SlotArea
    {
        Bench,
        Board,
    }

    // bench slot or a cell on the player's side
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
        // base + interest + streak + augments
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

    // the whole run: gold, units, shop and the round loop
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
        // + = win streak, - = loss streak
        public int Streak { get; private set; }
        public Shop Shop { get; } = new Shop();

        private readonly List<ItemDefinition> _inventory = new List<ItemDefinition>();
        private readonly List<AugmentDefinition> _augments = new List<AugmentDefinition>();
        private List<RewardOption> _pendingRewards = new List<RewardOption>();

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

        public IReadOnlyList<ActiveSynergy> ActiveSynergies => Synergies.Evaluate(_board);

        // synergy + item + augments for one of your units
        public StatBonus BonusFor(OwnedUnit unit, IReadOnlyList<ActiveSynergy> synergies)
        {
            var bonus = Synergies.BonusFor(unit.Definition, synergies) + TeamBonus;
            if (unit.Item != null) bonus += unit.Item.Bonus;
            return bonus;
        }

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

        // for tests
        internal void GrantForTests(RewardOption reward)
        {
            if (reward.Kind == RewardKind.Item) _inventory.Add(reward.Item);
            else _augments.Add(reward.Augment);
        }

        // swaps out whatever the unit was holding
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
                // bench is full but this is the 3rd copy, so merge right away
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

        // swaps if something's already there
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

        // null if the board is empty
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

        public RoundResult CompleteBattle(BattleSimulator battle)
        {
            if (Phase != Phase.Battle) throw new InvalidOperationException("No battle in progress.");
            if (!battle.IsOver) throw new InvalidOperationException("Battle has not finished.");

            bool won = battle.Winner == Team.Player;
            int damage = won ? 0 : BaseLossDamage + battle.AliveCount(Team.Enemy);
            Health = Math.Max(0, Health - damage);
            // draws count as a loss
            Streak = won ? Math.Max(Streak, 0) + 1 : Math.Min(Streak, 0) - 1;

            if (Health == 0)
            {
                Phase = Phase.Defeat;
                return new RoundResult(false, damage, 0, 0, 0);
            }
            if (Round == FinalRound)
            {
                // round 10 is the final boss, lose it and the run's over
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

        // 3 copies -> 1 upgraded unit (or 2 if extra is the 3rd). uses board copies first
        // so the upgraded one stays on the board
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

            // keep the first item, the rest go back to the inventory
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
