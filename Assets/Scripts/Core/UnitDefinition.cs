using System.Collections.Generic;

namespace AutoBattler.Core
{
    public enum UnitRole
    {
        /// <summary>Attacks the nearest enemy.</summary>
        Fighter,
        /// <summary>Heals the most injured ally (lowest HP fraction) and never attacks.</summary>
        Healer,
    }

    /// <summary>
    /// Static data for a unit type. All timings are in battle ticks (<see cref="BattleSimulator.TicksPerSecond"/>),
    /// which match the 10 fps animations one-to-one, so <see cref="ImpactTicks"/> is the attack sheet's impact frame.
    /// </summary>
    public sealed class UnitDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Cost { get; }
        public int MaxHp { get; }
        /// <summary>Damage per hit, or HP restored per heal for healers.</summary>
        public int Power { get; }
        public int Range { get; }
        public UnitRole Role { get; }
        /// <summary>Ticks between the start of one action and the start of the next.</summary>
        public int ActionCooldown { get; }
        /// <summary>Ticks from the start of an action until it takes effect (or the projectile is released).</summary>
        public int ImpactTicks { get; }
        /// <summary>Length of the action animation in ticks; attack speed can't push the cooldown below this.</summary>
        public int ActionFrames { get; }
        /// <summary>Whether the action launches a projectile that must travel to the target.</summary>
        public bool UsesProjectile { get; }
        /// <summary>Ticks spent moving one cell.</summary>
        public int MoveTicks { get; }
        /// <summary>RGB accent colour for UI, e.g. 0x4A90E2.</summary>
        public int Color { get; }

        public UnitDefinition(string id, string name, int cost, int maxHp, int power, int range, UnitRole role,
            int actionCooldown, int impactTicks, int actionFrames, bool usesProjectile, int moveTicks, int color)
        {
            Id = id;
            Name = name;
            Cost = cost;
            MaxHp = maxHp;
            Power = power;
            Range = range;
            Role = role;
            ActionCooldown = actionCooldown;
            ImpactTicks = impactTicks;
            ActionFrames = actionFrames;
            UsesProjectile = usesProjectile;
            MoveTicks = moveTicks;
            Color = color;
        }

        public override string ToString() => Name;
    }

    public static class UnitCatalog
    {
        public static readonly UnitDefinition Warrior = new UnitDefinition("warrior", "Warrior", cost: 1, maxHp: 60,
            power: 6, range: 1, UnitRole.Fighter, actionCooldown: 8, impactTicks: 2, actionFrames: 4, usesProjectile: false,
            moveTicks: 3, color: 0x4A90E2);

        public static readonly UnitDefinition Archer = new UnitDefinition("archer", "Archer", cost: 2, maxHp: 35,
            power: 6, range: 3, UnitRole.Fighter, actionCooldown: 12, impactTicks: 5, actionFrames: 8, usesProjectile: true,
            moveTicks: 3, color: 0x7ED321);

        public static readonly UnitDefinition Lancer = new UnitDefinition("lancer", "Lancer", cost: 3, maxHp: 140,
            power: 9, range: 1, UnitRole.Fighter, actionCooldown: 10, impactTicks: 1, actionFrames: 3, usesProjectile: false,
            moveTicks: 4, color: 0x8B6F47);

        public static readonly UnitDefinition Monk = new UnitDefinition("monk", "Monk", cost: 3, maxHp: 40,
            power: 12, range: 3, UnitRole.Healer, actionCooldown: 16, impactTicks: 5, actionFrames: 11, usesProjectile: false,
            moveTicks: 3, color: 0xF5A623);

        public static readonly IReadOnlyList<UnitDefinition> All = new[] { Warrior, Archer, Lancer, Monk };
    }
}
