namespace PowerArgs.Cli;

/// <summary>
///     Information about an option to be presented on a dialog
/// </summary>
public class DialogOption
{
    /// <summary>
    ///     The display text for the option
    /// </summary>
    public ConsoleString DisplayText { get; }

    public DialogOption(object value) : this(value.ToString()!, value) { }

    public DialogOption(string id, object value, ConsoleString? display = null)
    {
        Id = id;
        Value = value;
        DisplayText = display ?? value.ToString().ToConsoleString();
    }

    /// <summary>
    ///     The id of this option's value
    /// </summary>
    public string Id { get; } = null!;

    /// <summary>
    ///     An object that this option represents
    /// </summary>
    public object Value { get; }

    /// <summary>
    ///     Compares the ids of each option
    /// </summary>
    /// <param name="obj">the other option</param>
    /// <returns>true if the ids match</returns>
    public override bool Equals(object? obj)
    {
        var b = obj as DialogOption;
        if (b == null) return false;

        return b.Id == Id;
    }

    /// <summary>
    ///     gets the hashcode of the id
    /// </summary>
    /// <returns>the hashcode of the id</returns>
    public override int GetHashCode() => Id.GetHashCode();
}

/// <summary>
///     The base dialog options for controlling the dialog behavior
/// </summary>
public abstract class DialogOptions 
{
    protected DialogOptions(ConsoleApp application) { Application = application; }

    public ConsoleApp Application { get; }

    /// <summary>
    ///     The max height of the dialog. If set to 0 the dialog
    ///     will take up most of the application layout root.
    /// </summary>
    public int MaxHeight { get; init; } = 8;

    /// <summary>
    ///     If true (which is the default), the escape key can be used to dismiss the dialog
    ///     which should be interpreted as a cancellation
    /// </summary>
    public bool AllowEscapeToCancel { get; init; } = true;

    /// <summary>
    ///     When provided, the dialog will automatically dismiss when this task completes.
    /// </summary>
    public Task? AutoDismissTask { get; init; } = null;

    public Action<Dialog> OnPosition { get; init; } = d => {
        d.FillHorizontally();
        d.CenterVertically();
    };

    internal abstract InteractiveDialogContentControl GetContent();
}

/// <summary>
///     A flavor of dialog options that you can use to show the user a set of options to choose from.
/// </summary>
public abstract class InteractiveDialogOptions : DialogOptions
{
    public InteractiveDialogOptions(ConsoleApp application) : base(application) { }

    /// <summary>
    ///     The message to display above the options
    /// </summary>
    public ConsoleString Message { get; init; } = ConsoleString.Empty;

    /// <summary>
    ///     The options
    /// </summary>
    public List<DialogOption> Options { get; init; } = new();
}

public class GridDialogOptions : InteractiveDialogOptions
{
    public GridDialogOptions(ConsoleApp application) : base(application) { }

    internal override GridDialogContent GetContent()
    {
        if (!Options.Any())
            throw new ArgumentException("You need to specify at least one button for grid mode");

        return new GridDialogContent(
            Message.IsUnstyled ? Message.ToYellow() : Message, 
            Options);
    }
}

public class ButtonListDialogOptions : InteractiveDialogOptions
{
    public ButtonListDialogOptions(ConsoleApp application) : base(application) { }

    internal override ButtonListDialogContent GetContent() => new ButtonListDialogContent(Message, Options);

    /// <summary>
    ///     A generic Yes button
    /// </summary>
    public static DialogOption Yes => new("yes", true, "Yes".ToConsoleString());

    /// <summary>
    ///     A generic No button
    /// </summary>
    public static DialogOption No => new("no", false, "No".ToConsoleString());

    /// <summary>
    ///     A generic OK button
    /// </summary>
    public static DialogOption OK => new("ok", true, "OK".ToConsoleString());
}

/// <summary>
///     A flavor of dialog options where the dialog should present a text box
///     for text input
/// </summary>
public class RichTextDialogOptions : InteractiveDialogOptions
{
    /// <summary>
    ///     The message to display above the text box
    /// </summary>
    public ConsoleString Message { get; init; } = ConsoleString.Empty;

    public string? DefaultValue { get; set; }

    internal override RichTextDialogContent GetContent()
    {
        var content = new RichTextDialogContent(
            Message,
            DefaultValue, 
            Application.LayoutRoot.Width / 2,
            Application.LayoutRoot.Height / 2);

        return content;
    }

    public RichTextDialogOptions(ConsoleApp application) : base(application) { }
}

public abstract class InteractiveDialogContentControl : ConsolePanel
{
    protected readonly Lifetime dialogLifetime = new();
    protected bool dismissed;

    protected InteractiveDialogContentControl() : base()
    {
        OnDisposed(
            () => {
                dialogLifetime.Dispose();
                Dismiss();
            });
    }

    protected InteractiveDialogContentControl(int w, int h) : base(w, h)
    {
        OnDisposed(
            () => {
                dialogLifetime.Dispose();
                Dismiss();
            });
    }

    protected virtual void OnDismiss() { }
     
    public void Dismiss()
    {
        if (!dismissed)
        {
            OnDismiss();
            dismissed = true;
            Application.LayoutRoot.Controls.Remove(this);
        }
    }

    public void OnSelect(object o)
    {
        SelectedItem = o;
        Dismiss();
    }
    
    public object? SelectedItem
    {
        get => Get<object>();
        protected set => Set(value);
    }
}

public class RichTextDialogContent : InteractiveDialogContentControl
{
    public RichTextDialogContent(ConsoleString message, string? defaultValue, int w, int h) : base(w, h)
    {
        Add(new Label { Text = message, X = 2, Y = 2 });
        var textbox = Add(new TextBox { Value = (defaultValue ?? string.Empty).ToConsoleString() }).CenterHorizontally();
        textbox.Y = 4;

        SynchronizeForLifetime(
            nameof(Bounds),
            () => { textbox.Width = Math.Max(0, Width - 4); },
            dialogLifetime);

        textbox.KeyInputReceived.SubscribeForLifetime(
            textbox,
            k => {
                if (k.Key == ConsoleKey.Enter)
                {
                    OnSelect(textbox.Value);
                }
            });

        textbox.AddedToVisualTree.SubscribeOnce(() => Application.InvokeNextCycle(() => textbox.TryFocus()));
    }
}

public class GridDialogContent : InteractiveDialogContentControl
{
    private readonly Grid grid;

    public GridDialogContent(ConsoleString message, IEnumerable<DialogOption> options)
    {
        grid = Add(
            new Grid(options) {
                MoreDataMessage = "More options below".ToYellow(),
                EndOfDataMessage = "End of menu"
            });

        grid.VisibleColumns.Remove(
            grid.VisibleColumns.Single(v => v.ColumnName.Equals(nameof(DialogOption.Id))));

        grid.VisibleColumns[0].WidthPercentage = 1;
        grid.VisibleColumns[0].ColumnDisplayName = message;

        grid.VisibleColumns[0].OverflowBehavior = new TruncateOverflowBehavior {
            ColumnWidth = 0
        };

        grid.SelectedItemActivated += () => { OnSelect(grid.SelectedItem!); };

        AddedToVisualTree.SubscribeOnce(() => Application.InvokeNextCycle(() => { TryFocus(); }));
    }
    
    protected override void OnDismiss()
    {
        Controls.Remove(grid);
    }
}

public class ButtonListDialogContent : InteractiveDialogContentControl
{
    private readonly StackPanel buttonPanel;
    private readonly ScrollablePanel messagePanel;

    public ButtonListDialogContent(ConsoleString message, IEnumerable<DialogOption> options)
    {
        messagePanel = Add(new ScrollablePanel()).Fill(padding: new Thickness(0, 0, 1, 3));
        messagePanel.ScrollableContent
            .Add(new Label { Mode = LabelRenderMode.MultiLineSmartWrap, Text = message })
            .FillHorizontally(padding: new Thickness(3, 3, 0, 0));

        buttonPanel = Add(new StackPanel { Margin = 1, Height = 1, Orientation = Orientation.Horizontal })
            .FillHorizontally(padding: new Thickness(1, 0, 0, 0))
            .DockToBottom(padding: 1);

        options.Select(o => buttonPanel.Add(new Button { Text = o.DisplayText, Tag = o.Value }))
            .ToList()
            .ForEach(b => b.Pressed.SubscribeOnce(() => { OnSelect(b.Tag!); }));

        buttonPanel.Controls.Last().AddedToVisualTree.SubscribeOnce(
            () => buttonPanel.Application.InvokeNextCycle(() => { buttonPanel.Controls.Last().TryFocus(); }));
    }

    protected override void OnDismiss()
    {
        Controls.Remove(buttonPanel);
        Controls.Remove(messagePanel);
    }
}

/// <summary>
///     A console control that shows a dialog as a layer above a ConsoleApp
/// </summary>
public class Dialog : ConsolePanel
{
    private readonly Button? closeButton;
    private readonly DialogOptions options;
    private int myFocusStackDepth;

    private Dialog(DialogOptions options)
    {
        this.options = options;
        this.Application = options.Application;

        this.content = Add(options.GetContent()).Fill(padding: new Thickness(0, 0, 1, 1));

        if (options.AllowEscapeToCancel)
        {
            closeButton =
                Add(
                    new Button {
                        Text = "Close (ESC)".ToConsoleString(), Background = DefaultColors.H1Color,
                        Foreground = ConsoleColor.Black
                    }).DockToRight(padding: 1);

            closeButton.Pressed.SubscribeForLifetime(this, Escape);
        }

        BeforeAddedToVisualTree.SubscribeForLifetime(this, OnBeforeAddedToVisualTree);
        AddedToVisualTree.SubscribeForLifetime(this, OnAddedToVisualTree);
        RemovedFromVisualTree.SubscribeForLifetime(this, OnRemovedFromVisualTree);
    }

    public bool WasEscapeUsedToClose { get; set; }

    /// <summary>
    ///     Shows the dialog on top of the current app
    /// </summary>
    /// <returns>a Task that resolves when this dialog is dismissed. This Task never rejects.</returns>
    private Task<object?>? dialogTask;

    private TaskCompletionSource<object?> openHandle;
    private readonly InteractiveDialogContentControl content;

    private Task<object?> Show()
    {
        object? GetOptionSelectedValue() =>
            (WasEscapeUsedToClose, options) switch {
                (false, InteractiveDialogOptions) => content.SelectedItem,
                _                                 => null
            };

        if (dialogTask is { IsCompleted: false })
            return dialogTask!;

        if (dialogTask is { IsCompleted: true } or null)
        {
            openHandle = new TaskCompletionSource<object?>(TaskCreationOptions.AttachedToParent);
            Application.LayoutRoot.Add(this);
            RemovedFromVisualTree.SubscribeForLifetime(
                this,
                () => {
                    openHandle.SetResult(GetOptionSelectedValue());
                });
        }

        dialogTask = openHandle.Task;
        return dialogTask;
    }

    private void OnBeforeAddedToVisualTree()
    {
        Application.FocusManager.Push();
        myFocusStackDepth = Application.FocusManager.StackDepth;
        Application.FocusManager.GlobalKeyHandlers.PushForLifetime(ConsoleKey.Escape, null, Escape, this);
    }

    private void OnAddedToVisualTree()
    {
        if (Parent != Application.LayoutRoot)
        {
            throw new InvalidOperationException("Dialogs must be added to the LayoutRoot of an application");
        }

        Height = options.MaxHeight > 0
            ? Math.Min(options.MaxHeight, Application.LayoutRoot.Height - 2)
            : Application.LayoutRoot.Height - 2;

        options.OnPosition(this);
        Application.FocusManager.TryMoveFocus();
        Application.FocusManager.SubscribeForLifetime(this, nameof(FocusManager.StackDepth),
            () => {
                if (IsBeingRemoved)
                    return;

                if (closeButton == null)
                    return;

                closeButton.Background =
                    Application.FocusManager.StackDepth != myFocusStackDepth
                        ? DefaultColors.DisabledColor
                        : DefaultColors.H1Color;
            });
    }

    private void OnRemovedFromVisualTree()
    {
        Application.FocusManager.Pop();
    }

    private void Escape()
    {
        if (options.AllowEscapeToCancel)
        {
            WasEscapeUsedToClose = true;
            Application.LayoutRoot.Controls.Remove(this);
        }
    }

    /// <summary>
    ///     Paints the dialog control
    /// </summary>
    /// <param name="context">the drawing surface</param>
    protected override void OnPaint(ConsoleBitmap context)
    {
        var pen = new ConsoleCharacter(
            ' ',
            null,
            myFocusStackDepth == Application.FocusManager.StackDepth
                ? DefaultColors.H1Color
                : DefaultColors.DisabledColor);

        context.DrawLine(pen, 0, 0, Width, 0);
        context.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        base.OnPaint(context);
    }

    /// <summary>
    ///     Shows a dialog with a message and a set of options for the user to choose from
    /// </summary>
    /// <param name="options">the options used to configure the dialog</param>
    /// <returns>a Task that resolves with the selected option or null if the dialog was cancelled. The Task never rejects.</returns>
    public static Task<DialogOption?> ShowMessage<T>(T options) where T : InteractiveDialogOptions => 
        new Dialog(options).Show().ContinueWith(t => t.Result as DialogOption);

    /// <summary>
    ///     Shows a dialog with a message and an ok button
    /// </summary>
    /// <param name="app"></param>
    /// <param name="message">the message to show</param>
    /// <returns>a Task that resolves when the dialog is dismissed. The Task never rejects.</returns>
    public static Task ShowMessage(ConsoleApp app, ConsoleString message)
    {
        var buttonOptions = new ButtonListDialogOptions(app) {
            Message = message,
            Options = new List<DialogOption> {
                ButtonListDialogOptions.OK
            }
        };

        return ShowMessage(buttonOptions);
    }

    /// <summary>
    ///     Shows a dialog with a message and an ok button
    /// </summary>
    /// <param name="message">the message to show</param>
    /// <returns>a Task that resolves when the dialog is dismissed. The Task never rejects.</returns>
    public static Task ShowMessage(ConsoleApp app, string message) => ShowMessage(app, message.ToConsoleString());

    /// <summary>
    ///     Shows a dialog with the given message and provides the user with a yes and no option
    /// </summary>
    /// <param name="message">the message to show</param>
    /// <returns>a Task that resolves if the yes option was clicked. it rejects if no was clicked or if the dialog was cancelled</returns>
    public static Task ShowYesConfirmation(ConsoleApp app, string message) => ShowYesConfirmation(app, message.ToConsoleString());

    /// <summary>
    ///     Shows a dialog with the given message and provides the user with a yes and no option
    /// </summary>
    /// <param name="message">the message to show</param>
    /// <returns>a Task that resolves if the yes option was clicked. it rejects if no was clicked or if the dialog was cancelled</returns>
    public static Task<bool> ShowYesConfirmation(ConsoleApp app, ConsoleString message) =>
        ShowMessage(
                new ButtonListDialogOptions(app) {
                    Message = message,
                    Options = new List<DialogOption> {
                        ButtonListDialogOptions.Yes,
                        ButtonListDialogOptions.No
                    }
                })
            .ContinueWith(t => t.Result != null && (bool)t.Result.Value);

    /// <summary>
    ///     Shows a message and lets the user pick from a set of options defined by an enum
    /// </summary>
    /// <param name="message">the message to show</param>
    /// <param name="enumType">the enum type</param>
    /// <returns>A Task that resolves with the selected value or null if the dialog was cancelled. The Task never rejects.</returns>
    public static Task<T?> ShowEnumOptions<T>(ConsoleApp app, ConsoleString message) where T : struct, Enum =>
        ShowMessage(
                new ButtonListDialogOptions(app) {
                    Message = message,
                    Options = Enum.GetValues<T>().OrderBy(x => x.ToString()).Select(x => new DialogOption(x)).ToList()
                })
            .ContinueWith(t => (T?)t.Result?.Value);
    
    /// <summary>
    ///     Shows a dialog that presents the user with a message and a text box
    /// </summary>
    /// <param name="options">the options used to configure the dialog</param>
    /// <returns>a Task that resolves with the value of the text box at the time of dismissal. This Task never rejects.</returns>
    public static Task<ConsoleString> ShowRichTextInput(RichTextDialogOptions options) =>
        ShowMessage(options).ContinueWith(t => t.Result?.Value as ConsoleString ?? ConsoleString.Empty);
}