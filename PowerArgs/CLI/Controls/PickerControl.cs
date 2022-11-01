namespace PowerArgs.Cli;

public abstract class PickerControlOptions<T>
{
    public ConsoleString PromptMessage { get; set; } = "Choose from the following options".ToConsoleString();
    public List<T> Options { get; init; } = new();
    public Func<T, ConsoleString>? DisplayFormatter { get; set; }
    public Action<T>? SelectionChanged { get; set; }

    public T? InitiallySelectedOption { get; set; }

    internal bool HasDefaultSelection => InitiallySelectedOption != null;

    internal T DefaultSelection =>
        HasDefaultSelection
            ? InitiallySelectedOption!
            : throw new NotSupportedException($"Check {nameof(HasDefaultSelection)} first");
}

public class PickerControl<T> : ConsolePanel
{
    // hack because PowerArgs Pick function requires string Ids

    public PickerControl(PickerControlOptions<T> options)
    {
        Options = options;

        var innerLabel = Add(new Label()).Fill();

        // When the selected item changes make sure we update the label
        SubscribeForLifetime(this, nameof(SelectedItem),
            () => {
                if(SelectedItem != null)
                {
                    innerLabel.Text = FormatItem(SelectedItem);
                    Options.SelectionChanged?.Invoke(SelectedItem);
                }
            });

        innerLabel.CanFocus = true;

        innerLabel.KeyInputReceived.SubscribeForLifetime(
            this,
            key => {
                if (key.Key == ConsoleKey.Enter)
                {
                    Dialog
                        .ShowMessage(
                            new GridDialogOptions(Application) {
                                Message = Options.PromptMessage,
                                Options = Options.Options.Select(
                                    (o, i) => new DialogOption(i.ToString(), o!, FormatItem(o))).ToList()
                            })
                        .ContinueWith(t => { SelectedItem = (T?)t.Result?.Value; });
                }
            });

        if (options.HasDefaultSelection)
        {
            SelectedItem = options.DefaultSelection;
        }
    }

    public T? SelectedItem
    {
        get => Get<T>();
        set => Set(value);
    }

    public PickerControlOptions<T> Options { get; }

    private ConsoleString FormatItem(T item) =>
        Options.DisplayFormatter != null ? Options.DisplayFormatter(item) : (item?.ToString() ?? string.Empty).ToCyan();
}