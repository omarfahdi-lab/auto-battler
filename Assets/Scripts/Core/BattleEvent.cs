namespace AutoBattler.Core
{
    public enum BattleEventType
    {
        Move,
        // attack/heal started, actually lands ImpactTicks later
        ActionStart,
        // arrow fired, hits after Ticks ticks
        ProjectileLaunch,
        Damage,
        Heal,
        Death,
    }

    public readonly struct BattleEvent
    {
        public readonly BattleEventType Type;
        public readonly int SourceId;
        public readonly int TargetId;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly int Amount;
        public readonly int Ticks;

        private BattleEvent(BattleEventType type, int sourceId, int targetId, GridPos from, GridPos to, int amount, int ticks)
        {
            Type = type;
            SourceId = sourceId;
            TargetId = targetId;
            From = from;
            To = to;
            Amount = amount;
            Ticks = ticks;
        }

        public static BattleEvent Move(int id, GridPos from, GridPos to, int ticks) =>
            new BattleEvent(BattleEventType.Move, id, -1, from, to, 0, ticks);

        public static BattleEvent ActionStart(int id, int targetId, GridPos from, GridPos to) =>
            new BattleEvent(BattleEventType.ActionStart, id, targetId, from, to, 0, 0);

        public static BattleEvent ProjectileLaunch(int id, int targetId, GridPos from, GridPos to, int ticks) =>
            new BattleEvent(BattleEventType.ProjectileLaunch, id, targetId, from, to, 0, ticks);

        public static BattleEvent Damage(int sourceId, int targetId, int amount) =>
            new BattleEvent(BattleEventType.Damage, sourceId, targetId, default, default, amount, 0);

        public static BattleEvent Heal(int sourceId, int targetId, int amount) =>
            new BattleEvent(BattleEventType.Heal, sourceId, targetId, default, default, amount, 0);

        public static BattleEvent Death(int id) =>
            new BattleEvent(BattleEventType.Death, id, -1, default, default, 0, 0);
    }
}
