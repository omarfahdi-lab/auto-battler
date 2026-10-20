using System.Collections.Generic;

namespace AutoBattler.Core
{
    public enum UnitRole
    {
        Fighter,
        // heals the lowest hp % ally, never attacks
        Healer,
    }

    // timings are in ticks. 1 tick = 1 animation frame (10fps), so ImpactTicks is literally
    // the frame in the attack sheet where the hit happens
    public sealed class UnitDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public int Cost { get; }
        public int MaxHp { get; }
        // damage, or heal amount for healers
        public int Power { get; }
        public int Range { get; }
        public UnitRole Role { get; }
        // ticks between attacks
        public int ActionCooldown { get; }
        // ticks until the hit lands (or the arrow is fired)
        public int ImpactTicks { get; }
        // attack anim length, cooldown can't go below this
        public int ActionFrames { get; }
        public bool UsesProjectile { get; }
        public int MoveTicks { get; }
        // UI accent color
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
