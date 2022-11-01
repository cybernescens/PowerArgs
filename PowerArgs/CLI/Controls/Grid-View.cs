namespace PowerArgs.Cli;

public partial class Grid : ConsoleControl
{
    private TextBox? _filterTextBox;
    private TimerActionDebouncer filterTextDebouncer;

    public TextBox? FilterTextBox
    {
        get => _filterTextBox;
        set => SetFilterTextBox(value);
    }

    public ConsoleString? MoreDataMessage { get; set; }
    public bool ShowEndIfComplete { get; set; } = true;

    protected override void OnPaint(ConsoleBitmap context) { PaintInternal(context); }

    private void PaintInternal(ConsoleBitmap context)
    {
        if (Height < 5)
        {
            context.DrawString("Grid can't render in a space this small", 0, 0);
            return;
        }

        if (VisibleColumns.Count == 0)
        {
            context.DrawString(NoVisibleColumnsMessage.ToConsoleString(DefaultColors.H1Color), 0, 0);
            return;
        }

        var headers = new List<ConsoleString>();
        var rows = new List<List<ConsoleString>>();
        var overflowBehaviors = new List<ColumnOverflowBehavior>();

        if (VisibleColumns.All(c => c.WidthPercentage == 0))
        {
            foreach (var col in VisibleColumns)
                col.WidthPercentage = 1.0 / VisibleColumns.Count;
        }

        foreach (var header in VisibleColumns)
        {
            headers.Add(header.ColumnDisplayName);
            var colWidth = (int)(header.WidthPercentage * Width);

            if (header.OverflowBehavior is SmartWrapOverflowBehavior smart)
            {
                smart.MaxWidthBeforeWrapping = colWidth;
            }
            else if (header.OverflowBehavior is TruncateOverflowBehavior truncate)
            {
                truncate.ColumnWidth =
                    truncate.ColumnWidth == 0
                        ? colWidth
                        : truncate.ColumnWidth;
            }

            overflowBehaviors.Add(header.OverflowBehavior);
        }

        var viewIndex = visibleRowOffset;
        foreach (var item in DataView.Items)
        {
            var row = new List<ConsoleString?>();
            var columnIndex = 0;
            foreach (var col in VisibleColumns)
            {
                var value = PropertyResolver(item, col.ColumnName.ToString());
                var displayValue = value == null
                    ? "<null>".ToConsoleString()
                    : value is ConsoleString
                        ? (ConsoleString)value
                        : value.ToString().ToConsoleString();

                if (viewIndex == SelectedIndex && CanFocus)
                {
                    if (SelectionMode == GridSelectionMode.Row ||
                        (SelectionMode == GridSelectionMode.Cell && columnIndex == selectedColumnIndex))
                    {
                        displayValue = new ConsoleString(
                            displayValue.ToString(),
                            Background,
                            HasFocus ? DefaultColors.FocusColor : DefaultColors.SelectedUnfocusedColor);
                    }
                }

                row.Add(displayValue);
                columnIndex++;
            }

            viewIndex++;
            rows.Add(row);
        }

        var builder = new ConsoleTableBuilder();
        var table = builder.FormatAsTable(headers, rows, RowPrefix.ToString(), overflowBehaviors, Gutter);

        if (FilterText != null)
        {
            table = table.Highlight(
                FilterText,
                DefaultColors.HighlightContrastColor,
                DefaultColors.HighlightColor,
                StringComparison.InvariantCultureIgnoreCase);
        }

        if (DataView.IsViewComplete == false)
        {
            table += "Loading more rows...".ToConsoleString(DefaultColors.H1Color);
        }
        else if (DataView.IsViewEndOfData && DataView.Items.Count == 0)
        {
            table += NoDataMessage.ToConsoleString(DefaultColors.H1Color);
        }
        else if (DataView.IsViewEndOfData)
        {
            if (ShowEndIfComplete)
            {
                table += EndOfDataMessage.ToConsoleString(DefaultColors.H1Color);
            }
        }
        else
        {
            table += MoreDataMessage;
        }

        context.DrawString(table, 0, 0);

        if (FilteringEnabled) { }
    }

    private void OnKeyInputReceived(ConsoleKeyInfo info)
    {
        if (info.Key == ConsoleKey.UpArrow)
        {
            Up();
        }
        else if (info.Key == ConsoleKey.DownArrow)
        {
            Down();
        }
        else if (info.Key == ConsoleKey.LeftArrow)
        {
            Left();
        }
        else if (info.Key == ConsoleKey.RightArrow)
        {
            Right();
        }
        else if (info.Key == ConsoleKey.PageDown)
        {
            PageDown();
        }
        else if (info.Key == ConsoleKey.PageUp)
        {
            PageUp();
        }
        else if (info.Key == ConsoleKey.Home)
        {
            Home();
        }
        else if (info.Key == ConsoleKey.End)
        {
            End();
        }
        else if (info.Key == ConsoleKey.Enter)
        {
            Activate();
        }
        else if (FilteringEnabled && RichTextCommandLineReader.IsWriteable(info) && FilterTextBox != null)
        {
            FilterTextBox.Value = info.KeyChar.ToString().ToConsoleString();
            Application.FocusManager.TrySetFocus(FilterTextBox);
        }
    }

    private void InitGridView()
    {
        MoreDataMessage = "more data below".ToConsoleString(DefaultColors.H1Color);
        KeyInputReceived.SubscribeForLifetime(this, OnKeyInputReceived);

        filterTextDebouncer = new TimerActionDebouncer(
            TimeSpan.FromSeconds(0),
            () => {
                if (Application != null && FilterTextBox != null)
                {
                    Application.InvokeNextCycle(() => { FilterText = FilterTextBox.Value.ToString(); });
                }
            });

        // don't accept focus unless I have at least one item in the data view
        Focused.SubscribeForLifetime(
            this,
            () => {
                if (DataView.Items.Count == 0)
                {
                    Application.FocusManager.TryMoveFocus();
                }
            });
    }

    private void SetFilterTextBox(TextBox? value)
    {
        if (_filterTextBox != null)
        {
            throw new ArgumentException("Grid is already bound to a text box");
        }

        _filterTextBox = value;
        _filterTextBox.SubscribeForLifetime(value, nameof(TextBox.Value), FilterTextValueChanged);
        _filterTextBox.KeyInputReceived.SubscribeForLifetime(value, FilterTextKeyPressed);
        FilteringEnabled = true;
    }

    private void FilterTextKeyPressed(ConsoleKeyInfo keyInfo)
    {
        if (keyInfo.Key == ConsoleKey.Enter)
        {
            Activate();
        }
        else if (keyInfo.Key == ConsoleKey.DownArrow)
        {
            TryFocus();
        }
        else if (keyInfo.Key == ConsoleKey.PageDown)
        {
            TryFocus();
        }
        else if (keyInfo.Key == ConsoleKey.PageUp)
        {
            TryFocus();
        }
    }

    private void FilterTextValueChanged() { filterTextDebouncer.Trigger(); }
}