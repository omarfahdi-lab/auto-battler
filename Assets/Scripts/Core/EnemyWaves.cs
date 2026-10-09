using System;
using System.Collections.Generic;

namespace AutoBattler.Core
{
    public readonly struct EnemyPlacement
    {
        public readonly OwnedUnit Unit;
        public readonly GridPos Position;
        public readonly bool IsBoss;

        public EnemyPlacement(OwnedUnit unit, GridPos position, bool isBoss = false)
        {
            Unit = unit;
            Position = position;
            IsBoss = isBoss;
        }
    }

    /// <summary>
    /// Builds each round's enemy team from a power budget (<see cref="Balance.EnemyBudget"/>), so difficulty follows
    /// a designed curve instead of shop luck. Power is measured like gold: cost x copies (3 per star level).
    /// Boss rounds spend part of the budget on one big upgraded unit and the rest on support.
    /// </summary>
    public static class EnemyWaves
    {
        public const int MaxEnemies = 8;

        public static bool IsBossRound(int round) => Array.IndexOf(Balance.BossRounds, round) >= 0;

        public static int PowerOf(OwnedUnit unit) => unit.Definition.Cost * unit.CopyCount;

        public static IReadOnlyList<EnemyPlacement> Generate(int round, IRandom rng)
        {
            int remaining = Balance.EnemyBudget(round);
            int maxCost = Balance.EnemyMaxCost(round);
            int maxStars = Balance.EnemyMaxStars(round);
            var units = new List<OwnedUnit>();
            OwnedUnit boss = null;

            if (IsBossRound(round))
            {
                boss = Balance.BossFor(round);
                remaining -= PowerOf(boss);
            }

            int supportCount = Math.Min(Balance.EnemyUnitCount(round), MaxEnemies - (boss != null ? 1 : 0));
            while (units.Count < supportCount)
            {
                int slotsAfter = supportCount - units.Count - 1;
                var def = PickAffordable(rng, maxCost, remaining - slotsAfter);
                if (def == null) break;
                units.Add(new OwnedUnit(def));
                remaining -= def.Cost;
            }

            // Spend what's left upgrading random units, then on extra bodies if nothing can be upgraded.
            while (remaining > 0)
            {
                var upgradable = new List<int>();
                for (int i = 0; i < units.Count; i++)
                    if (units[i].Stars < maxStars && UpgradeCost(units[i]) <= remaining) upgradable.Add(i);

                if (upgradable.Count > 0)
                {
                    int index = upgradable[rng.Next(upgradable.Count)];
                    remaining -= UpgradeCost(units[index]);
                    units[index] = new OwnedUnit(units[index].Definition, units[index].Stars + 1);
                    continue;
                }

                var extra = units.Count + (boss != null ? 1 : 0) < MaxEnemies ? PickAffordable(rng, maxCost, remaining) : null;
                if (extra == null) break;
                units.Add(new OwnedUnit(extra));
                remaining -= extra.Cost;
            }

            return Place(units, boss, rng);
        }

        private static int UpgradeCost(OwnedUnit unit) => unit.Definition.Cost * unit.CopyCount * 2;

        private static UnitDefinition PickAffordable(IRandom rng, int maxCost, int budget)
        {
            var options = new List<UnitDefinition>();
            foreach (var def in UnitCatalog.All)
                if (def.Cost <= maxCost && def.Cost <= budget) options.Add(def);
            return options.Count == 0 ? null : options[rng.Next(options.Count)];
        }

        private static IReadOnlyList<EnemyPlacement> Place(List<OwnedUnit> units, OwnedUnit boss, IRandom rng)
        {
            var free = new List<GridPos>();
            for (int y = BoardLayout.PlayerRows; y < BoardLayout.Height; y++)
                for (int x = 0; x < BoardLayout.Width; x++)
                    free.Add(new GridPos(x, y));

            var wave = new List<EnemyPlacement>();
            int frontRow = BoardLayout.PlayerRows;
            int backRow = BoardLayout.Height - 1;

            if (boss != null)
            {
                // The boss leads from the middle of the front line.
                var pos = new GridPos(BoardLayout.Width / 2, boss.Definition.Range <= 1 ? frontRow : backRow);
                free.Remove(pos);
                wave.Add(new EnemyPlacement(boss, pos, isBoss: true));
            }

            foreach (var unit in units)
            {
                // Melee units stand in the front row; ranged units and healers in the back, when there is room.
                int preferredRow = unit.Definition.Range <= 1 ? frontRow : backRow;
                var options = free.FindAll(p => p.Y == preferredRow);
                if (options.Count == 0) options = free;
                var pos = options[rng.Next(options.Count)];
                free.Remove(pos);
                wave.Add(new EnemyPlacement(unit, pos));
            }
            return wave;
        }
    }
}
