using PowerArgs.Cli;

namespace ConsoleAnimationStudio;

internal class Program
{
    private static void Main(string[] args)
    {
        var app = new ConsoleApp();
        app.Invoke(() => app.LayoutRoot.Add(new ConsoleBitmapAnimationStudio()).Fill());
        app.Run();
    }
}