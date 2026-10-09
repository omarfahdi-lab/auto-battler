using System.Collections.Generic;

namespace AutoBattler.Core
{
    /// <summary>A class bonus that applies to every unit of that class when enough of them are on the board.</summary>
    public sealed class SynergyDefinition
    {
        public UnitDefinition Class { get; }
        public string Name { get; }
        /// <summary>Units needed for each tier, ascending.</summary>
        public IReadOnlyList<int> Thresholds { get; }
        /// <summary>Bonus at each tier (same length as <see cref="Thresholds"/>).</summary>
        public IReadOnlyList<StatBonus> Bonuses { get; }

        public SynergyDefinition(UnitDefinition unitClass, string name, int[] thresholds, StatBonus[] bonuses)
        {
            Class = unitClass;
            Name = name;
            Thresholds = thresholds;
            Bonuses = bonuses;
        }

        /// <summary>0 when inactive, otherwise the highest tier reached (1-based).</summary>
        public int TierFor(int count)
        {
            int tier = 0;
            for (int i = 0; i < Thresholds.Count; i++)
                if (count >= Thresholds[i]) tier = i + 1;
            return tier;
        }
    }

    public readonly struct ActiveSynergy
    {
        public readonly SynergyDefinition Definition;
        public readonly int Count;
        public readonly int Tier;

        public ActiveSynergy(SynergyDefinition definition, int count)
        {
            Definition = definition;
            Count = count;
            Tier = definition.TierFor(count);
        }

        public bool IsActive => Tier > 0;
        public StatBonus Bonus => IsActive ? Definition.Bonuses[Tier - 1] : default;
        public int NextThreshold => Tier < Definition.Thresholds.Count ? Definition.Thresholds[Tier] : 0;
    }

    public static class Synergies
    {
        public static readonly SynergyDefinition Warriors = new SynergyDefinition(UnitCatalog.Warrior, "Warriors",
            Balance.SynergyThresholds, Balance.WarriorSynergy);
        public static readonly SynergyDefinition Archers = new SynergyDefinition(UnitCatalog.Archer, "Archers",
            Balance.SynergyThresholds, Balance.ArcherSynergy);
        public static readonly SynergyDefinition Lancers = new SynergyDefinition(UnitCatalog.Lancer, "Lancers",
            Balance.SynergyThresholds, Balance.LancerSynergy);
        public static readonly SynergyDefinition Monks = new SynergyDefinition(UnitCatalog.Monk, "Monks",
            Balance.SynergyThresholds, Balance.MonkSynergy);

        public static readonly IReadOnlyList<SynergyDefinition> All = new[] { Warriors, Archers, Lancers, Monks };

        /// <summary>Counts units per class (every copy on the board counts) and resolves each synergy's tier.</summary>
        public static IReadOnlyList<ActiveSynergy> Evaluate(IEnumerable<OwnedUnit> board)
        {
            var counts = new Dictionary<UnitDefinition, int>();
            foreach (var unit in board)
            {
                if (unit == null) continue;
                counts.TryGetValue(unit.Definition, out int n);
                counts[unit.Definition] = n + 1;
            }

            var result = new List<ActiveSynergy>(All.Count);
            foreach (var synergy in All)
            {
                counts.TryGetValue(synergy.Class, out int count);
                result.Add(new ActiveSynergy(synergy, count));
            }
            return result;
        }

        public static StatBonus BonusFor(UnitDefinition unitClass, IReadOnlyList<ActiveSynergy> active)
        {
            foreach (var s in active)
                if (s.Definition.Class == unitClass) return s.Bonus;
            return default;
        }
    }
}
