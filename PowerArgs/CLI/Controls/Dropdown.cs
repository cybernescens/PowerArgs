namespace PowerArgs.Cli;

/// <summary>
///     A control that lets the user view and edit the current value among a set of options.
/// </summary>
public class Dropdown : ProtectedConsolePanel
{
    private bool isOpen;
    private readonly Label valueLabel;
    private int selectedIndex = 0;

    public Dropdown() : this(Array.Empty<DialogOption>()) { }

    /// <summary>
    ///     Creates a new Dropdown
    /// </summary>
    public Dropdown(IEnumerable<DialogOption> options)
    {
        Options.AddRange(options);
        Value = Options.FirstOrDefault();
        Height = 1;
        valueLabel = ProtectedPanel.Add(new Label());

        SynchronizeForLifetime(AnyProperty, SyncValueLabel, this);
        Focused.SubscribeForLifetime(this, SyncValueLabel);
        Unfocused.SubscribeForLifetime(this, SyncValueLabel);

        KeyInputReceived.SubscribeForLifetime(
            this,
            k => {
                if (k.Key == ConsoleKey.Enter || k.Key == ConsoleKey.DownArrow)
                {
                    Open();
                }
                else if (EnableWAndSKeysForUpDown && (k.Key == ConsoleKey.W || k.Key == ConsoleKey.S))
                {
                    Open();
                }
            });
    }

    public override bool CanFocus => true;
    public bool IsEmpty => Value == null;

    public List<DialogOption> Options { get; } = new();

    /// <summary>
    ///     The currently selected option
    /// </summary>
    public DialogOption? Value
    {
        get => Get<DialogOption>();
        set => Set(value);
    }

    public bool EnableWAndSKeysForUpDown { get; set; }

    private void SyncValueLabel()
    {
        if (IsEmpty)
            return;

        var text = Value!.DisplayText.StringValue;
        if (text.Length > Width - 3 && Width > 0)
        {
            text = text.Substring(0, Math.Max(0, Width - 3));
        }

        while (text.Length < Width - 2 && Width > 0)
        {
            text += " ";
        }

        if (HasFocus || isOpen)
        {
            text += isOpen ? "^ " : "v ";
        }
        else
        {
            text += "  ";
        }

        valueLabel.Text = HasFocus
            ? text.ToBlack(RGB.Cyan)
            : isOpen
                ? text.ToCyan(RGB.DarkGray)
                : text.ToWhite();
    }

    private async void Open()
    {
        isOpen = true;
        SyncValueLabel();
        TryUnfocus();

        try
        {
            Application.FocusManager.Push();
            var appropriatePopupWidth = 2 + 1 + Options.Select(o => o.DisplayText.Length).Max() + 1 + 1 + 2;
            var scrollPanel = new ScrollablePanel();
            scrollPanel.Width = appropriatePopupWidth - 4;
            scrollPanel.Height = Math.Min(8, Options.Count);

            var optionsStack = scrollPanel.ScrollableContent.Add(new StackPanel());
            optionsStack.Height = Options.Count;
            optionsStack.Width = scrollPanel.Width - 3;
            optionsStack.X = 1;

            var labels = optionsStack
                .AddRange(
                    Options.Select(option => new Label { CanFocus = true, Text = option.DisplayText, Tag = option }))
                .ToArray();

            scrollPanel.ScrollableContent.Width = optionsStack.Width + 2;
            scrollPanel.ScrollableContent.Height = optionsStack.Height;

            var popup = new BorderPanel(scrollPanel) { BorderColor = RGB.DarkCyan };
            popup.Width = scrollPanel.Width + 4;
            popup.Height = scrollPanel.Height + 2;
            popup.X = AbsoluteX;
            popup.Y = AbsoluteY + 1;
            Application.LayoutRoot.Add(popup);

            int FindSelectedIndex()
            {
                return Value != null ? Options.IndexOf(Value) : 0;
            }

            void SyncSelectedIndex(int index)
            {
                for (var i = 0; i < labels.Length; i++)
                {
                    labels[i].Text = Options[i].DisplayText;

                    // This value won't show so we need to invert its colors
                    if (labels[i]
                            .Text.Count(c => c.BackgroundColor == popup.Background && c.ForegroundColor == popup.Background) ==
                        labels[i].Text.Length)
                    {
                        labels[i].Text = new ConsoleString(
                            labels[i]
                                .Text.Select(
                                    c => new ConsoleCharacter(
                                        c.Value,
                                        c.ForegroundColor.GetCompliment(),
                                        popup.Background)));
                    }
                }

                var label = labels[index];
                label.TryFocus();
                label.Text = label.Text.ToBlack(RGB.Cyan);
                selectedIndex = index;
            }

            void Move(int step)
            {
                var tmp = selectedIndex + step;
                var index = tmp < 0
                    ? labels.Length - 1
                    : tmp >= labels.Length
                        ? 0
                        : tmp;

                SyncSelectedIndex(index);
            }

            SyncSelectedIndex(FindSelectedIndex());

            Application.FocusManager.GlobalKeyHandlers.PushForLifetime(
                ConsoleKey.Enter,
                null,
                () => {
                    Value = Options[selectedIndex];
                    popup.Dispose();
                },
                popup);

            Application.FocusManager.GlobalKeyHandlers.PushForLifetime(ConsoleKey.Escape, null, popup.Dispose, popup);

            if (EnableWAndSKeysForUpDown)
            {
                Application.FocusManager.GlobalKeyHandlers.PushForLifetime(ConsoleKey.W, null, () => Move(-1), popup);
                Application.FocusManager.GlobalKeyHandlers.PushForLifetime(ConsoleKey.S, null, () => Move(1), popup);
            }

            Application.FocusManager.GlobalKeyHandlers.PushForLifetime(ConsoleKey.UpArrow, null, () => Move(-1), popup);
            Application.FocusManager.GlobalKeyHandlers.PushForLifetime(ConsoleKey.DownArrow, null, () => Move(1), popup);
            Application.FocusManager.GlobalKeyHandlers.PushForLifetime(ConsoleKey.Tab, ConsoleModifiers.Shift, () => Move(-1), popup);
            Application.FocusManager.GlobalKeyHandlers.PushForLifetime(ConsoleKey.Tab, null, () => Move(1), popup);

            await popup.AsTask();
        }
        finally
        {
            isOpen = false;
            Application.FocusManager.Pop();
            TryFocus();
        }
    }
}