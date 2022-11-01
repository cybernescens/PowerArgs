using System.Runtime.CompilerServices;
using Microsoft.Toolkit.HighPerformance;

namespace PowerArgs.Cli;

public abstract class Container : ConsoleControl
{
    internal Container() : this(1, 1) { }

    internal Container(int w, int h) : base(w, h) { }

    public abstract IEnumerable<ConsoleControl> Children { get; }

    public IEnumerable<ConsoleControl> Descendents
    {
        get {
            var descendents = new List<ConsoleControl>();
            VisitControlTree(
                d => {
                    descendents.Add(d);
                    return false;
                });

            return descendents.AsReadOnly();
        }
    }

    /// <summary>
    ///     Visits every control in the control tree, recursively, using the visit action provided
    /// </summary>
    /// <param name="visitAction">
    ///     the visitor function that will be run for each child control, the function can return true if
    ///     it wants to stop further visitation
    /// </param>
    /// <param name="root">set to null, used for recursion</param>
    /// <returns>true if the visitation was short circuited by a visitor, false otherwise</returns>
    public bool VisitControlTree(Func<ConsoleControl, bool> visitAction, Container? root = null)
    {
        root ??= this;

        foreach (var child in root.Children)
        {
            var shortCircuit = visitAction(child);
            if (shortCircuit) return true;

            if (child is Container cc)
            {
                shortCircuit = VisitControlTree(visitAction, cc);
                if (shortCircuit) return true;
            }
        }

        return false;
    }

    protected void Compose(ConsoleControl control)
    {
        if (control.IsVisible == false) return;

        control.Paint();

        foreach (var filter in control.RenderFilters)
        {
            filter.Control = control;
            filter.Filter(control.Bitmap);
        }

        if (control.CompositionMode == CompositionMode.PaintOver)
        {
            ComposePaintOver(control);
            return;
        }

        if (control.CompositionMode == CompositionMode.BlendBackground)
        {
            ComposeBlendBackground(control);
            return;
        }

        if (control.CompositionMode == CompositionMode.BlendVisible)
        {
            ComposeBlendVisible(control);
            return;
        }

        throw new InvalidOperationException($"unknown composition mode: {control.CompositionMode}");
    }

    protected virtual (int X, int Y) Transform(ConsoleControl c) => (c.X, c.Y);

    private void ComposePaintOver(ConsoleControl control)
    {
        var position = Transform(control);

        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);

        var pixels = Bitmap.Pixels.AsSpan2D();

        /* may not be right */

        for (var i = minY * minX + minX; i < maxX * maxY; i++)
        {
            var x = i % maxX;
            var y = i / maxX;
            pixels[x, y] = control.Bitmap.Pixels[x - position.X, y - position.Y];
        }
    }

    private void ComposeBlendBackground(ConsoleControl control)
    {
        var position = Transform(control);
        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);

        var pixels = Bitmap.Pixels.AsSpan2D();

        for (var i = minY * minX + minX; i < maxX * maxY; i++)
        {
            var x = i % maxX;
            var y = i / maxX;

            var controlPixel = control.Bitmap.Pixels[x - position.X, y - position.Y];

            if (controlPixel.BackgroundColor != ConsoleString.DefaultBackgroundColor)
            {
                pixels[x, y] = controlPixel;
                continue;
            }

            var myPixel = Bitmap.Pixels[x, y];

            if (myPixel.BackgroundColor != ConsoleString.DefaultBackgroundColor)
            {
                var composedValue = new ConsoleCharacter(
                    controlPixel.Value,
                    controlPixel.ForegroundColor,
                    myPixel.BackgroundColor);

                pixels[x, y] = composedValue;
            }
            else
            {
                pixels[x, y] = controlPixel;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ControlPixelCanBenRendered(in ConsoleCharacter pixel) =>
        pixel.Value == ' '
            ? pixel.BackgroundColor != Background
            : pixel.ForegroundColor != Background || pixel.BackgroundColor != Background;

    private void ComposeBlendVisible(ConsoleControl control)
    {
        var position = Transform(control);
        var minX = Math.Max(position.X, 0);
        var minY = Math.Max(position.Y, 0);
        var maxX = Math.Min(Width, position.X + control.Width);
        var maxY = Math.Min(Height, position.Y + control.Height);
        
        var pixels = Bitmap.Pixels.AsSpan2D();

        for (var i = minY * minX + minX; i < maxX * maxY; i++)
        {
            var x = i % maxX;
            var y = i / maxX;

            var controlPixel = control.Bitmap.Pixels[x - position.X, y - position.Y];

            if (ControlPixelCanBenRendered(controlPixel))
            {
                pixels[x, y] = controlPixel;
            }
        }
    }
}