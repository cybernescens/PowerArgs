using PowerArgs.Cli.Physics;

namespace PowerArgs.Cli;

public class Rectangular : ObservableObject, ICollider
{
    private RectF fBounds;
    private int x, y, w, h;

    private int z;

    public int Width
    {
        get => w;
        set {
            if (w == value) return;

            w = value;
            fBounds = new RectF(fBounds.Left, fBounds.Top, w, fBounds.Height);
            FirePropertyChanged(nameof(Bounds));
        }
    }

    public int Height
    {
        get => h;
        set {
            if (h == value) return;

            h = value;
            fBounds = new RectF(fBounds.Left, fBounds.Top, fBounds.Width, h);
            FirePropertyChanged(nameof(Bounds));
        }
    }

    public int X
    {
        get => x;
        set {
            if (x == value) return;

            x = value;
            fBounds = new RectF(x, fBounds.Top, fBounds.Width, fBounds.Height);
            FirePropertyChanged(nameof(Bounds));
        }
    }

    public int Y
    {
        get => y;
        set {
            if (y == value) return;

            y = value;
            fBounds = new RectF(fBounds.Left, y, fBounds.Width, fBounds.Height);
            FirePropertyChanged(nameof(Bounds));
        }
    }

    public float Left => X;

    public float Top => Y;

    public int ZIndex
    {
        get => z;
        set => SetHardIf(ref z, value, () => z != value);
    }

    public int ColliderHashCode { get; internal set; }

    public RectF Bounds
    {
        get => fBounds;
        set {
            fBounds = value;
            var newX = ConsoleMath.Round(value.Left);
            var newY = ConsoleMath.Round(value.Top);
            var newW = ConsoleMath.Round(value.Width);
            var newH = ConsoleMath.Round(value.Height);
            if (newX == x && newY == y && newW == w && newH == h) return;

            x = newX;
            y = newY;
            w = newW;
            h = newH;

            FirePropertyChanged(nameof(Bounds));
        }
    }

    public bool CanCollideWith(ICollider other) => true;

    public RectF MassBounds => new(X, Y, Width, Height);
}