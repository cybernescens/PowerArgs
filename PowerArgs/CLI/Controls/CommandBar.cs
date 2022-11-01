namespace PowerArgs.Cli;

public class CommandBar : ConsolePanel
{
    public CommandBar()
    {
        Height = 1;
        Controls.SynchronizeForLifetime(Commands_Added, Commands_Removed, () => { }, this);
    }

    private void Commands_Added(ConsoleControl c) { Layout.StackHorizontally(1, Controls); }

    private void Commands_Removed(ConsoleControl c) { Layout.StackHorizontally(1, Controls); }
}