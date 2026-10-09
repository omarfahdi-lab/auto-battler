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

        public Combatant(int id, Team team, OwnedUnit unit, GridPos position)
        {
            Id = id;
            Team = team;
            Unit = unit;
            Position = position;
            Hp = unit.MaxHp;
        }

        public UnitDefinition Definition => Unit.Definition;
        public bool IsAlive => Hp > 0;
        public bool IsActing => PendingTargetId >= 0;
        public int MaxHp => Unit.MaxHp;
        public int Power => Unit.Power;
        public int Range => Unit.Definition.Range;
    }
}
