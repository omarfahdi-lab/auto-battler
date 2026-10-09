using System.Collections.Generic;

namespace AutoBattler.Core
{
    /// <summary>Static data for a unit type. Cooldowns are measured in battle ticks.</summary>
    public sealed class UnitDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Cost { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public int Range { get; }
        public int AttackCooldown { get; }
        /// <summary>RGB colour used by the placeholder art, e.g. 0x4A90E2.</summary>
        public int Color { get; }

        public UnitDefinition(string id, string name, int cost, int maxHp, int attack, int range, int attackCooldown, int color)
        {
            Id = id;
            Name = name;
            Cost = cost;
            MaxHp = maxHp;
            Attack = attack;
            Range = range;
            AttackCooldown = attackCooldown;
            Color = color;
        }

        public override string ToString() => Name;
    }

    public static class UnitCatalog
    {
        public static readonly UnitDefinition Knight = new UnitDefinition("knight", "Knight", 1, 60, 6, 1, 3, 0x4A90E2);
        public static readonly UnitDefinition Archer = new UnitDefinition("archer", "Archer", 1, 35, 5, 3, 3, 0x7ED321);
        public static readonly UnitDefinition Rogue = new UnitDefinition("rogue", "Rogue", 2, 45, 5, 1, 2, 0x9B59B6);
        public static readonly UnitDefinition Mage = new UnitDefinition("mage", "Mage", 3, 30, 14, 3, 5, 0xF5A623);
        public static readonly UnitDefinition Golem = new UnitDefinition("golem", "Golem", 3, 140, 8, 1, 4, 0x8B6F47);

        public static readonly IReadOnlyList<UnitDefinition> All = new[] { Knight, Archer, Rogue, Mage, Golem };
    }
}
