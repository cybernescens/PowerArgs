namespace PowerArgs.Cli;

public readonly struct Point : IEquatable<Point>
{
    public readonly int X;
    public readonly int Y;

    public Point(int x, int y) : this()
    {
        X = x;
        Y = y;
    }

    public bool Equals(Point other) => X == other.X && Y == other.Y;
}