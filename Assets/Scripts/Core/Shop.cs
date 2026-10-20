using System;
using System.Collections.Generic;

namespace AutoBattler.Core
{
    public sealed class Shop
    {
        public const int SlotCount = 5;

        private readonly UnitDefinition[] _slots = new UnitDefinition[SlotCount];

        // null once bought
        public UnitDefinition this[int slot] => _slots[slot];

        public void Roll(IRandom rng, int level)
        {
            for (int i = 0; i < SlotCount; i++) _slots[i] = Pick(rng, level);
        }

        public UnitDefinition Take(int slot)
        {
            var unit = _slots[slot];
            _slots[slot] = null;
            return unit;
        }

        // for tests
        public void Set(int slot, UnitDefinition unit) => _slots[slot] = unit;

        public static UnitDefinition Pick(IRandom rng, int level) => Pick(rng, level, UnitCatalog.All);

        // roll the cost tier first, then a unit in that tier
        public static UnitDefinition Pick(IRandom rng, int level, IReadOnlyList<UnitDefinition> pool)
        {
            int total = 0;
            for (int cost = 1; cost <= 3; cost++)
                if (HasTier(pool, cost)) total += Balance.ShopOdds(level, cost);
            if (total == 0) throw new InvalidOperationException("No units available at this level.");

            int roll = rng.Next(total);
            for (int cost = 1; cost <= 3; cost++)
            {
                if (!HasTier(pool, cost)) continue;
                roll -= Balance.ShopOdds(level, cost);
                if (roll >= 0) continue;

                var tier = new List<UnitDefinition>();
                foreach (var def in pool) if (def.Cost == cost) tier.Add(def);
                return tier[rng.Next(tier.Count)];
            }
            throw new InvalidOperationException("Unreachable");
        }

        private static bool HasTier(IReadOnlyList<UnitDefinition> pool, int cost)
        {
            foreach (var def in pool) if (def.Cost == cost) return true;
            return false;
        }
    }
}
