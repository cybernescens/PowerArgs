using System.Runtime.CompilerServices;

namespace PowerArgs.Cli;

/// <summary>
///     A class representing a console application that uses a message pump to synchronize work on a UI thread
/// </summary>
public class ConsoleApp : EventLoop, IObservableObject
{
    private readonly IConsoleProvider console;
    private readonly TextWriter consoleWriter;

    private readonly FrameRateMeter cycleRateMeter;

    private readonly ConsoleCharacter defaultPen = new(' ', null, DefaultColors.BackgroundColor);
    private readonly TextBroacaster interceptor;

    /// <summary>
    ///     If set to true then the app will automatically update its layout to fill the entire window.  If false the app
    ///     will not react to resizing, which means it may clip or wrap in unexpected ways when the window is resized.
    ///     If you use the constructor that takes no parameters then this is set to true and assumes you want to take the
    ///     whole window and respond to window size changes.  If you use the constructor that takes in coordinates and boudnds
    ///     then it is set to false and it is assumed that you only want the app to live within those bounds
    /// </summary>
    private readonly bool isFullScreen;

    private readonly ObservableObject observable;
    private readonly FrameRateMeter paintRateMeter = new();
    private readonly List<TaskCompletionSource<bool>> paintRequests = new();
    private readonly Queue<KeyRequest> sendKeys = new();
    private readonly AsyncLocal<ConsoleApp> consoleApp = new();

    private DebugPanel? debugPanel;

    private int lastConsoleWidth, lastConsoleHeight;
    private ConsoleKey lastKey;
    private DateTime lastKeyPressTime = DateTime.MinValue;

    private bool draining;
    public int nextId;

    private bool paintRequested;

    private List<IDisposable> timerHandles = new();

    /// <summary>
    ///     Creates a new console app given a set of boundaries
    /// </summary>
    /// <param name="w">The width of the app</param>
    /// <param name="h">The height of the app</param>
    /// <param name="name"></param>
    public ConsoleApp(int w, int h, string? name = null)
    {
        console = ConsoleProvider.Current;
        consoleWriter = Console.Out;
        interceptor = new TextBroacaster();
        LoopStarted.SubscribeOnce(() => Console.SetOut(interceptor));
        lastConsoleWidth = console.BufferWidth;
        lastConsoleHeight = console.WindowHeight;
        observable = new ObservableObject(this);

        cycleRateMeter = new FrameRateMeter();

        EndOfCycle.SubscribeForLifetime(this, Cycle);
        SetFocusOnStart = true;
        LayoutRoot = new ConsolePanel(w, h);
        FocusManager = new FocusManager();
        isFullScreen = false;

        FocusManager.SubscribeForLifetime(this, nameof(FocusManager.FocusedControl), () => RequestPaintAsync());

        LayoutRoot.Controls.BeforeAdded
            .SubscribeForLifetime(this, c => { c.Application = this; c.BeforeAddedToVisualTreeInternal(); });
        
        LayoutRoot.Controls.Added
            .SubscribeForLifetime(this, ControlAddedToVisualTree);

        LayoutRoot.Controls.BeforeRemoved
            .SubscribeForLifetime(this, c => { c.BeforeRemovedFromVisualTreeInternal(); });
        
        LayoutRoot.Controls.Removed
            .SubscribeForLifetime(this, ControlRemovedFromVisualTree);

        LoopStarted.SubscribeOnce(
            () => {
                Current = this;
                LayoutRoot.Application = this;
            });

        WindowResized.SubscribeForLifetime(this, HandleDebouncedResize);
        EndOfCycle.SubscribeForLifetime(this, DrainPaints);
    }

    /// <summary>
    ///     Creates a full screen console app that will automatically adjust its layout if the window size changes
    /// </summary>
    public ConsoleApp(Action? init = null) : this(
        ConsoleProvider.Current.BufferWidth,
        ConsoleProvider.Current.WindowHeight - 1)
    {
        isFullScreen = true;
        if (init != null)
        {
            Invoke(init);
        }
    }

    /// <summary>
    ///     True by default. When true, discards key presses that come in too fast
    ///     likely because the user is holding the key down. You can set the
    ///     MinTimeBetweenKeyPresses property to suit your needs.
    /// </summary>
    public bool KeyThrottlingEnabled { get; set; } = true;

    /// <summary>
    ///     When key throttling is enabled this lets you set the minimum time that must
    ///     elapse before we forward a key press to the app, provided it is the same key
    ///     that was most recently clicked.
    /// </summary>
    public TimeSpan MinTimeBetweenKeyPresses { get; set; } = TimeSpan.FromMilliseconds(35);

    /// <summary>
    ///     If true, paint requests will be honored. Defaults to true.
    /// </summary>
    public bool PaintEnabled { get; set; } = true;

    /// <summary>
    ///     If true, clears the console when the app exits. Defaults to true.
    /// </summary>
    public bool ClearOnExit { get; set; } = true;
    public Event OnKeyInputThrottled { get; } = new();

    /// <summary>
    ///     An event that fires when the console window has been resized by the user
    /// </summary>
    public Event WindowResized { get; } = new();

    /// <summary>
    ///     Gets the total number of event loop cycles that have run
    /// </summary>
    public int TotalCycles => cycleRateMeter.TotalFrames;

    /// <summary>
    ///     Gets the current frame rate for the app
    /// </summary>
    public int CyclesPerSecond => cycleRateMeter.CurrentFps;

    /// <summary>
    ///     Gets a reference to the current app running on this thread.  This will only be populated by the thread
    ///     that is running the message pump (i.e. it will never be your main thread).
    /// </summary>
    public ConsoleApp? Current
    {
        get => consoleApp.Value;
        set {
            if (value != null)
            {
                consoleApp.Value = value;
            }
        }
    }

    /// <summary>
    ///     Gets the current paint rate for the app
    /// </summary>
    public int PaintRequestsProcessedPerSecond => paintRateMeter.CurrentFps;

    /// <summary>
    ///     Gets the total number of times a paint actually happened
    /// </summary>
    public int TotalPaints => paintRateMeter.TotalFrames;

    /// <summary>
    ///     The writer used to record the contents of the screen while the app
    ///     is running. If not set then recording does not take place
    /// </summary>
    public ConsoleBitmapVideoWriter? Recorder { get; set; }

    /// <summary>
    ///     An event that fires when the application is about to stop, before the console is wiped
    /// </summary>
    public Event Stopping { get; } = new();

    /// <summary>
    ///     An event that fires after the message pump is completely stopped and the console is wiped
    /// </summary>
    public Event Stopped { get; } = new();

    /// <summary>
    ///     An event that fires when a control is added to the visual tree
    /// </summary>
    public Event<ConsoleControl> ControlAdded { get; } = new();

    /// <summary>
    ///     An event that fires when a control is removed from the visual tree
    /// </summary>
    public Event<ConsoleControl> ControlRemoved { get; } = new();

    /// <summary>
    ///     Gets the bitmap that will be painted to the console
    /// </summary>
    public ConsoleBitmap Bitmap => LayoutRoot.Bitmap;

    /// <summary>
    ///     Gets the root panel that contains the controls being used by the app
    /// </summary>
    public ConsolePanel LayoutRoot { get; }

    /// <summary>
    ///     Gets the focus manager used to manage input focus
    /// </summary>
    public FocusManager FocusManager { get; }

    /// <summary>
    ///     Gets or set whether or not to give focus to a control when the app starts.  The default is true.
    /// </summary>
    public bool SetFocusOnStart { get; set; }

    /// <summary>
    ///     An event that fires just after painting the app
    /// </summary>
    public Event AfterPaint { get; } = new();

    /// <summary>
    ///     True by default, enables ALT+SHIFT+D to show debug panel. Standard output
    ///     is redirected by the App Thread. The debug panel will show whatever is going out
    ///     via Console.Write().
    /// </summary>
    public bool DebugEnabled { get; set; } = true;

    /// <summary>
    ///     An event that fires when Console.Write() has been called.
    /// </summary>
    public Event<ConsoleString> ConsoleOutTextReady => interceptor.TextReady;

    public bool SuppressEqualChanges
    {
        get => observable.SuppressEqualChanges;
        set => observable.SuppressEqualChanges = value;
    }

    public IDisposable SubscribeUnmanaged(string propertyName, Action handler) =>
        observable.SubscribeUnmanaged(propertyName, handler);

    public void SubscribeForLifetime(ILifetimeManager lifetimeManager, string propertyName, Action handler) =>
        observable.SubscribeForLifetime(lifetimeManager, propertyName, handler);

    public IDisposable SynchronizeUnmanaged(string propertyName, Action handler) =>
        observable.SynchronizeUnmanaged(propertyName, handler);

    public void SynchronizeForLifetime(string propertyName, Action handler, ILifetimeManager? lifetimeManager) =>
        observable.SynchronizeForLifetime(propertyName, handler, lifetimeManager);

    public object? GetPrevious(string propertyName) => ((IObservableObject)observable).GetPrevious(propertyName);
    public Lifetime GetPropertyValueLifetime(string propertyName) => observable.GetPropertyValueLifetime(propertyName);

    public T? Get<T>([CallerMemberName] string name = "") => observable.Get<T>(name);
    public void Set<T>(T? value, [CallerMemberName] string name = "") => observable.Set(value, name);

    /// <summary>
    ///     Writes the string to the debug output which can be seen if DebugEnabled is true and
    ///     the user presses SHIFT+ALT+D.
    /// </summary>
    /// <param name="s">the string to write</param>
    public static void Debug(string? s) =>
        (s ?? "<null>").ToConsoleString(DebugPanel.ForegroundColor, DebugPanel.BackgroundColor).Write();

    /// <summary>
    ///     Writes the string plus a newline to the debug output which can be seen if DebugEnabled is true and
    ///     the user presses SHIFT+ALT+D.
    /// </summary>
    /// <param name="s">the string to write</param>
    public static void DebugLine(string? s) => Debug((s ?? "<null>") + "\n");

    /// <summary>
    ///     Writes the object as a ToString() to the debug output which can be seen if DebugEnabled is true and
    ///     the user presses SHIFT+ALT+D.
    /// </summary>
    /// <param name="o">the object to stringify</param>
    public static void Debug(object? o) => Debug(o?.ToString());

    /// <summary>
    ///     Writes the object as a ToString() plus a newline to the debug output which can be seen if DebugEnabled is true and
    ///     the user presses SHIFT+ALT+D.
    /// </summary>
    /// <param name="o">the object to stringify</param>
    public static void DebugLine(object? o) => DebugLine(o?.ToString());

    private void DrainPaints()
    {
        if (paintRequests.Count > 0)
        {
            PaintInternal();

            var paintRequestsCopy = paintRequests.ToArray();
            paintRequests.Clear();

            for (var i = 0; i < paintRequestsCopy.Length; i++)
                paintRequestsCopy[i].SetResult(true);

            paintRateMeter.Increment();
            paintRequested = false;
        }
        else if (paintRequested)
        {
            PaintInternal();
            paintRequested = false;
        }
    }

    /// <summary>
    ///     Adds the given control to a ConsoleApp, fills the space, and blocks until the app terminates
    /// </summary>
    /// <param name="control">the control to show</param>
    public static void Show(ConsoleControl control)
    {
        var app = new ConsoleApp();
        app.LayoutRoot.Add(control).Fill();
        app.Start().Wait();
    }

    /// <summary>
    ///     Starts a new ConsoleApp and waits for it to finish
    /// </summary>
    /// <param name="init">the function that initializes the app</param>
    public static void Show(Action<ConsoleApp> init)
    {
        var app = new ConsoleApp();
        app.InvokeNextCycle(() => init(app));
        app.Start().Wait();
    }

    /// <summary>
    ///     Starts the app, asynchronously.
    /// </summary>
    /// <returns>A task that will complete when the app exits</returns>
    public override Task Start()
    {
        Current = this;

        if (SetFocusOnStart)
            InvokeNextCycle(() => { FocusManager.TryMoveFocus(); });
        
        return base.Start().ContinueWith(_ => ExitInternal());
    }

    public override void Run()
    {
        Current = this;

        if (SetFocusOnStart)
            InvokeNextCycle(() => { FocusManager.TryMoveFocus(); });

        try
        {
            base.Run();
        }
        finally
        {
            ExitInternal();
        }
    }

    private void HandleDebouncedResize()
    {
        if (Bitmap.Console.BufferWidth < 1 || Bitmap.Console.WindowHeight - 1 < 1)
            return;

        if (isFullScreen)
        {
            LayoutRoot.Width = Bitmap.Console.BufferWidth;
            LayoutRoot.Height = Bitmap.Console.WindowHeight - 1;
        }

        RequestPaint();
    }

    /// <summary>
    ///     Queues up a request to paint the app.  The system will dedupe multiple paint requests when there are multiple in
    ///     the pump's work queue
    ///     <returns>a Task that resolves after the paint happens</returns>
    /// </summary>
    public Task RequestPaintAsync()
    {
        /* wrong async context */
        if (Current == null)
            return Task.CompletedTask;

        if (IsDrainingOrDrained) 
            return Task.CompletedTask;

        //AssertAppThread(this);
        var d = new TaskCompletionSource<bool>();
        paintRequests.Add(d);
        return d.Task;
    }

    public void RequestPaint()
    {
        paintRequested = true;
    }

    private void ControlAddedToVisualTree(ConsoleControl c)
    {
        c.ColliderHashCode = nextId++;
        c.Application = this;
        c.OnDisposed(
            () => {
                if (c.Application != this || c.Parent == null || c.Parent.Application != this)
                {
                    return;
                }

                if (c.Parent is ConsolePanel parent)
                {
                    parent.Controls.Remove(c);
                }
                else
                {
                    throw new NotSupportedException(
                        $"You cannot manually dispose child controls of parent type {c.Parent.GetType().Name}");
                }
            });

        if (c is ConsolePanel panel)
        {
            panel.Controls.SynchronizeForLifetime(ControlAddedToVisualTree, ControlRemovedFromVisualTree, () => { }, c);
        }
        else if (c is ProtectedConsolePanel protectedPanel)
        {
            ControlAddedToVisualTree(protectedPanel.ProtectedPanelInternal);
            protectedPanel.OnDisposed(() => ControlRemovedFromVisualTree(protectedPanel.ProtectedPanelInternal));
        }

        FocusManager.Add(c);
        c.AddedToVisualTreeInternal();

        ControlAdded.Fire(c);
    }

    private void ControlRemovedFromVisualTree(ConsoleControl c)
    {
        c.IsBeingRemoved = true;

        if (ControlRemovedFromVisualTreeRecursive(c))
        {
            FocusManager.TryRestoreFocus();
        }
    }

    private bool ControlRemovedFromVisualTreeRecursive(ConsoleControl? c)
    {
        var focusChanged = false;

        if (c is ConsolePanel panel)
        {
            foreach (var child in panel.Controls)
            {
                child.IsBeingRemoved = true;
                focusChanged = ControlRemovedFromVisualTreeRecursive(child) || focusChanged;
            }
        }

        if (FocusManager.FocusedControl == c)
        {
            FocusManager.ClearFocus();
            focusChanged = true;
        }

        FocusManager.Remove(c);

        c.RemovedFromVisualTreeInternal();
        c.Application = null;
        ControlRemoved.Fire(c);
        if (c.IsExpired == false && c.IsExpiring == false)
            c.Dispose();

        return focusChanged;
    }

    /// <summary>
    ///     Handles key input for the application
    /// </summary>
    /// <param name="info">The key that was pressed</param>
    protected virtual void HandleKeyInput(ConsoleKeyInfo info)
    {
        if (DebugEnabled &&
            info.Key == ConsoleKey.D &&
            info.Modifiers.HasFlag(ConsoleModifiers.Alt) &&
            info.Modifiers.HasFlag(ConsoleModifiers.Shift))
        {
            if (debugPanel == null)
            {
                debugPanel = LayoutRoot.Add(new DebugPanel { Height = 15 }).FillHorizontally().DockToBottom();
            }
            else
            {
                debugPanel.Dispose();
                debugPanel = null;
            }

            return;
        }

        if (FocusManager.GlobalKeyHandlers.TryIntercept(info))
        {
            // great, it was handled
        }
        else if (info.Key == ConsoleKey.Tab)
        {
            FocusManager.TryMoveFocus(info.Modifiers.HasFlag(ConsoleModifiers.Shift) == false);
        }
        else if (info.Key == ConsoleKey.Escape)
        {
            Stop();
            return;
        }
        else if (FocusManager.FocusedControl is { IsExpired: false })
        {
            FocusManager.FocusedControl.HandleKeyInput(info);
        }

        RequestPaint();
    }

    private void ExitInternal()
    {
        Stopping.Fire();
        Recorder?.WriteFrame(Bitmap, true);
        Recorder?.Finish();

        if (ClearOnExit)
        {
            ConsoleProvider.Current.Clear();
        }

        Bitmap.Console.ForegroundColor = ConsoleString.DefaultForegroundColor;
        Bitmap.Console.BackgroundColor = ConsoleString.DefaultBackgroundColor;
        Current = null;
        LayoutRoot.Dispose();
        Stopped.Fire();
        Dispose();
        Console.SetOut(consoleWriter);
    }

    private void PaintInternal()
    {
        Bitmap.Fill(defaultPen);
        LayoutRoot.Paint();

        Recorder?.WriteFrame(Bitmap);
        if (PaintEnabled)
        {
            Console.SetOut(consoleWriter);
            Bitmap.Paint();
            Console.SetOut(interceptor);
        }

        AfterPaint.Fire();
    }

    private void Cycle()
    {
        cycleRateMeter.Increment();
        // todo - if evaluation showed up on a profile. Consider checking this at most twice per second.
        if (lastConsoleWidth != console.BufferWidth || lastConsoleHeight != console.WindowHeight)
        {
            DebounceResize();
            WindowResized.Fire();
        }

        if (console.KeyAvailable)
        {
            var info = console.ReadKey(true);

            var effectiveMinTimeBetweenKeyPresses = MinTimeBetweenKeyPresses;
            if (KeyThrottlingEnabled &&
                info.Key == lastKey &&
                DateTime.UtcNow - lastKeyPressTime < effectiveMinTimeBetweenKeyPresses)
            {
                // the user is holding the key down and throttling is enabled
                OnKeyInputThrottled.Fire();
            }
            else
            {
                lastKeyPressTime = DateTime.UtcNow;
                lastKey = info.Key;
                InvokeNextCycle(() => HandleKeyInput(info));
            }
        }

        if (sendKeys.Count > 0)
        {
            var request = sendKeys.Dequeue();
            InvokeNextCycle(
                () => {
                    HandleKeyInput(request.Info);
                    request.TaskSource.SetResult(true);
                });
        }
    }

    /// <summary>
    ///     Simulates a key press
    /// </summary>
    /// <param name="key">the key press info</param>
    public Task SendKey(ConsoleKeyInfo key)
    {
        var tcs = new TaskCompletionSource<bool>();
        Invoke(() => { sendKeys.Enqueue(new KeyRequest { Info = key, TaskSource = tcs }); });
        return tcs.Task;
    }

    /// <summary>
    ///     Schedules the given action for periodic processing by the message pump
    /// </summary>
    /// <param name="a">The action to schedule for periodic processing</param>
    /// <param name="interval">the execution interval for the action</param>
    /// <returns>A handle that can be passed to ClearInterval if you want to cancel the work</returns>
    public SetIntervalHandle SetInterval(Action a, TimeSpan interval)
    {
        var handle = new SetIntervalHandle(interval);
        Invoke(
            async () => {
                while (IsRunning && IsDrainingOrDrained == false && handle.IsExpired == false)
                {
                    await Task.Delay(handle.Interval);
                    a();
                }
            });

        return handle;
    }

    /// <summary>
    ///     Updates a previously scheduled interval
    /// </summary>
    /// <param name="handle">the handle that was returned by a previous call to setInterval</param>
    /// <param name="newInterval">the new interval</param>
    public void ChangeInterval(SetIntervalHandle handle, TimeSpan newInterval) { handle.Interval = newInterval; }

    /// <summary>
    ///     Schedules the given action for a one time execution after the given period elapses
    /// </summary>
    /// <param name="a">The action to schedule</param>
    /// <param name="period">the period of time to wait before executing the action</param>
    /// <returns></returns>
    public IDisposable SetTimeout(Action a, TimeSpan period)
    {
        var lt = new Lifetime();

        Invoke(
            () => {
                return IsRunning && IsDrainingOrDrained == false && lt.IsExpired == false
                    ? Task.Delay(period).ContinueWith(_ => a())
                    : Task.Run(a);
            });

        return lt;
    }

    private void DebounceResize()
    {
        console.Clear();
        var done = false;
        var debouncer = new TimerActionDebouncer(TimeSpan.FromSeconds(.25), () => { done = true; });

        debouncer.Trigger();
        while (done == false)
        {
            if (console.BufferWidth != lastConsoleWidth || console.WindowHeight != lastConsoleHeight)
            {
                lastConsoleWidth = console.BufferWidth;
                lastConsoleHeight = console.WindowHeight;
                debouncer.Trigger();
            }
        }
    }

    private class KeyRequest
    {
        public ConsoleKeyInfo Info { get; init; }
        public TaskCompletionSource<bool> TaskSource { get; init; } = null!;
    }
}

public class SetIntervalHandle : Lifetime
{
    public SetIntervalHandle(TimeSpan interval) { Interval = interval; }

    public TimeSpan Interval { get; internal set; }
}