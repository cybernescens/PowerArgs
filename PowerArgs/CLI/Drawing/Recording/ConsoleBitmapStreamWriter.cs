using PowerArgs.Cli.Physics;

namespace PowerArgs.Cli;

/// <summary>
///     An object that can write console bitmap video data to a stream
/// </summary>
public class ConsoleBitmapVideoWriter
{
    public const int DurationLineLength = 30;
    private char[] buffer = new char[250000];

    private int bufferIndex;

    private readonly Action<string> finishAction;
    private DateTime? firstFrameTime;

    private ConsoleBitmapRawFrame? lastFrame;
    private DateTime? pausedAt;
    private readonly ConsoleBitmapFrameSerializer serializer;
    private TimeSpan totalPauseTime = TimeSpan.Zero;

    /// <summary>
    ///     Creates a new writer given a stream
    /// </summary>
    /// <param name="finishAction">Action to run on completion</param>
    public ConsoleBitmapVideoWriter(Action<string> finishAction)
    {
        this.finishAction = finishAction;
        serializer = new ConsoleBitmapFrameSerializer();
        for (var i = 0; i < DurationLineLength - 1; i++)
            Append("-");

        Append("\n");
    }

    public RectF? Window { get; set; }

    private int GetEffectiveLeft => Window.HasValue ? (int)Window.Value.Left : 0;
    private int GetEffectiveTop => Window.HasValue ? (int)Window.Value.Top : 0;

    public bool IsFinished { get; private set; }

    /// <summary>
    ///     Gets the total number of frames written by the writer. This only counts unique frames
    ///     since calls to write frames with the same image as the previous frame are ignored.
    /// </summary>
    public int FramesWritten { get; private set; }
    private int GetEffectiveWidth(ConsoleBitmap bitmap) => Window.HasValue ? Convert.ToInt32(Window.Value.Width) : bitmap.Width;
    private int GetEffectiveHeight(ConsoleBitmap bitmap) => Window.HasValue ? Convert.ToInt32(Window.Value.Height) : bitmap.Height;

    public void Pause()
    {
        if (pausedAt.HasValue) return;

        pausedAt = DateTime.UtcNow;
    }

    public void Resume()
    {
        if (pausedAt.HasValue == false) return;

        var now = DateTime.UtcNow;
        totalPauseTime += now - pausedAt.Value;
        pausedAt = null;
    }

    /// <summary>
    ///     Writes the given bitmap image as a frame to the stream.  If this is the first image or more than half of the pixels
    ///     have
    ///     changed then a raw frame will be written.   Otherwise, a diff frame will be written.
    ///     This method uses the system's wall clock to determine the timestamp for this frame. The timestamp will be
    ///     relative to the wall clock time when the first frame was written.
    /// </summary>
    /// <param name="bitmap">the image to write</param>
    /// <param name="force">if true, writes the frame even if there are no changes</param>
    /// <param name="desiredFrameTime">
    ///     if provided, sstamp the frame with this time, otherwise stamp it with the wall clock
    ///     delta from the first frame time
    /// </param>
    /// <returns>the same bitmap that was passed in</returns>
    public ConsoleBitmap WriteFrame(ConsoleBitmap bitmap, bool force = false, TimeSpan? desiredFrameTime = null)
    {
        if (IsFinished) throw new NotSupportedException("Already finished");

        if (pausedAt.HasValue) return bitmap;

        var now = DateTime.UtcNow - totalPauseTime;
        firstFrameTime ??= now;
        var timestamp = desiredFrameTime ?? now - firstFrameTime.Value;

        var rawFrame = new ConsoleBitmapRawFrame(bitmap, timestamp, Window);
        
        if (lastFrame == null)
        {
            StreamHeader(bitmap);
            Append(serializer.SerializeFrame(rawFrame));
            FramesWritten++;
        }
        else
        {
            if (GetEffectiveWidth(bitmap) != lastFrame.Size.Width ||
                GetEffectiveHeight(bitmap) != lastFrame.Size.Height)
            {
                throw new InvalidOperationException("Video frame has changed size");
            }

            var diff = PrepareDiffFrame(lastFrame, bitmap, timestamp);

            var numPixels = GetEffectiveWidth(bitmap) * GetEffectiveHeight(bitmap);
            if (force || diff.Diffs.Count > numPixels / 2)
            {
                var frame = serializer.SerializeFrame(rawFrame);

                // checking to make sure we can deserialize what we just wrote so that if we can't
                // we still have time to debug. I'd love to get rid of this check for perf, but
                // there have been some cases where I wasn't able to read back what was written and if 
                // that edge case creeps up I want to catch it early.
                var deserialized = serializer.DeserializeFrame(frame);
                var frameBack = serializer.SerializeFrame((ConsoleBitmapRawFrame)deserialized);
                if (frameBack.Equals(frame) == false)
                {
                    throw new Exception("Serialization failure");
                }

                if (frame.EndsWith("\n", StringComparison.Ordinal) == false)
                {
                    throw new Exception();
                }

                Append(frame);
                FramesWritten++;
            }
            else if (diff.Diffs.Count > 0)
            {
                Append(serializer.SerializeFrame(diff));
                FramesWritten++;
            }
        }

        lastFrame = rawFrame;
        return bitmap;
    }

    private void Append(string s)
    {
        if (buffer.Length < bufferIndex + s.Length)
        {
            var temp = new char[Math.Max(buffer.Length * 2, bufferIndex + s.Length * 2)];
            Array.Copy(buffer, 0, temp, 0, bufferIndex);
            buffer = temp;
        }

        for (var i = 0; i < s.Length; i++)
            buffer[bufferIndex++] = s[i];
    }

    private void AppendLine(string s)
    {
        Append(s);
        Append("\n");
    }

    public bool TryFinish()
    {
        if (IsFinished) return false;

        Finish();
        return true;
    }

    /// <summary>
    ///     Writes the duration information in the beginning of the stream and then closes the inner stream
    ///     if CloseInnerStream is true
    /// </summary>
    public void Finish()
    {
        if (IsFinished)
        {
            throw new Exception("Already finished");
        }

        var toPrepend = CalculateDurationString();
        for (var i = 0; i < toPrepend.Length; i++)
            buffer[i] = toPrepend[i];

        var str = new string(buffer, 0, bufferIndex).Trim();
        buffer = Array.Empty<char>();
        finishAction(str);
        IsFinished = true;
    }

    private string CalculateDurationString()
    {
        var recordingTicks = lastFrame?.Timestamp.Ticks ?? 0;
        var ticksString = recordingTicks + "\n";

        while (ticksString.Length < DurationLineLength)
        {
            ticksString = "0" + ticksString;
        }

        return ticksString;
    }

    private void StreamHeader(ConsoleBitmap initialFrame)
    {
        AppendLine($"{GetEffectiveWidth(initialFrame)}x{GetEffectiveHeight(initialFrame)}");
    }

    private ConsoleBitmapDiffFrame PrepareDiffFrame(
        ConsoleBitmapRawFrame previous,
        ConsoleBitmap bitmap,
        TimeSpan timestamp)
    {
        var diff = new ConsoleBitmapDiffFrame(timestamp, bitmap.Bounds);
        var changes = 0;

        for (int y = 0; y < GetEffectiveHeight(bitmap); y++)
        {
            for (int x = 0; x < GetEffectiveWidth(bitmap); x++)
            {
                var pixel = bitmap.GetPixel(GetEffectiveLeft + x, GetEffectiveTop + y);
                var hasPreviousPixel = previous.Size.Width == GetEffectiveWidth(bitmap) &&
                    previous.Size.Height == GetEffectiveHeight(bitmap);

                var previousPixel = hasPreviousPixel ? previous.Pixels[x, y] : default;

                if (hasPreviousPixel == false || pixel.EqualsIn(previousPixel) == false)
                {
                    changes++;
                    diff.Diffs.Add(new ConsoleBitmapPixelDiff(x, y, pixel));
                }
            }
        }

        return diff;
    }
}