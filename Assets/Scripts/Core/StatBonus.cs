using System;
using System.Collections.Generic;

namespace AutoBattler.Core
{
    // % bonuses. synergy + item + augment + boss all just add up
    public readonly struct StatBonus
    {
        public readonly int HpPct;
        public readonly int PowerPct;
        public readonly int AttackSpeedPct;
        // % of damage blocked
        public readonly int ArmorPct;
        // only does anything for healers
        public readonly int HealPct;

        public StatBonus(int hpPct = 0, int powerPct = 0, int attackSpeedPct = 0, int armorPct = 0, int healPct = 0)
        {
            HpPct = hpPct;
            PowerPct = powerPct;
            AttackSpeedPct = attackSpeedPct;
            ArmorPct = armorPct;
            HealPct = healPct;
        }

        public static StatBonus operator +(StatBonus a, StatBonus b) => new StatBonus(
            a.HpPct + b.HpPct, a.PowerPct + b.PowerPct, a.AttackSpeedPct + b.AttackSpeedPct,
            a.ArmorPct + b.ArmorPct, a.HealPct + b.HealPct);

        public bool IsEmpty => HpPct == 0 && PowerPct == 0 && AttackSpeedPct == 0 && ArmorPct == 0 && HealPct == 0;

        public string Describe()
        {
            var parts = new List<string>();
            if (HpPct != 0) parts.Add($"+{HpPct}% HP");
            if (PowerPct != 0) parts.Add($"+{PowerPct}% attack");
            if (AttackSpeedPct != 0) parts.Add($"+{AttackSpeedPct}% attack speed");
            if (ArmorPct != 0) parts.Add($"{ArmorPct}% armor");
            if (HealPct != 0) parts.Add($"+{HealPct}% healing");
            return parts.Count == 0 ? "No bonus" : string.Join(", ", parts);
        }
    }

    public readonly struct CombatStats
    {
        public const int MaxArmorPct = 75;

        public readonly int MaxHp;
        public readonly int Power;
        public readonly int ArmorPct;
        public readonly int ActionCooldown;

        public CombatStats(int maxHp, int power, int armorPct, int actionCooldown)
        {
            MaxHp = maxHp;
            Power = power;
            ArmorPct = armorPct;
            ActionCooldown = actionCooldown;
        }

        public static CombatStats For(OwnedUnit unit, StatBonus bonus)
        {
            var def = unit.Definition;
            int powerPct = bonus.PowerPct + (def.Role == UnitRole.Healer ? bonus.HealPct : 0);
            int hp = Scale(unit.MaxHp, bonus.HpPct);
            int power = Scale(unit.Power, powerPct);
            int armor = Math.Max(0, Math.Min(bonus.ArmorPct, MaxArmorPct));
            // attack speed can't make it faster than the attack animation
            int cooldown = (int)Math.Round(def.ActionCooldown * 100.0 / (100 + Math.Max(0, bonus.AttackSpeedPct)));
            return new CombatStats(hp, power, armor, Math.Max(def.ActionFrames, cooldown));
        }

        private static int Scale(int value, int pct) => Math.Max(1, (int)Math.Round(value * (100 + pct) / 100.0));
    }
}
