using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.CompilerServices;
using Microsoft.Toolkit.HighPerformance;
using PowerArgs.Cli.Physics;

namespace PowerArgs.Cli;

/// <summary>
///     A data structure representing a 2d image that can be pained in
///     a console window
/// </summary>
public class ConsoleBitmap
{
    // larger is faster, but may cause gaps
    private const float DrawPrecision = .5f;
    private static readonly ChunkPool chunkPool = new();
    private static readonly List<Chunk> chunksOnLine = new();
    private static readonly PaintBuffer paintBuilder = new();

    internal static ThreadLocal<Point[]> LineBuffer = new(() => new Point[1000]);

    private Size bounds;
    private int lastBufferWidth;
    private ConsoleCharacter[,] lastDrawnPixels;
    private ConsoleCharacter[,] pixels;

    private bool wasFancy;

    public ConsoleCharacter[,] Pixels => pixels;

    /// <summary>
    ///     Creates a new ConsoleBitmap
    /// </summary>
    /// <param name="w">the width of the image</param>
    /// <param name="h">the height of the image</param>
    public ConsoleBitmap(int w, int h) : this(new Size(w, h)) { }

    /// <summary>
    ///     Creates a new ConsoleBitmap
    /// </summary>
    /// <param name="bounds">the area of the image</param>
    public ConsoleBitmap(Size bounds)
    {
        this.bounds = bounds;
        Console = ConsoleProvider.Current;
        lastBufferWidth = Console.BufferWidth;
        pixels = new ConsoleCharacter[Width, Height];
        lastDrawnPixels = new ConsoleCharacter[Width, Height];
        Fill(ConsoleCharacter.Default);
    }

    /// <summary>
    ///    The width and height of the image, in number of character pixels for each
    /// </summary>
    public Size Bounds => bounds;

    /// <summary>
    ///     The width of the image, in number of character pixels
    /// </summary>
    public int Width => bounds.Width;

    /// <summary>
    ///     The height of the image, in number of character pixels
    /// </summary>
    public int Height => bounds.Height;

    /// <summary>
    ///     The console to target when the Paint method is called
    /// </summary>
    public IConsoleProvider Console { get; set; }

    /// <summary>
    ///     Converts this ConsoleBitmap to a ConsoleString
    /// </summary>
    /// <param name="trimMode">
    ///     if false (the default), unformatted whitespace at the end of each line will be included as
    ///     whitespace in the return value. If true, that whitespace will be trimmed from the return value.
    /// </param>
    /// <returns>the bitmap as a ConsoleString</returns>
    public ConsoleString ToConsoleString(bool trimMode = false)
    {
        var chars = new Stack<ConsoleCharacter>();
       
        for (var i = 0; i < Width * Height; i++)
        {
            var x = i % Width;
            var y = i / Width;
            var endOfLine = x == 0 && i > 0;

            if (endOfLine)
            {
                while (trimMode && chars.Count > 0 && chars.Peek().Equals(ConsoleCharacter.Default))
                    chars.Pop();

                chars.Push(ConsoleCharacter.LineFeed);
            }

            chars.Push(pixels[x, y]);
        }

        while (trimMode && chars.Count > 0 && chars.Peek().Equals(ConsoleCharacter.Default))
            chars.Pop();

        return new ConsoleString(chars.Reverse());
    }

    /// <summary>
    ///     Resizes this image, preserving the data in the pixels that remain in the new area
    /// </summary>
    /// <param name="w">the new width</param>
    /// <param name="h">the new height</param>
    public void Resize(int w, int h)
    {
        if (w == Width && h == Height) 
            return;

        var newPixels = new ConsoleCharacter[w, h];
        var newLastDrawnCharacters = new ConsoleCharacter[w, h];

        pixels = newPixels;
        lastDrawnPixels = newLastDrawnCharacters;
        bounds = new Size(w, h);
        Invalidate();
    }

    /// <summary>
    ///     Gets the pixel at the given location
    /// </summary>
    /// <param name="x">the x coordinate</param>
    /// <param name="y">the y coordinate</param>
    /// <returns>the pixel at the given location</returns>
    public ConsoleCharacter GetPixel(int x, int y) => pixels[x, y];

    public void SetPixel(int x, int y, in ConsoleCharacter c) { pixels[x, y] = c; }

    //private static void Initialize(out ConsoleCharacter[,] plane, int width, int height, ConsoleCharacter pen = default)
    //{
    //    var memory = plane.AsSpan2D();

    //    for (var i = 0; i < width * height; i++)
    //    {
    //        var x = i % width;
    //        var y = i / height;
    //        memory[x, y] = pen;
    //    }

    //    plane = memory;
    //}

    /// <summary>
    ///     Creates a snapshot of the cursor position
    /// </summary>
    /// <returns>a snapshot of the cursor position</returns>
    public ConsoleSnapshot CreateSnapshot()
    {
        var snapshot = new ConsoleSnapshot(0, 0, Console);
        return snapshot;
    }

    public bool IsInBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    ///     Draws the given string onto the bitmap
    /// </summary>
    /// <param name="str">the value to write</param>
    /// <param name="x">the x coordinate to draw the string's fist character</param>
    /// <param name="y">the y coordinate to draw the string's first character </param>
    /// <param name="vert">if true, draw vertically, else draw horizontally</param>
    public void DrawString(string? str, int x, int y, bool vert = false)
    {
        DrawString(new ConsoleString(str), x, y, vert);
    }

    /// <summary>
    ///     Draws a filled in rectangle bounded by the given coordinates
    ///     using the current pen
    /// </summary>
    /// <param name="x">the left of the rectangle</param>
    /// <param name="y">the top of the rectangle</param>
    /// <param name="w">the width of the rectangle</param>
    /// <param name="h">the height of the rectangle</param>
    public void FillRect(in ConsoleCharacter pen, int x, int y, int w, int h)
    {
        var maxX = Math.Min(x + w, Width);
        var maxY = Math.Min(y + h, Height);

        var minX = Math.Max(x, 0);
        var minY = Math.Max(y, 0);

        var memory = pixels.AsSpan2D();

        for (var i = minY * minX + minX; i < maxX * maxY; i++)
        {
            var xx = i % maxX;
            var yy = i / maxX;
            memory[xx, yy] = pen;
        }
    }

    public void FillRect(in RGB color, int x, int y, int w, int h) =>
        FillRect(new ConsoleCharacter(' ', backgroundColor: color), x, y, w, h);

    public void Fill(in RGB color) => Fill(new ConsoleCharacter(' ', backgroundColor: color));

    public void Fill(in ConsoleCharacter pen)
    {
        var memory = pixels.AsSpan2D();

        for (var i = 0; i < Width * Height; i++)
        {
            var x = i % Width;
            var y = i / Width;
            memory[x, y] = pen;
        }
    }

    /// <summary>
    ///     Draws a filled in rectangle bounded by the given coordinates
    ///     using the current pen, without performing bounds checks
    /// </summary>
    /// <param name="x">the left of the rectangle</param>
    /// <param name="y">the top of the rectangle</param>
    /// <param name="w">the width of the rectangle</param>
    /// <param name="h">the height of the rectangle</param>
    public void FillRectUnsafe(in ConsoleCharacter pen, int x, int y, int w, int h)
    {
        var maxX = x + w;
        var maxY = y + h;

        //var memory = pixels.AsSpan2D();

        for (var i = x * y + x; i < maxX * maxY; i++)
        {
            var xx = i % Width;
            var yy = i / Width;
            pixels[xx, yy] = pen;
        }
    }

    /// <summary>
    ///     Draws an unfilled in rectangle bounded by the given coordinates
    ///     using the current pen
    /// </summary>
    /// <param name="x">the left of the rectangle</param>
    /// <param name="y">the top of the rectangle</param>
    /// <param name="w">the width of the rectangle</param>
    /// <param name="h">the height of the rectangle</param>
    public void DrawRect(in ConsoleCharacter pen, int x, int y, int w, int h)
    {
        var maxX = Math.Min(x + w, Width);
        var maxY = Math.Min(y + h, Height);
        var minX = Math.Max(x, 0);
        var minY = Math.Max(y, 0);

        // left vertical line
        for (var yd = minY; yd < maxY; yd++)
            pixels[minX, yd] = pen;

        // right vertical line
        for (var yd = minY; yd < maxY; yd++)
            pixels[maxX, yd] = pen;

        // top horizontal line
        for (var xd = minX; xd < maxX; xd++)
            pixels[xd, minY] = pen;

        // bottom horizontal line
        for (var xd = minX; xd < maxX; xd++)
            pixels[xd, maxY] = pen;
    }

    /// <summary>
    ///     Draws the given string onto the bitmap
    /// </summary>
    /// <param name="str">the value to write</param>
    /// <param name="x">the x coordinate to draw the string's fist character</param>
    /// <param name="y">the y coordinate to draw the string's first character </param>
    /// <param name="vert">if true, draw vertically, else draw horizontally</param>
    public void DrawString(ConsoleString str, int x, int y, bool vert = false)
    {
        var xStart = x;
        var span = str.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var character = span[i];
            if (character.Value == '\n')
            {
                y++;
                x = xStart;
            }
            else if (character.Value == '\r')
            {
                // ignore
            }
            else if (IsInBounds(x, y))
            {
                pixels[x, y] = character;
                if (vert)
                {
                    y++;
                }
                else
                {
                    x++;
                }
            }
        }
    }

    /// <summary>
    ///     Draw a single pixel value at the given point using the current pen
    /// </summary>
    /// <param name="x">the x coordinate</param>
    /// <param name="y">the y coordinate</param>
    public void DrawPoint(in ConsoleCharacter pen, int x, int y)
    {
        if (IsInBounds(x, y))
        {
            pixels[x, y] = pen;
        }
    }

    /// <summary>
    ///     Draw a line segment between the given points
    /// </summary>
    /// <param name="x1">the x coordinate of the first point</param>
    /// <param name="y1">the y coordinate of the first point</param>
    /// <param name="x2">the x coordinate of the second point</param>
    /// <param name="y2">the y coordinate of the second point</param>
    public void DrawLine(in ConsoleCharacter pen, int x1, int y1, int x2, int y2)
    {
        var len = DefineLineBuffered(x1, y1, x2, y2, out var buffer);

        for (var i = 0; i < len; i++)
        {
            var point = buffer[i];
            if (IsInBounds(point.X, point.Y))
            {
                pixels[point.X, point.Y] = pen;
            }
        }
    }

    public static int DefineLineBuffered(int x1, int y1, int x2, int y2, out Point[] buffer)
    {
        buffer = LineBuffer.Value!;

        var ret = 0;
        if (x1 == x2)
        {
            var yMin = Math.Min(y1, y2);
            var yMax = Math.Max(y1, y2);
            for (var y = yMin; y < yMax; y++)
                buffer[ret++] = new Point(x1, y);
        }
        else if (y1 == y2)
        {
            var xMin = Math.Min(x1, x2);
            var xMax = Math.Max(x1, x2);
            for (var x = xMin; x < xMax; x++)
                buffer[ret++] = new Point(x, y1);
        }
        else
        {
            var slope = ((float)y2 - y1) / ((float)x2 - x1);

            var dx = Math.Abs(x1 - x2);
            var dy = Math.Abs(y1 - y2);

            var last = new Point();
            if (dy > dx)
            {
                for (float x = x1; x < x2; x += DrawPrecision)
                {
                    var y = slope + (x - x1) + y1;
                    var xInt = ConsoleMath.Round(x);
                    var yInt = ConsoleMath.Round(y);
                    var p = new Point(xInt, yInt);
                    if (p.Equals(last) == false)
                    {
                        buffer[ret++] = p;
                        last = p;
                    }
                }

                for (float x = x2; x < x1; x += DrawPrecision)
                {
                    var y = slope + (x - x1) + y1;
                    var xInt = ConsoleMath.Round(x);
                    var yInt = ConsoleMath.Round(y);
                    var p = new Point(xInt, yInt);
                    if (p.Equals(last) == false)
                    {
                        buffer[ret++] = p;
                        last = p;
                    }
                }
            }
            else
            {
                for (float y = y1; y < y2; y += DrawPrecision)
                {
                    var x = (y - y1) / slope + x1;
                    var xInt = ConsoleMath.Round(x);
                    var yInt = ConsoleMath.Round(y);
                    var p = new Point(xInt, yInt);
                    if (p.Equals(last) == false)
                    {
                        buffer[ret++] = p;
                        last = p;
                    }
                }

                for (float y = y2; y < y1; y += DrawPrecision)
                {
                    var x = (y - y1) / slope + x1;
                    var xInt = ConsoleMath.Round(x);
                    var yInt = ConsoleMath.Round(y);
                    var p = new Point(xInt, yInt);
                    if (p.Equals(last) == false)
                    {
                        buffer[ret++] = p;
                        last = p;
                    }
                }
            }
        }

        return ret;
    }

    /// <summary>
    ///     Makes a copy of this bitmap
    /// </summary>
    /// <returns>a copy of this bitmap</returns>
    public ConsoleBitmap Clone()
    {
        var ret = new ConsoleBitmap(Width, Height);
        for (var i = 0; i < Width * Height; i++)
        {
            var x = i % Width;
            var y = i / Width;
            ret.pixels[x, y] = new ConsoleCharacter(
                pixels[x, y].Value,
                pixels[x, y].ForegroundColor,
                pixels[x, y].BackgroundColor,
                pixels[x, y].IsUnderlined);
        }

        return ret;
    }

    public void Paint()
    {
        if (ConsoleProvider.Fancy != wasFancy)
        {
            Invalidate();
            wasFancy = ConsoleProvider.Fancy;

            if (ConsoleProvider.Fancy)
            {
                Console.Write(Ansi.Cursor.Hide + Ansi.Text.BlinkOff);
            }
        }

        if (ConsoleProvider.Fancy)
        {
            PaintNew();
        }
        else
        {
            PaintOld();
        }
    }

    /// <summary>
    ///     Paints this image to the current Console
    /// </summary>
    public void PaintOld()
    {
        if (Console.WindowHeight == 0) return;

        var changed = false;
        if (lastBufferWidth != Console.BufferWidth)
        {
            lastBufferWidth = Console.BufferWidth;
            Invalidate();
            Console.Clear();
            changed = true;
        }

        try
        {
            Chunk? currentChunk = null;
            var chunksOnLine = new List<Chunk>();
            for (var y = 0; y < Height; y++)
            {
                var changeOnLine = false;
                for (var x = 0; x < Width; x++)
                {
                    var pixel = pixels[x, y];
                    var lastDrawn = lastDrawnPixels[x, y];
                    var pixelChanged = pixel != lastDrawn;
                    changeOnLine = changeOnLine || pixelChanged;
                    var val = pixel.Value;
                    var fg = pixel.ForegroundColor;
                    var bg = pixel.BackgroundColor;

                    if (currentChunk == null)
                    {
                        // first pixel always gets added to the current empty chunk
                        currentChunk = new Chunk(Width);
                        currentChunk.FG = fg;
                        currentChunk.BG = bg;
                        currentChunk.HasChanged = pixelChanged;
                        currentChunk.Add(val);
                    }
                    else if (currentChunk.HasChanged == false && pixelChanged == false)
                    {
                        // characters that have not changed get chunked even if their styles differ
                        currentChunk.Add(val);
                    }
                    else if (currentChunk.HasChanged && pixelChanged && fg == currentChunk.FG && bg == currentChunk.BG)
                    {
                        // characters that have changed only get chunked if their styles match to minimize the number of writes
                        currentChunk.Add(val);
                    }
                    else
                    {
                        // either the styles of consecutive changing characters differ or we've gone from a non changed character to a changed one
                        // in either case we end the current chunk and start a new one
                        chunksOnLine.Add(currentChunk);
                        currentChunk = new Chunk(Width);
                        currentChunk.FG = fg;
                        currentChunk.BG = bg;
                        currentChunk.HasChanged = pixelChanged;
                        currentChunk.Add(val);
                    }

                    lastDrawnPixels[x, y] = pixel;
                }

                if (currentChunk?.Length > 0)
                {
                    chunksOnLine.Add(currentChunk);
                }

                currentChunk = null;

                if (changeOnLine)
                {
                    Console.CursorTop = y; // we know there will be a change on this line so move the cursor top
                    var left = 0;
                    var leftChanged = true;
                    for (var i = 0; i < chunksOnLine.Count; i++)
                    {
                        var chunk = chunksOnLine[i];
                        if (chunk.HasChanged)
                        {
                            if (leftChanged)
                            {
                                Console.CursorLeft = left;
                                leftChanged = false;
                            }

                            Console.ForegroundColor = chunk.FG;
                            Console.BackgroundColor = chunk.BG;
                            Console.Write(chunk.ToString());
                            left += chunk.Length;
                            changed = true;
                        }
                        else
                        {
                            left += chunk.Length;
                            leftChanged = true;
                        }
                    }
                }

                chunksOnLine.Clear();
            }

            if (changed)
            {
                Console.CursorLeft = 0;
                Console.CursorTop = 0;
                Console.ForegroundColor = ConsoleString.DefaultForegroundColor;
                Console.BackgroundColor = ConsoleString.DefaultBackgroundColor;
            }
        }
        catch (IOException)
        {
            Invalidate();
            PaintOld();
        }
        catch (ArgumentOutOfRangeException)
        {
            Invalidate();
            PaintOld();
        }
    }

    public void Dump(string dest)
    {
        using var b = new Bitmap(Width * 10, Height * 20);
        using var g = Graphics.FromImage(b);

        g.CompositingQuality = CompositingQuality.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var pix = GetPixel(x, y);
                var bgColor = Color.FromArgb(pix.BackgroundColor.R, pix.BackgroundColor.G, pix.BackgroundColor.B);
                var fgColor = Color.FromArgb(pix.ForegroundColor.R, pix.ForegroundColor.G, pix.ForegroundColor.B);
                var imgX = x * 10;
                var imgY = y * 20;

                g.FillRectangle(new SolidBrush(bgColor), imgX, imgY, 10, 20);
                g.DrawString(
                    pix.Value.ToString(),
                    new Font("Consolas", 12),
                    new SolidBrush(fgColor),
                    imgX - 2,
                    imgY);
            }
        }

        b.Save(dest, ImageFormat.Png);
    }

    public void PaintNew()
    {
        if (Console.WindowHeight == 0) return;

        if (lastBufferWidth != Console.BufferWidth)
        {
            lastBufferWidth = Console.BufferWidth;
            Invalidate();
            Console.Clear();
        }

        try
        {
            paintBuilder.Clear();
            Chunk? currentChunk = null;
            for (var y = 0; y < Height; y++)
            {
                var changeOnLine = false;
                for (var x = 0; x < Width; x++)
                {
                    var pixel = pixels[x, y];
                    var lastDrawn = lastDrawnPixels[x, y];
                    var pixelChanged = pixel != lastDrawn;
                    changeOnLine = changeOnLine || pixelChanged;

                    var val = pixel.Value;
                    var fg = pixel.ForegroundColor;
                    var bg = pixel.BackgroundColor;
                    var underlined = pixel.IsUnderlined;

                    if (currentChunk == null)
                    {
                        // first pixel always gets added to the current empty chunk
                        currentChunk = chunkPool.Get(Width);
                        currentChunk.FG = fg;
                        currentChunk.BG = bg;
                        currentChunk.Underlined = underlined;
                        currentChunk.HasChanged = pixelChanged;
                        currentChunk.Add(val);
                    }
                    else if (currentChunk.HasChanged == false && pixelChanged == false)
                    {
                        // characters that have not changed get chunked even if their styles differ
                        currentChunk.Add(val);
                    }
                    else if (currentChunk.HasChanged &&
                             pixelChanged &&
                             fg == currentChunk.FG &&
                             bg == currentChunk.BG &&
                             underlined == currentChunk.Underlined)
                    {
                        // characters that have changed only get chunked if their styles match to minimize the number of writes
                        currentChunk.Add(val);
                    }
                    else
                    {
                        chunksOnLine.Add(currentChunk);
                        currentChunk = chunkPool.Get(Width);
                        currentChunk.FG = fg;
                        currentChunk.BG = bg;
                        currentChunk.Underlined = underlined;
                        currentChunk.HasChanged = pixelChanged;
                        currentChunk.Add(val);
                    }

                    lastDrawnPixels[x, y] = pixel;
                }

                if (currentChunk?.Length > 0)
                {
                    chunksOnLine.Add(currentChunk);
                }

                currentChunk = null;

                if (changeOnLine)
                {
                    var left = 0;
                    for (var i = 0; i < chunksOnLine.Count; i++)
                    {
                        var chunk = chunksOnLine[i];
                        if (chunk.HasChanged)
                        {
                            if (chunk.Underlined)
                            {
                                paintBuilder.Append(Ansi.Text.UnderlinedOn);
                            }

                            Ansi.Cursor.Move.ToLocation(left + 1, y + 1, paintBuilder);
                            Ansi.Color.Foreground.Rgb(chunk.FG, paintBuilder);
                            Ansi.Color.Background.Rgb(chunk.BG, paintBuilder);
                            paintBuilder.Append(chunk);
                            if (chunk.Underlined)
                            {
                                paintBuilder.Append(Ansi.Text.UnderlinedOff);
                            }
                        }

                        left += chunk.Length;
                    }
                }

                foreach (var chunk in chunksOnLine)
                    chunkPool.Return(chunk);

                chunksOnLine.Clear();
            }

            Ansi.Cursor.Move.ToLocation(Width - 1, Height - 1, paintBuilder);
            Console.Write(paintBuilder.Buffer, paintBuilder.Length);
        }
        catch (IOException)
        {
            Invalidate();
            PaintNew();
        }
        catch (ArgumentOutOfRangeException)
        {
            Invalidate();
            PaintNew();
        }
    }

    /// <summary>
    ///     Clears the cached paint state of each pixel so that
    ///     all pixels will forcefully be painted the next time Paint
    ///     is called
    /// </summary>
    public void Invalidate()
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            lastDrawnPixels[x, y] = default;
        }
    }

    /// <summary>
    ///     Gets a string representation of this image
    /// </summary>
    /// <returns>a string representation of this image</returns>
    public override string? ToString() => ToConsoleString().ToString();

    /// <summary>
    ///     Returns true if the given object is a ConsoleBitmap with
    ///     equivalent values as this bitmap, false otherwise
    /// </summary>
    /// <param name="obj">the object to compare</param>
    /// <returns>
    ///     true if the given object is a ConsoleBitmap with
    ///     equivalent values as this bitmap, false otherwise
    /// </returns>
    public override bool Equals(object? obj)
    {
        var other = obj as ConsoleBitmap;
        if (other == null) return false;

        if (Width != other.Width || Height != other.Height)
        {
            return false;
        }

        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var thisVal = GetPixel(x, y).Value;
                var otherVal = other.GetPixel(x, y).Value;
                if (thisVal != otherVal) return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Gets a hashcode for this bitmap
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode() => base.GetHashCode();
}

internal class Chunk
{
    public RGB BG;
    public readonly char[] buffer;
    public RGB FG;
    public bool HasChanged;
    public short Length;
    public bool Underlined;

    public Chunk(int maxWidth) { buffer = new char[maxWidth]; }

    public int BufferLength => buffer.Length;

    public void Clear()
    {
        Length = 0;
        FG = default;
        BG = default;
        Underlined = default;
        HasChanged = false;
    }

    public void Add(char c) => buffer[Length++] = c;
    public override string? ToString() => new(buffer, 0, Length);
}

internal class PaintBuffer
{
    public char[] Buffer = new char[120 * 80];
    public int Length;

    internal void Append(Chunk c)
    {
        EnsureBigEnough(Length + c.Length);

        var span = c.buffer.AsSpan();
        for (var i = 0; i < c.Length; i++)
            Buffer[Length++] = span[i];
    }

    public void Append(char c)
    {
        EnsureBigEnough(Length + 1);
        Buffer[Length++] = c;
    }

    public void Append(string chars)
    {
        EnsureBigEnough(Length + chars.Length);

        var span = chars.AsSpan();
        for (var i = 0; i < span.Length; i++)
            Buffer[Length++] = span[i];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureBigEnough(int newLen)
    {
        while (newLen > Buffer.Length)
        {
            var newBuffer = new char[Buffer.Length * 2];
            Array.Copy(Buffer, 0, newBuffer, 0, Buffer.Length);
            Buffer = newBuffer;
            newLen = Buffer.Length;
        }
    }

    public void Clear() { Length = 0; }
}

internal class ChunkPool
{
    private readonly Dictionary<int, List<Chunk>> pool = new();

    public Chunk Get(int w)
    {
        if (pool.TryGetValue(w, out var chunks) == false || chunks.None())
        {
            return new Chunk(w);
        }

        var ret = chunks[0];
        chunks.RemoveAt(0);
        return ret;
    }

    public void Return(Chunk obj)
    {
        if (pool.TryGetValue(obj.BufferLength, out var chunks) == false)
        {
            chunks = new List<Chunk>();
            pool.Add(obj.BufferLength, chunks);
        }

        obj.Clear();
        chunks.Add(obj);
    }
}