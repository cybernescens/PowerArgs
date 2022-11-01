using PowerArgs.Cli;

namespace PowerArgs.CLI.Controls;

public class ArrowBasedListMenu<T> : ProtectedConsolePanel where T : class
{
    private readonly Func<T, ConsoleString?> formatter;

    public ArrowBasedListMenu(List<T> menuItems, Func<T, ConsoleString?> formatter = null)
    {
        MenuItems = menuItems;
        formatter = formatter ?? (item => ("" + item).ToConsoleString());
        this.formatter = formatter;

        var stack = ProtectedPanel.Add(new StackPanel { Orientation = Orientation.Vertical, Margin = 1 }).Fill();
        CanFocus = true;

        Focused.SubscribeForLifetime(this, Sync);
        Unfocused.SubscribeForLifetime(this, Sync);

        foreach (var menuItem in menuItems)
        {
            var label = stack.Add(new Label { Text = formatter(menuItem), Tag = menuItem }).FillHorizontally();
        }

        Sync();

        KeyInputReceived.SubscribeForLifetime(this, OnKeyPress);
    }

    public int SelectedIndex
    {
        get => Get<int>();
        set => Set(value);
    }

    public T SelectedItem => MenuItems.Count > 0 ? MenuItems[SelectedIndex] : null;

    public Event<T> ItemActivated { get; } = new();
    public List<T> MenuItems { get; }

    public ConsoleKey? AlternateUp { get; set; }
    public ConsoleKey? AlternateDown { get; set; }

    private void OnKeyPress(ConsoleKeyInfo obj)
    {
        if (obj.Key == ConsoleKey.UpArrow || (AlternateUp.HasValue && obj.Key == AlternateUp.Value))
        {
            if (SelectedIndex > 0)
            {
                SelectedIndex--;
                FirePropertyChanged(nameof(SelectedItem));
                Sync();
            }
        }
        else if (obj.Key == ConsoleKey.DownArrow || (AlternateDown.HasValue && obj.Key == AlternateDown.Value))
        {
            if (SelectedIndex < MenuItems.Count - 1)
            {
                SelectedIndex++;
                FirePropertyChanged(nameof(SelectedItem));
                Sync();
            }
        }
        else if (obj.Key == ConsoleKey.Enter)
        {
            ItemActivated.Fire(SelectedItem);
        }
    }

    private void Sync()
    {
        foreach (var label in ProtectedPanel.Descendents.WhereAs<Label>().Where(l => l.Tag is T))
            if (ReferenceEquals(label.Tag, SelectedItem))
            {
                label.Text = formatter(label.Tag as T).StringValue.ToConsoleString(
                    HasFocus ? RGB.Black : Foreground,
                    HasFocus ? RGB.Cyan : Background);
            }
            else
            {
                label.Text = formatter(label.Tag as T);
            }
    }
}