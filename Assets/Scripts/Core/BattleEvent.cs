namespace AutoBattler.Core
{
    public enum BattleEventType
    {
        Move,
        Attack,
        Death,
    }

    /// <summary>Something that happened during a battle tick, for the presentation layer to animate.</summary>
    public readonly struct BattleEvent
    {
        public readonly BattleEventType Type;
        public readonly int SourceId;
        public readonly int TargetId;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly int Damage;

        private BattleEvent(BattleEventType type, int sourceId, int targetId, GridPos from, GridPos to, int damage)
        {
            Type = type;
            SourceId = sourceId;
            TargetId = targetId;
            From = from;
            To = to;
            Damage = damage;
        }

        public static BattleEvent Move(int id, GridPos from, GridPos to) =>
            new BattleEvent(BattleEventType.Move, id, -1, from, to, 0);

        public static BattleEvent Attack(int sourceId, int targetId, int damage) =>
            new BattleEvent(BattleEventType.Attack, sourceId, targetId, default, default, damage);

        public static BattleEvent Death(int id) =>
            new BattleEvent(BattleEventType.Death, id, -1, default, default, 0);
    }
}
