using System;

namespace AutoBattler.Core
{
    // (0,0) = bottom left of the player's side
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        // chebyshev, diagonals count as 1
        public int DistanceTo(GridPos other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

        public int ManhattanTo(GridPos other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);
        public override string ToString() => $"({X},{Y})";
    }
}
