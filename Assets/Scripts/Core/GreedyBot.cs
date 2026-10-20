using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoBattler.Core
{
    // simple deterministic bot for the balance sim. each shop it equips items, finishes merges,
    // levels when the board is full, buys whatever looks best and fields its strongest units
    public sealed class GreedyBot
    {
        // gold to hold for interest. spending everything actually wins more over 10 rounds
        // (see README) so this is 0 by default
        public int Reserve { get; set; }
        public int ReserveFromRound { get; set; } = 3;
        public int XpFromRound { get; set; } = 3;
        public int MaxRerollsPerRound { get; set; } = 2;

        private static readonly int[] ColumnOrder = { 3, 4, 2, 5, 1, 6, 0, 7 };

        public void PlayShop(GameSession s)
        {
            if (s.Phase != Phase.Shop) return;
            int reserve = s.Round >= ReserveFromRound ? Reserve : 0;

            for (int rerolls = 0; ; rerolls++)
            {
                BuyMerges(s);
                LevelUp(s, reserve);
                BuyUpgrades(s, reserve);
                if (rerolls >= MaxRerollsPerRound || s.Gold - Balance.RerollCost < reserve + 2) break;
                s.TryReroll();
            }

            Arrange(s);
            EquipItems(s);
        }

        public int ChooseReward(GameSession s)
        {
            int best = 0;
            double bestScore = double.MinValue;
            for (int i = 0; i < s.PendingRewards.Count; i++)
            {
                double score = Score(s, s.PendingRewards[i]);
                if (score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }
            return best;
        }

        private static double Score(GameSession s, RewardOption option)
        {
            int roundsLeft = Balance.FinalRound - s.Round + 1;
            if (option.Kind == RewardKind.Item)
                return TotalPct(option.Item.Bonus) * 1.6; // goes on the best unit
            var augment = option.Augment;
            int units = Math.Max(3, s.BoardCapacity);
            return TotalPct(augment.TeamBonus) * units * 0.5 + augment.GoldPerRound * roundsLeft * 1.5;
        }

        private static int TotalPct(StatBonus b) => b.HpPct + b.PowerPct + b.AttackSpeedPct + b.ArmorPct + b.HealPct;

        // buying

        private static void BuyMerges(GameSession s)
        {
            for (int i = 0; i < Shop.SlotCount; i++)
            {
                var def = s.Shop[i];
                if (def != null && CopiesOf(s, def, 1) >= 2) s.TryBuy(i);
            }
        }

        private void LevelUp(GameSession s, int reserve)
        {
            if (s.Round < XpFromRound) return;
            // only level when the board's full so the new slot actually gets used
            while (s.CanBuyXp && s.BoardCount >= s.BoardCapacity && s.Gold - Balance.XpCost >= reserve)
                s.TryBuyXp();
        }

        private static void BuyUpgrades(GameSession s, int reserve)
        {
            while (true)
            {
                int bestSlot = -1;
                double bestValue = 0;
                for (int i = 0; i < Shop.SlotCount; i++)
                {
                    var def = s.Shop[i];
                    if (def == null || s.Gold - def.Cost < reserve) continue;
                    double value = BuyValue(s, def);
                    if (value > bestValue)
                    {
                        bestValue = value;
                        bestSlot = i;
                    }
                }
                if (bestSlot < 0) return;

                if (!s.CanBuy(bestSlot) && !MakeBenchRoom(s, bestValue)) return;
                if (!s.TryBuy(bestSlot)) return;
            }
        }

        // pairs and synergies first, then just cost
        private static double BuyValue(GameSession s, UnitDefinition def)
        {
            int copies = CopiesOf(s, def, 1);
            int ofClass = s.OccupiedSlots().Count(slot => s.Get(slot).Definition == def);
            bool roomOnBoard = Units(s).Count() < s.BoardCapacity;
            double value = def.Cost;
            if (copies == 1) value += 2;               // pair -> close to a merge
            if (ofClass == 1 || ofClass == 3) value += 1.5; // hits a synergy breakpoint
            if (roomOnBoard) value += 2;
            else if (copies == 0) value -= 2.5;        // would just sit on the bench
            return value;
        }

        // sell the worst bench unit if the new one's better
        private static bool MakeBenchRoom(GameSession s, double incomingValue)
        {
            Slot? weakest = null;
            int weakestScore = int.MaxValue;
            for (int i = 0; i < GameSession.BenchSize; i++)
            {
                var unit = s.Get(Slot.Bench(i));
                if (unit == null) continue;
                int score = Strength(unit) + (CopiesOf(s, unit.Definition, unit.Stars) >= 2 ? 3 : 0);
                if (score < weakestScore)
                {
                    weakestScore = score;
                    weakest = Slot.Bench(i);
                }
            }
            return weakest.HasValue && weakestScore < incomingValue && s.TrySell(weakest.Value);
        }

        // board

        private static void Arrange(GameSession s)
        {
            var ranked = s.OccupiedSlots().Select(slot => (slot, unit: s.Get(slot)))
                .OrderByDescending(x => Strength(x.unit)).ThenBy(x => x.unit.Definition.Id).ToList();
            var wanted = new HashSet<OwnedUnit>(ranked.Take(s.BoardCapacity).Select(x => x.unit));

            // bench whoever didn't make the cut, then fill the board
            foreach (var (slot, unit) in ranked)
            {
                if (slot.Area != SlotArea.Board || wanted.Contains(unit)) continue;
                int free = FreeBench(s);
                if (free >= 0) s.TryMove(slot, Slot.Bench(free));
            }
            foreach (var (_, unit) in ranked)
            {
                if (!wanted.Contains(unit)) continue;
                var from = Find(s, unit);
                if (from.Area == SlotArea.Board) continue;
                // no free cell (bench was full) so swap with someone who didn't make the cut
                var cell = PreferredCell(s, unit) ?? UnwantedBoardSlot(s, wanted);
                if (cell.HasValue) s.TryMove(from, cell.Value);
            }
        }

        private static Slot? UnwantedBoardSlot(GameSession s, HashSet<OwnedUnit> wanted)
        {
            foreach (var slot in s.OccupiedSlots())
                if (slot.Area == SlotArea.Board && !wanted.Contains(s.Get(slot))) return slot;
            return null;
        }

        private static Slot? PreferredCell(GameSession s, OwnedUnit unit)
        {
            var def = unit.Definition;
            int row = def.Role == UnitRole.Healer ? 1 : def.Range > 1 ? 0 : BoardLayout.PlayerRows - 1;
            foreach (int r in new[] { row, 1, 0, 2 }.Distinct())
            foreach (int x in ColumnOrder)
            {
                var slot = Slot.Board(new GridPos(x, r));
                if (s.Get(slot) == null) return slot;
            }
            return null;
        }

        private static void EquipItems(GameSession s)
        {
            while (s.Inventory.Count > 0)
            {
                var target = s.OccupiedSlots().Where(slot => slot.Area == SlotArea.Board && s.Get(slot).Item == null)
                    .OrderByDescending(slot => Strength(s.Get(slot))).Cast<Slot?>().FirstOrDefault();
                if (!target.HasValue || !s.TryEquip(0, target.Value)) return;
            }
        }

        private static int Strength(OwnedUnit unit) => EnemyWaves.PowerOf(unit) * 2 + (unit.Item != null ? 2 : 0);

        private static IEnumerable<OwnedUnit> Units(GameSession s) => s.OccupiedSlots().Select(s.Get);

        private static int CopiesOf(GameSession s, UnitDefinition def, int stars) =>
            Units(s).Count(u => u.Definition == def && u.Stars == stars);

        private static int FreeBench(GameSession s)
        {
            for (int i = 0; i < GameSession.BenchSize; i++)
                if (s.Get(Slot.Bench(i)) == null) return i;
            return -1;
        }

        private static Slot Find(GameSession s, OwnedUnit unit) => s.OccupiedSlots().First(slot => s.Get(slot) == unit);
    }
}
