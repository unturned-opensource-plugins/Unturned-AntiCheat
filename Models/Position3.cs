using System;

namespace Emqo.Unturned_AntiCheat.Models
{
    public readonly struct Position3
    {
        public static readonly Position3 Zero = new Position3(0d, 0d, 0d);

        public Position3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public double HorizontalMagnitude => Math.Sqrt(X * X + Z * Z);

        public static Position3 operator -(Position3 left, Position3 right)
        {
            return new Position3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }
    }
}
