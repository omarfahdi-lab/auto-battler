using System;

namespace AutoBattler.Core
{
    /// <summary>A unit the player (or an enemy wave) owns. Three copies of the same star level merge into one of the next level.</summary>
    public sealed class OwnedUnit
    {
        public const int MaxStars = 3;
        private static readonly int[] StatMultiplier = { 0, 1, 2, 4 };

        public UnitDefinition Definition { get; }
        public int Stars { get; }
        /// <summary>Equipped item, or null. Managed by <see cref="GameSession"/>.</summary>
        public ItemDefinition Item { get; internal set; }

        public OwnedUnit(UnitDefinition definition, int stars = 1)
        {
            if (stars < 1 || stars > MaxStars) throw new ArgumentOutOfRangeException(nameof(stars));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Stars = stars;
        }

        public int MaxHp => Definition.MaxHp * StatMultiplier[Stars];
        public int Power => Definition.Power * StatMultiplier[Stars];

        /// <summary>Number of 1-star copies this unit represents.</summary>
        public int CopyCount => (int)Math.Pow(3, Stars - 1);

        public int SellValue => Definition.Cost * CopyCount;

        public bool CanMergeWith(OwnedUnit other) =>
            other != null && other.Definition == Definition && other.Stars == Stars && Stars < MaxStars;

        public override string ToString() => $"{Definition.Name} {Stars}*";
    }
}
