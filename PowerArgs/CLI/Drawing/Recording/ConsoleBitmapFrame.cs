using PowerArgs.Cli.Physics;

namespace PowerArgs.Cli;

/// <summary>
///     The base class for a console bitmap frame
/// </summary>
public abstract class ConsoleBitmapFrame
{
    protected ConsoleBitmapFrame(TimeSpan timestamp, Size size)
    {
        Timestamp = timestamp;
        Size = size;
    }

    /// <summary>
    ///     The timestamp of the frame
    /// </summary>
    public TimeSpan Timestamp { get; }

    /// <summary>
    ///     The size of the frame
    /// </summary>
    public Size Size { get; }

    /// <summary>
    ///     Paints the current frame onto the given bitmap
    /// </summary>
    /// <param name="bitmap">The image to paint on</param>
    /// <returns>the resulting bitmap, which is the same as what you passed in as long as it was not null</returns>
    public abstract ConsoleBitmap Paint(out ConsoleBitmap bitmap);
}

/// <summary>
///     A raw frame that contains all of the bitmap data needed to construct a frame
/// </summary>
public class ConsoleBitmapRawFrame : ConsoleBitmapFrame
{
    public ConsoleBitmapRawFrame(ConsoleBitmap rawFrame, TimeSpan? timestamp = null, RectF? window = default) 
        : base(timestamp ?? TimeSpan.Zero, new Size(window?.Width ?? rawFrame.Width, window?.Height ?? rawFrame.Height))
    {
        var effectiveLeft = Convert.ToInt32(window?.Left ?? 0);
        var effectiveTop = Convert.ToInt32(window?.Top ?? 0);

        Pixels = new ConsoleCharacter[rawFrame.Width, rawFrame.Height];

        for (var x = 0; x < Math.Min(rawFrame.Width + effectiveLeft, rawFrame.Width); x++)
        for (var y = 0; y < Math.Min(rawFrame.Height + effectiveTop, rawFrame.Height); y++)
        {
            var pixel = rawFrame.GetPixel(effectiveLeft + x, effectiveTop + y);
            Pixels[x, y] = new ConsoleCharacter(pixel.Value);
        }
    }

    /// <summary>
    /// Initializes an empty raw frame
    /// </summary>
    /// <param name="timestamp"></param>
    /// <param name="size"></param>
    public ConsoleBitmapRawFrame(TimeSpan timestamp, Size size) : base(timestamp, size)
    {
        Pixels = new ConsoleCharacter[size.Width, size.Height];
    }

    /// <summary>
    ///     The pixel data for the current frame
    /// </summary>
    public ConsoleCharacter[,] Pixels { get; }

    /// <summary>
    ///     Paints the entire frame onto the given bitmap.  If the given bitmap is null then
    ///     a new bitmap of the correct size will be created and assigned to the reference you
    ///     have provided.  The normal usage pattern is to pass null when reading the first frame,
    ///     which will always be a raw frame.  You can then pass this same bitmap to subsequent calls
    ///     to Paint, and it will work whether the subsequent frames are raw frames or diff frames.
    /// </summary>
    /// <param name="bitmap">The bitmap to paint on or null to create a new bitmap from the raw frame</param>
    /// <returns>the same bitmap you passed in or one that was created for you</returns>
    public override ConsoleBitmap Paint(out ConsoleBitmap bitmap)
    {
        bitmap = new ConsoleBitmap(Size);

        for (var x = 0; x < Size.Width; x++)
        for (var y = 0; y < Size.Height; y++)
            bitmap.Pixels[x, y] = Pixels[x, y];

        return bitmap;
    }
}

/// <summary>
///     A frame that contains only the pixel data for pixels that have changed since the previous frame
/// </summary>
public class ConsoleBitmapDiffFrame : ConsoleBitmapFrame
{
    public ConsoleBitmapDiffFrame(TimeSpan timestamp, Size size) : base(timestamp, size) { }

    /// <summary>
    ///     The pixel diff data, one element for each pixel that has changed since the last frame
    /// </summary>
    public List<ConsoleBitmapPixelDiff> Diffs { get; } = new();

    /// <summary>
    ///     Paints the diff on top of the given image which, unlike with raw frames, cannot be null,
    ///     since a diff frame can only be applied to an existing image
    /// </summary>
    /// <param name="bitmap">the image to apply the diff to</param>
    /// <returns>the same image reference you passed in, updated with the diff</returns>
    public override ConsoleBitmap Paint(out ConsoleBitmap bitmap)
    {
        bitmap = new ConsoleBitmap(Size);

        foreach (var diff in Diffs)
            bitmap.Pixels[diff.X, diff.Y] = diff.Value;

        return bitmap;
    }

}

/// <summary>
///     Represents a changed pixel
/// </summary>
public class ConsoleBitmapPixelDiff
{
    public ConsoleBitmapPixelDiff(int x, int y, ConsoleCharacter value)
    {
        X = x;
        Y = y;
        Value = value;
    }

    /// <summary>
    ///     The x coordinate of the pixel
    /// </summary>
    public int X { get; }

    /// <summary>
    ///     The y coordinate of the pixel
    /// </summary>
    public int Y { get; }

    /// <summary>
    ///     The value of the pixel
    /// </summary>
    public ConsoleCharacter Value { get; }
}