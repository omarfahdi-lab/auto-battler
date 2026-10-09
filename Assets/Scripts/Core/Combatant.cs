namespace AutoBattler.Core
{
    public enum Team
    {
        Player,
        Enemy,
    }

    /// <summary>Battle-time copy of a unit. Damage taken in battle never affects the owned unit.</summary>
    public sealed class Combatant
    {
        public int Id { get; }
        public Team Team { get; }
        public OwnedUnit Unit { get; }
        public GridPos Position { get; internal set; }
        public int Hp { get; internal set; }
        public int Cooldown { get; internal set; }
        public int MoveCooldown { get; internal set; }

        /// <summary>Target of the action currently winding up, or -1.</summary>
        internal int PendingTargetId { get; set; } = -1;
        internal int PendingImpactTick { get; set; }

        public CombatStats Stats { get; }
        /// <summary>Boss units are drawn larger; their stat bonus is already in <see cref="Stats"/>.</summary>
        public bool IsBoss { get; }

        public Combatant(int id, Team team, OwnedUnit unit, GridPos position)
            : this(id, team, unit, position, CombatStats.For(unit, default))
        {
        }

        public Combatant(int id, Team team, OwnedUnit unit, GridPos position, CombatStats stats, bool isBoss = false)
        {
            Id = id;
            Team = team;
            Unit = unit;
            Position = position;
            Stats = stats;
            IsBoss = isBoss;
            Hp = stats.MaxHp;
        }

        public UnitDefinition Definition => Unit.Definition;
        public bool IsAlive => Hp > 0;
        public bool IsActing => PendingTargetId >= 0;
        public int MaxHp => Stats.MaxHp;
        public int Power => Stats.Power;
        public int Range => Unit.Definition.Range;
    }
}
