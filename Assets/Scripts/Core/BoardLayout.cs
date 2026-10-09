namespace AutoBattler.Core
{
    /// <summary>Fixed dimensions of the battle grid. The player owns the bottom rows, the enemy the top rows.</summary>
    public static class BoardLayout
    {
        public const int Width = 8;
        public const int Height = 6;
        public const int PlayerRows = 3;
        public const int PlayerCells = Width * PlayerRows;

        public static bool InBounds(GridPos p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

        public static bool IsPlayerCell(GridPos p) => InBounds(p) && p.Y < PlayerRows;

        public static GridPos PlayerIndexToPos(int index) => new GridPos(index % Width, index / Width);

        public static int PlayerPosToIndex(GridPos p) => p.Y * Width + p.X;
    }
}
