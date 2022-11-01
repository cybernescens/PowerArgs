namespace PowerArgs.Cli;

/// <summary>
///     A control that displays a ConsoleBitmap
/// </summary>
public class BitmapControl : ConsoleControl
{
    /// <summary>
    ///     Creates a new Bitmap control
    /// </summary>
    public BitmapControl() 
    {
        SubscribeForLifetime(this, nameof(AutoSize), BitmapOrAutoSizeChanged);
        SubscribeForLifetime(this, nameof(Bitmap), BitmapOrAutoSizeChanged);
    }

    /// <summary>
    ///     The Bitmap image to render in the control
    /// </summary>
    public ConsoleBitmap? Bitmap
    {
        get => Get<ConsoleBitmap>();
        set => Set(value);
    }

    /// <summary>
    ///     If true then this control will auto size itself based on its target bitmap
    /// </summary>
    public bool AutoSize
    {
        get => Get<bool>();
        set => Set(value);
    }

    private void BitmapOrAutoSizeChanged()
    {
        if (AutoSize && Bitmap != null)
        {
            Width = Bitmap.Width;
            Height = Bitmap.Height;
            Application?.RequestPaint();
        }
    }

    /// <summary>
    ///     Draws the bitmap
    /// </summary>
    /// <param name="context">the pain context</param>
    protected override void OnPaint(ConsoleBitmap context)
    {
        if (Bitmap == null) return;

        for (var x = 0; x < Bitmap.Width && x < Width; x++)
        {
            for (var y = 0; y < Bitmap.Height && y < Height; y++)
            {
                var pixel = Bitmap.GetPixel(x, y);
                context.DrawPoint(pixel, x, y);
            }
        }
    }
}