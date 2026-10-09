using System;
using System.Collections.Generic;

namespace AutoBattler.Core
{
    public readonly struct EnemyPlacement
    {
        public readonly OwnedUnit Unit;
        public readonly GridPos Position;

        public EnemyPlacement(OwnedUnit unit, GridPos position)
        {
            Unit = unit;
            Position = position;
        }
    }

    /// <summary>Generates the enemy team for a round. Waves grow in size and star level as rounds advance.</summary>
    public static class EnemyWaves
    {
        public const int MaxEnemies = 8;

        public static int EnemyCount(int round) => Math.Min(1 + round, MaxEnemies);

        public static IReadOnlyList<EnemyPlacement> Generate(int round, IRandom rng)
        {
            int count = EnemyCount(round);
            var free = new List<GridPos>();
            for (int y = BoardLayout.PlayerRows; y < BoardLayout.Height; y++)
                for (int x = 0; x < BoardLayout.Width; x++)
                    free.Add(new GridPos(x, y));

            var wave = new List<EnemyPlacement>(count);
            for (int i = 0; i < count; i++)
            {
                var def = Shop.Pick(rng, round);
                var unit = new OwnedUnit(def, RollStars(round, rng));

                // Melee units stand in the front row (closest to the player) when there is room.
                int frontRow = BoardLayout.PlayerRows;
                int backRow = BoardLayout.Height - 1;
                int preferredRow = def.Range <= 1 ? frontRow : backRow;
                var options = free.FindAll(p => p.Y == preferredRow);
                if (options.Count == 0) options = free;

                var pos = options[rng.Next(options.Count)];
                free.Remove(pos);
                wave.Add(new EnemyPlacement(unit, pos));
            }
            return wave;
        }

        private static int RollStars(int round, IRandom rng)
        {
            if (round >= 8 && rng.Next(100) < 10) return 3;
            if (round >= 4 && rng.Next(100) < (round - 3) * 12) return 2;
            return 1;
        }
    }
}
