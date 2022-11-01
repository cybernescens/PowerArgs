namespace PowerArgs.Cli;

public class FrameRateMeter
{
    private DateTime currentSecond;
    private int framesInCurrentSecond;
    private readonly DateTime start = DateTime.Now;

    public FrameRateMeter()
    {
        currentSecond = start;
        framesInCurrentSecond = 0;
    }

    public int TotalFrames { get; private set; }
    public int CurrentFps { get; private set; }

    public void Increment()
    {
        var now = DateTime.UtcNow;
        TotalFrames++;

        if (AreSameSecond(now, currentSecond))
        {
            framesInCurrentSecond++;
        }
        else
        {
            CurrentFps = framesInCurrentSecond;
            framesInCurrentSecond = 0;
            currentSecond = now;
        }
    }

    private bool AreSameSecond(DateTime a, DateTime b) =>
        a.Year == b.Year &&
        a.Month == b.Month &&
        a.Day == b.Day &&
        a.Hour == b.Hour &&
        a.Minute == b.Minute &&
        a.Second == b.Second;
}