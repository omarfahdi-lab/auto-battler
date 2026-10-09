using System;
using System.Collections.Generic;

namespace AutoBattler.Core
{
    /// <summary>A row of units for sale. Pricier units become more common in later rounds.</summary>
    public sealed class Shop
    {
        public const int SlotCount = 5;

        private readonly UnitDefinition[] _slots = new UnitDefinition[SlotCount];

        /// <summary>The unit in a slot, or null if it was bought.</summary>
        public UnitDefinition this[int slot] => _slots[slot];

        public void Roll(IRandom rng, int round)
        {
            for (int i = 0; i < SlotCount; i++) _slots[i] = Pick(rng, round);
        }

        public UnitDefinition Take(int slot)
        {
            var unit = _slots[slot];
            _slots[slot] = null;
            return unit;
        }

        /// <summary>Replaces the slot contents; used by tests and tooling.</summary>
        public void Set(int slot, UnitDefinition unit) => _slots[slot] = unit;

        public static int Weight(int cost, int round)
        {
            switch (cost)
            {
                case 1: return 6;
                case 2: return 3 + round / 3;
                case 3: return round >= 3 ? 1 + round / 3 : 0;
                default: return 0;
            }
        }

        public static UnitDefinition Pick(IRandom rng, int round) => Pick(rng, round, UnitCatalog.All);

        public static UnitDefinition Pick(IRandom rng, int round, IReadOnlyList<UnitDefinition> pool)
        {
            int total = 0;
            foreach (var def in pool) total += Weight(def.Cost, round);
            if (total == 0) throw new InvalidOperationException("No units available for this round.");

            int roll = rng.Next(total);
            foreach (var def in pool)
            {
                roll -= Weight(def.Cost, round);
                if (roll < 0) return def;
            }
            throw new InvalidOperationException("Unreachable");
        }
    }
}
