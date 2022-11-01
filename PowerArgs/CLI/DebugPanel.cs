namespace PowerArgs.Cli;

internal class DebugPanel : LogTailControl
{
    public static readonly RGB ForegroundColor = RGB.Black;
    public static readonly RGB BackgroundColor = RGB.DarkYellow;

    public DebugPanel()
    {
        Foreground = ForegroundColor;
        Background = BackgroundColor;
        Ready.SubscribeOnce(
            () => {
                Application.ConsoleOutTextReady
                    .SubscribeForLifetime(this, Append);
            });
    }
}