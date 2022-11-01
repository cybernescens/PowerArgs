namespace PowerArgs.Cli.Physics;

/// <summary>
///     A model of time that lets you plug time functions and play them out on a thread. Each iteration of the time loop
///     processes queued actions,
///     executes time functions in order, and then increments the Now value.
/// </summary>
public class Time : EventLoop, IDelayProvider
{
    private readonly Dictionary<string, ITimeFunction> idMap = new();
    private readonly Random rand = new();

    private readonly List<ITimeFunction> timeFunctions = new();

    /// <summary>
    ///     Creates a new time model, optionally providing a starting time and increment
    /// </summary>
    /// <param name="increment">The amount of time to increment on each iteration, defaults to one 100 nanosecond tick</param>
    /// <param name="now">The starting time, defaults to zero</param>
    public Time(TimeSpan? increment = null, TimeSpan? now = null)
    {
        Increment = increment ?? TimeSpan.FromSeconds(.05f);
        Now = now ?? TimeSpan.Zero;
        
        StartOfCycle.SubscribeOnce(() => CurrentTime = this);
        EndOfCycle.SubscribeForLifetime(this, () => Now = Now.Add(Increment));

        OnDisposed(
            () => {
                foreach (var func in Functions)
                    func.Lifetime.TryDispose();
            });
    }

    /// <summary>
    ///     Gets the time model running on the current thread.
    /// </summary>
    [field: ThreadStatic]
    public static Time CurrentTime { get; private set; }

    /// <summary>
    ///     An event that fires when a time function is added to the model
    /// </summary>
    public Event<ITimeFunction> TimeFunctionAdded { get; } = new();

    /// <summary>
    ///     An event that fires when a time function is removed from the model
    /// </summary>
    public Event<ITimeFunction> TimeFunctionRemoved { get; } = new();

    public static bool CanInvoke =>
        SpaceTime.CurrentSpaceTime.IsDrainingOrDrained == false;

    /// <summary>
    ///     The current time
    /// </summary>
    public TimeSpan Now { get; private set; }

    /// <summary>
    ///     The amount to add to the value of 'Now' after each tick.
    /// </summary>
    public TimeSpan Increment { get; set; }

    /// <summary>
    ///     Enumerates all of the time functions that are a part of the model as of now.
    /// </summary>
    public IEnumerable<ITimeFunction> Functions => EnumerateFunctions();

    /// <summary>
    ///     Gets the time function with the given id. Ids must be populated at the time it was
    ///     added in order to be tracked.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public ITimeFunction this[string id] => idMap[id];

    public IReadOnlyList<ITimeFunction> TimeFunctions => timeFunctions.AsReadOnly();

    public Task DelayFuzzyAsync(float ms, double maxDeltaPercentage = .1)
    {
        var maxDelta = maxDeltaPercentage * ms;
        var min = ms - maxDelta;
        var max = ms + maxDelta;
        var delay = rand.Next((int)min, (int)max);
        delay = Math.Max((int)CurrentTime.Increment.TotalMilliseconds, delay);
        return DelayAsync(delay);
    }

    public Task DelayAsync(double ms) => DelayAsync(TimeSpan.FromMilliseconds(ms));

    public Task DelayAsync(TimeSpan timeout)
    {
        if (timeout == TimeSpan.Zero)
            throw new ArgumentException(
                "Delay for a time span of zero is not supported because there's a " +
                "good chance you're putting it in a loop on the time thread, " +
                "which will block the thread. You may want to call DelayOrYield (extension method).");

        var startTime = Now;
        return DelayAsync(() => Now - startTime >= timeout);
    }

    public Task DelayAsync(Event ev, TimeSpan? timeout = null, TimeSpan? evalFrequency = null)
    {
        var fired = false;

        ev.SubscribeOnce(() => { fired = true; });

        return DelayAsync(() => fired, timeout, evalFrequency);
    }

    public async Task DelayAsync(Func<bool> condition, TimeSpan? timeout = null, TimeSpan? evalFrequency = null)
    {
        if (await TryDelayAsync(condition, timeout, evalFrequency) == false)
        {
            throw new TimeoutException("Timed out awaiting delay condition");
        }
    }

    public async Task<bool> TryDelayAsync(
        Func<bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? evalFrequency = null)
    {
        var startTime = Now;
        var governor = evalFrequency.HasValue ? new RateGovernor(evalFrequency.Value, startTime) : null;
        while (IsRunning && IsDrainingOrDrained == false)
        {
            if (governor != null && governor.ShouldFire(Now) == false)
            {
                await Task.Yield();
            }
            else if (condition())
            {
                return true;
            }
            else if (timeout.HasValue && Now - startTime >= timeout.Value)
            {
                return false;
            }
            else
            {
                await Task.Yield();
            }
        }

        return true;
    }

    /// <summary>
    ///     Adds the given time function to the model. This method must be called from the time thread.
    /// </summary>
    /// <typeparam name="T">The type of the time function</typeparam>
    /// <param name="timeFunction">the time function to add</param>
    /// <returns>the time function that was passed in</returns>
    public T Add<T>(T timeFunction) where T : TimeFunction
    {
        AssertIsThisTimeThread();
        timeFunction.InternalState = new(this, Now);
        timeFunctions.Add(timeFunction);
        idMap.Add(timeFunction.Id, timeFunction);

        timeFunction.Lifetime.OnDisposed(
            () => {
                timeFunctions.Remove(timeFunction);
                if (idMap.ContainsKey(timeFunction.Id))
                    idMap.Remove(timeFunction.Id);

                TimeFunctionRemoved.Fire(timeFunction);
                timeFunction.InternalState = new(null, TimeSpan.Zero);
            });

        TimeFunctionAdded.Fire(timeFunction);
        timeFunction.Added.Fire();
        return timeFunction;
    }

    /// <summary>
    ///     Call this method to guard against code running on this model's time thread. It will throw an
    ///     InvalidOperationException
    ///     if the check fails.
    /// </summary>
    public void AssertIsThisTimeThread()
    {
        if (this != CurrentTime)
        {
            throw new InvalidOperationException("Code not running on time thread");
        }
    }

    /// <summary>
    ///     Asserts that there is a time model running on the current thread
    /// </summary>
    public static void AssertTimeThread()
    {
        if (CurrentTime == null)
        {
            throw new InvalidOperationException("Code not running on time thread");
        }
    }

    private IEnumerable<ITimeFunction> EnumerateFunctions() => timeFunctions.ToArray();
}