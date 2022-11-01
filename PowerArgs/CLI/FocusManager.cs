namespace PowerArgs.Cli;

/// <summary>
///     A class that manages the focus of a CLI application
/// </summary>
public class FocusManager : ObservableObject
{
    private string? currentFocusedControlId;

    /// <summary>
    ///     Initializes the focus manager
    /// </summary>
    public FocusManager()
    {
        Stack = new Stack<FocusContext>();
        Stack.Push(new FocusContext());
    }

    public Stack<FocusContext> Stack { get; }

    /// <summary>
    ///     Gets the number of layers on the stack
    /// </summary>
    public int StackDepth => Stack.Count;

    /// <summary>
    ///     Gets the keyboard manager that can be used to intercept keystrokes on the current layer
    /// </summary>
    public KeyboardInterceptionManager GlobalKeyHandlers => Stack.Peek().Interceptors;

    /// <summary>
    ///     Gets the currently focused control or null if there is no control with focus yet.
    /// </summary>
    public ConsoleControl? FocusedControl
    {
        get => Get<ConsoleControl>();
        private set {
            currentFocusedControlId = value switch {
                { IsBeingRemoved: false } => value.Id,
                null                      => null,
                _                         => currentFocusedControlId
            };

            Set(value);
        }
    }

    /// <summary>
    ///     Adds the current control to the current focus context
    /// </summary>
    /// <param name="c">The control to add</param>
    internal void Add(ConsoleControl c)
    {
        if (Stack.Peek().Controls.Contains(c))
            throw new InvalidOperationException("Item already being tracked");

        Stack.Peek().Controls.Add(c);

        if (c.Id == currentFocusedControlId)
            c.TryFocus();

        c.SubscribeForLifetime(c, nameof(c.CanFocus),
            () => {
                if (c.CanFocus == false && c.HasFocus)
                    TryMoveFocus();
            });
    }

    /// <summary>
    ///     Removes the control from all focus contexts
    /// </summary>
    /// <param name="c">The control to remove</param>
    internal void Remove(ConsoleControl c)
    {
        foreach (var context in Stack)
            context.Controls.Remove(c);
    }

    /// <summary>
    ///     Pushes a new focus context onto the stack.  This is useful, for example, when a dialog appears above all other
    ///     controls and you want to limit focus to the dialog to achieve a modal affect.  You must remember to call pop
    ///     when your context ends.
    /// </summary>
    public void Push()
    {
        Stack.Push(new FocusContext());
        FirePropertyChanged(nameof(StackDepth));
    }

    /// <summary>
    ///     Pops the current focus context.  This should be called if you've implemented a modal dialog like experience and
    ///     your dialog
    ///     has just closed.  Pop() will automatically restore focus on the previous context.
    /// </summary>
    public void Pop()
    {
        if (Stack.Count == 1)
            throw new InvalidOperationException("Cannot pop the last item off the focus stack");

        var _ = Stack.Pop();
        TryRestoreFocus();
        FirePropertyChanged(nameof(StackDepth));
    }

    /// <summary>
    ///     Tries to set focus on the given control.
    /// </summary>
    /// <param name="newFocusControl">the control to focus.  </param>
    /// <returns>True if the focus was set or if it was already set, false if the control cannot be focused</returns>
    public bool TrySetFocus(ConsoleControl newFocusControl)
    {
        var index = Stack.Peek().Controls.IndexOf(newFocusControl);
        if (index < 0)
            return false;

        if (newFocusControl.CanFocus == false)
            return false;

        if (newFocusControl == FocusedControl)
            return true;

        var oldFocusedControl = FocusedControl;
        if (oldFocusedControl != null)
            oldFocusedControl.HasFocus = false;

        newFocusControl.HasFocus = true;
        FocusedControl = newFocusControl;
        Stack.Peek().FocusIndex = index;
        oldFocusedControl?.FireFocused(false);
        FocusedControl?.FireFocused(true);
        return true;
    }

    /// <summary>
    ///     Tries to move the focus forward or backwards
    /// </summary>
    /// <param name="forward">If true then the manager will try to move forwards, otherwise backwards</param>
    /// <returns>True if the focus moved, false otherwise</returns>
    public bool TryMoveFocus(bool forward = true)
    {
        if (Stack.Peek().Controls.Count == 0)
            return false;

        var initialPosition = Stack.Peek().FocusIndex;
        var start = DateTime.Now;

        do
        {
            var wrapped = CycleFocusIndex(forward);
            var nextControl = Stack.Peek().Controls[Stack.Peek().FocusIndex];

            if (nextControl.CanFocus)
                return TrySetFocus(nextControl);

            if (wrapped && initialPosition < 0) break;
        } while (Stack.Peek().FocusIndex != initialPosition && DateTime.Now - start < TimeSpan.FromSeconds(.2));

        return false;
    }

    /// <summary>
    ///     Tries to restore the focus on the given context
    /// </summary>
    /// <returns>True if the focus changed, false otherwise</returns>
    public bool TryRestoreFocus()
    {
        if (!Stack.Peek().Controls.Any(c => c.CanFocus))
            return false;

        var initialPosition = Stack.Peek().FocusIndex;
        var skipOnce = true;

        do
        {
            var wrapped = false;
            if (skipOnce)
                skipOnce = false;
            else
                wrapped = CycleFocusIndex(true);

            var newFocusIndex = Math.Max(0, Math.Min(Stack.Peek().FocusIndex, Stack.Peek().Controls.Count - 1));
            Stack.Peek().FocusIndex = newFocusIndex;

            var nextControl = Stack.Peek().Controls[Stack.Peek().FocusIndex];
            if (nextControl.CanFocus)
                return TrySetFocus(nextControl);

            if (wrapped && initialPosition < 0) break;
        } while (Stack.Peek().FocusIndex != initialPosition);

        return false;
    }

    /// <summary>
    ///     Clears the focus, but preserves the focus index
    /// </summary>
    public void ClearFocus()
    {
        if (FocusedControl != null)
        {
            FocusedControl.HasFocus = false;
            FocusedControl.FireFocused(false);
            FocusedControl = null;
        }
    }

    private bool CycleFocusIndex(bool forward)
    {
        Stack.Peek().FocusIndex += forward ? 1 : -1;

        if (Stack.Peek().FocusIndex >= Stack.Peek().Controls.Count)
        {
            Stack.Peek().FocusIndex = 0;
            return true;
        }

        if (Stack.Peek().FocusIndex < 0)
        {
            Stack.Peek().FocusIndex = Stack.Peek().Controls.Count - 1;
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Data object used to capture the focus context on the stack
    /// </summary>
    public class FocusContext
    {
        /// <summary>
        ///     Creates a new focus context
        /// </summary>
        public FocusContext()
        {
            FocusIndex = -1;
        }

        public KeyboardInterceptionManager Interceptors { get; } = new();

        /// <summary>
        ///     The controls being managed by this context
        /// </summary>
        public List<ConsoleControl> Controls { get; } = new();

        /// <summary>
        ///     The current focus index within this context
        /// </summary>
        public int FocusIndex { get; internal set; }
    }
}