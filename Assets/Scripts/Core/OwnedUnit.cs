using System;

namespace AutoBattler.Core
{
    // 3 of the same unit + star merge into the next star
    public sealed class OwnedUnit
    {
        public const int MaxStars = 3;
        private static readonly int[] StatMultiplier = { 0, 1, 2, 4 };

        public UnitDefinition Definition { get; }
        public int Stars { get; }
        public ItemDefinition Item { get; internal set; }

        public OwnedUnit(UnitDefinition definition, int stars = 1)
        {
            if (stars < 1 || stars > MaxStars) throw new ArgumentOutOfRangeException(nameof(stars));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Stars = stars;
        }

        public int MaxHp => Definition.MaxHp * StatMultiplier[Stars];
        public int Power => Definition.Power * StatMultiplier[Stars];

        // how many 1* copies this is worth
        public int CopyCount => (int)Math.Pow(3, Stars - 1);

        public int SellValue => Definition.Cost * CopyCount;

        public bool CanMergeWith(OwnedUnit other) =>
            other != null && other.Definition == Definition && other.Stars == Stars && Stars < MaxStars;

        public override string ToString() => $"{Definition.Name} {Stars}*";
    }
}
