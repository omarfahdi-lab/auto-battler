using System.Collections.Generic;

namespace AutoBattler.Core
{
    public enum ItemStat
    {
        Attack,
        Hp,
        AttackSpeed,
    }

    /// <summary>Equipment for a single unit. Each unit holds one item.</summary>
    public sealed class ItemDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public ItemStat Stat { get; }
        public StatBonus Bonus { get; }
        public bool Greater { get; }

        public ItemDefinition(string id, string name, ItemStat stat, StatBonus bonus, bool greater)
        {
            Id = id;
            Name = name;
            Stat = stat;
            Bonus = bonus;
            Greater = greater;
        }

        public string Description => Bonus.Describe();
        public override string ToString() => Name;
    }

    /// <summary>A permanent team-wide bonus for the rest of the run.</summary>
    public sealed class AugmentDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public StatBonus TeamBonus { get; }
        public int GoldPerRound { get; }
        public bool Greater { get; }

        public AugmentDefinition(string id, string name, StatBonus teamBonus, int goldPerRound, bool greater)
        {
            Id = id;
            Name = name;
            TeamBonus = teamBonus;
            GoldPerRound = goldPerRound;
            Greater = greater;
        }

        public string Description => GoldPerRound > 0
            ? $"+{GoldPerRound} gold every round"
            : $"All units: {TeamBonus.Describe()}";

        public override string ToString() => Name;
    }

    public enum RewardKind
    {
        Item,
        Augment,
    }

    public sealed class RewardOption
    {
        public RewardKind Kind { get; }
        public ItemDefinition Item { get; }
        public AugmentDefinition Augment { get; }

        private RewardOption(RewardKind kind, ItemDefinition item, AugmentDefinition augment)
        {
            Kind = kind;
            Item = item;
            Augment = augment;
        }

        public static RewardOption Of(ItemDefinition item) => new RewardOption(RewardKind.Item, item, null);
        public static RewardOption Of(AugmentDefinition augment) => new RewardOption(RewardKind.Augment, null, augment);

        public string Name => Kind == RewardKind.Item ? Item.Name : Augment.Name;
        public string Description => Kind == RewardKind.Item ? $"Item: {Item.Description}" : Augment.Description;
        public bool Greater => Kind == RewardKind.Item ? Item.Greater : Augment.Greater;
        public override string ToString() => Name;
    }

    /// <summary>The pick-one-of-three offered after every won round. Boss wins offer greater versions.</summary>
    public static class Rewards
    {
        public const int ChoiceCount = 3;

        public static IReadOnlyList<ItemDefinition> Items => Balance.Items;
        public static IReadOnlyList<ItemDefinition> GreaterItems => Balance.GreaterItems;
        public static IReadOnlyList<AugmentDefinition> Augments => Balance.Augments;
        public static IReadOnlyList<AugmentDefinition> GreaterAugments => Balance.GreaterAugments;

        /// <summary>Three distinct options: at least one item and at least one augment, in random order.</summary>
        public static IReadOnlyList<RewardOption> Generate(IRandom rng, bool bossWin)
        {
            var items = new List<ItemDefinition>(bossWin ? GreaterItems : Items);
            var augments = new List<AugmentDefinition>(bossWin ? GreaterAugments : Augments);

            var options = new List<RewardOption> { RewardOption.Of(Take(items, rng)), RewardOption.Of(Take(augments, rng)) };
            int remaining = items.Count + augments.Count;
            while (options.Count < ChoiceCount && remaining > 0)
            {
                int pick = rng.Next(remaining);
                options.Add(pick < items.Count
                    ? RewardOption.Of(Take(items, pick))
                    : RewardOption.Of(Take(augments, pick - items.Count)));
                remaining--;
            }

            // Fisher-Yates so the guaranteed item isn't always first.
            for (int i = options.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = options[i];
                options[i] = options[j];
                options[j] = tmp;
            }
            return options;
        }

        private static T Take<T>(List<T> pool, IRandom rng) => Take(pool, rng.Next(pool.Count));

        private static T Take<T>(List<T> pool, int index)
        {
            var value = pool[index];
            pool.RemoveAt(index);
            return value;
        }
    }
}
