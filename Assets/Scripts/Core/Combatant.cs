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

        public Combatant(int id, Team team, OwnedUnit unit, GridPos position)
        {
            Id = id;
            Team = team;
            Unit = unit;
            Position = position;
            Hp = unit.MaxHp;
        }

        public bool IsAlive => Hp > 0;
        public int MaxHp => Unit.MaxHp;
        public int Attack => Unit.Attack;
        public int Range => Unit.Definition.Range;
    }
}
