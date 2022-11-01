using System.Runtime.CompilerServices;

namespace PowerArgs.Cli;

public enum CompositionMode
{
    PaintOver = 0,
    BlendBackground = 1,
    BlendVisible = 2
}

/// <summary>
///     A console control that has nested control within its bounds
/// </summary>
public class ConsolePanel : Container
{
    private readonly List<ConsoleControl> sortedControls = new();

    public ConsolePanel() : this(1, 1) { }

    /// <summary>
    ///     Creates a new console panel
    /// </summary>
    public ConsolePanel(int w, int h) : base(w, h)
    {
        Controls.Added.SubscribeForLifetime(
            this,
            c => {
                c.Parent = this;
                sortedControls.Add(c);
                SortZ();
                c.SubscribeForLifetime(Controls.GetMembershipLifetime(c)!, nameof(c.ZIndex), SortZ);
            });

        Controls.AssignedToIndex.SubscribeForLifetime(
            this,
            (object[] _) =>
                throw new NotSupportedException("Index assignment is not supported in Controls collection"));

        Controls.Removed.SubscribeForLifetime(
            this,
            c => {
                sortedControls.Remove(c);
                c.Parent = null;
            });

        OnDisposed(
            () => {
                foreach (var child in Controls.ToArray())
                    child.TryDispose();
            });
    }

    public override bool CanFocus => false;

    /// <summary>
    ///     The nested controls
    /// </summary>
    public ObservableCollection<ConsoleControl> Controls { get; } = new();

    /// <summary>
    ///     All nested controls, including those that are recursively nested within inner console panels
    /// </summary>
    public override IEnumerable<ConsoleControl> Children => Controls;

    /// <summary>
    ///     Adds a control to the panel
    /// </summary>
    /// <typeparam name="T">the type of controls being added</typeparam>
    /// <param name="c">the control to add</param>
    /// <returns>the control that was added</returns>
    public T Add<T>(T c) where T : ConsoleControl
    {
        c.Parent = this;
        Controls.Add(c);
        return c;
    }

    /// <summary>
    ///     Adds a collection of controls to the panel
    /// </summary>
    /// <param name="controls">the controls to add</param>
    public IEnumerable<T> AddRange<T>(IEnumerable<T> controls) where T : ConsoleControl => controls.Select(Add);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareZ(ConsoleControl a, ConsoleControl b) =>
        a.ZIndex == b.ZIndex ? a.ParentIndex.CompareTo(b.ParentIndex) : a.ZIndex.CompareTo(b.ZIndex);

    private void SortZ()
    {
        for (var i = 0; i < sortedControls.Count; i++)
            sortedControls[i].ParentIndex = i;

        sortedControls.Sort(CompareZ);
        Application?.RequestPaint();
    }

    /// <summary>
    ///     Paints this control
    /// </summary>
    /// <param name="context">the drawing surface</param>
    protected override void OnPaint(ConsoleBitmap context)
    {
        foreach (var control in sortedControls.Where(
                     control => control.Width > 0 && control.Height > 0 && control.IsVisible))
            Compose(control);

        foreach (var filter in RenderFilters)
        {
            filter.Control = this;
            filter.Filter(Bitmap);
        }
    }
}

/// <summary>
///     A ConsolePanel that can prevent outside influences from
///     adding to its Controls collection. You must use the internal
///     Unlock method to add or remove controls.
/// </summary>
public class ProtectedConsolePanel : Container
{
    /// <summary>
    ///     Creates a new ConsolePanel
    /// </summary>
    public ProtectedConsolePanel()
    {
        ProtectedPanel.Parent = this;
        ProtectedPanel.Fill();
        SubscribeForLifetime(this, nameof(Background), () => ProtectedPanel.Background = Background);
        SubscribeForLifetime(this, nameof(Foreground), () => ProtectedPanel.Foreground = Foreground);
    }

    protected ConsolePanel ProtectedPanel { get; } = new();
    internal ConsolePanel ProtectedPanelInternal => ProtectedPanel;
    public override IEnumerable<ConsoleControl> Children => ProtectedPanel.Children;
    public override bool CanFocus => false;
    protected override void OnPaint(ConsoleBitmap context) { Compose(ProtectedPanel); }
}