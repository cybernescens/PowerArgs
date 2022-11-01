namespace PowerArgs;

/// <summary>
///     A lifetime aware event
/// </summary>
public class Event
{
    private int paramsSubCount;

    private int paramsTail;
    private int subCount;
    private (Action, ILifetimeManager)?[] subscribers = Array.Empty<(Action, ILifetimeManager)?>();
    private (Action<object[]>, object[], ILifetimeManager)?[] subscribersWithParams = Array.Empty<(Action<object[]>, object[], ILifetimeManager)?>();

    private int tail;

    /// <summary>
    ///     returns true if there is at least one subscriber
    /// </summary>
    public bool HasSubscriptions => subCount > 0 || paramsSubCount > 0;

    /// <summary>
    ///     Fires the event. All subscribers will be notified
    /// </summary>
    public void Fire()
    {
        for (var i = 0; i < tail; i++)
            subscribers[i]?.Item1.Invoke();

        for (var i = 0; i < paramsTail; i++)
            subscribersWithParams[i]?.Item1.Invoke(subscribersWithParams[i]?.Item2!);
    }

    /// <summary>
    ///     Subscribes to this event such that the given handler will be called when the event fires
    /// </summary>
    /// <param name="handler">the action to run when the event fires</param>
    /// <returns>A subscription that can be disposed when you no loner want to be notified from this event</returns>
    public ILifetime SubscribeUnmanaged(Action handler)
    {
        EnsureRoomForMore();
        var myI = tail++;
        subCount++;
        subscribers[myI] = (handler, new Lifetime());

        ((Lifetime)subscribers[myI]!.Value.Item2).OnDisposed(
            indexes => {
                var idx = (int)indexes[0];
                subscribers[idx] = null;
                subCount--;
            }, 
            myI);

        return (Lifetime)subscribers[myI]!.Value.Item2;
    }

    public ILifetime SubscribeUnmanaged(Action<object[]> handler, params object[] param)
    {
        EnsureRoomForMoreWithParams();
        var myI = paramsTail++;
        paramsSubCount++;
        subscribersWithParams[myI] = (handler, param, new Lifetime());

        ((Lifetime)subscribersWithParams[myI]!.Value.Item3).OnDisposed(
            indexes => {
                subscribersWithParams[(int)indexes[0]] = null;
                paramsSubCount--;
            }, 
            myI);

        return (Lifetime)subscribersWithParams[myI]!.Value.Item3;
    }

    private void EnsureRoomForMore()
    {
        if (tail != subscribers.Length) return;

        var tmp = subscribers;
        subscribers = new (Action, ILifetimeManager)?[Math.Max(tmp.Length, 1) * 2];
        Array.Copy(tmp, subscribers, tmp.Length);
    }

    private void EnsureRoomForMoreWithParams()
    {
        if (paramsTail != subscribersWithParams.Length) return;

        var tmp = subscribersWithParams;
        subscribersWithParams = new (Action<object[]>, object[], ILifetimeManager)?[Math.Max(tmp.Length, 1) * 2];
        Array.Copy(tmp, subscribersWithParams, tmp.Length);
    }

    public ILifetime SynchronizeUnmanaged(Action handler)
    {
        handler();
        return SynchronizeUnmanaged(handler);
    }

    /// <summary>
    ///     Subscribes to this event such that the given handler will be called when the event fires. Notifications will stop
    ///     when the lifetime associated with the given lifetime manager is disposed.
    /// </summary>
    /// <param name="lifetimeManager">the lifetime manager that determines when to stop being notified</param>
    /// <param name="handler">the action to run when the event fires</param>
    public void SubscribeForLifetime(ILifetimeManager lifetimeManager, Action handler)
    {
        var lt = SubscribeUnmanaged(handler);
        lifetimeManager.OnDisposed(lt);
    }

    public void SubscribeForLifetime(ILifetimeManager lifetimeManager, Action<object[]> handler, params object[] param)
    {
        var lt = SubscribeUnmanaged(handler, param);
        lifetimeManager.OnDisposed(lt);
    }

    public void SynchronizeForLifetime(ILifetimeManager lifetimeManager, Action handler)
    {
        handler();
        SubscribeForLifetime(lifetimeManager, handler);
    }

    /// <summary>
    ///     Subscribes to the event for one notification and then immediately unsubscribes so your callback will only be called
    ///     at most once
    /// </summary>
    /// <param name="handler">The action to run when the event fires</param>
    public void SubscribeOnce(Action handler)
    {
        var lt = new Lifetime();

        void Wrap()
        {
            try
            {
                handler();
            }
            finally
            {
                lt.Dispose();
            }
        }

        SubscribeForLifetime(lt, Wrap);
    }

    public void SubscribeOnce(Action<object[]> handler, params object[] param)
    {
        var lt = new Lifetime();

        void Wrap(object[] args)
        {
            try
            {
                handler(args);
            }
            finally
            {
                lt.Dispose();
            }
        }

        SubscribeForLifetime(lt, Wrap, param);
    }

    /// <summary>
    ///     Creates a lifetime that will end the next time this
    ///     event fires
    /// </summary>
    /// <returns>a lifetime that will end the next time this event fires</returns>
    public Lifetime CreateNextFireLifetime()
    {
        var lifetime = new Lifetime();
        SubscribeOnce(lifetime.Dispose);
        return lifetime;
    }

    public Task CreateNextFireTask()
    {
        var tcs = new TaskCompletionSource<bool>();
        SubscribeOnce(_ => tcs.SetResult(true));
        return tcs.Task;
    }
}

public class Event<T>
{
    private int paramsSubCount;

    private int paramsTail;
    private int subCount;
    private (Action<T>, ILifetimeManager)[] subscribers = Array.Empty<(Action<T>, ILifetimeManager)>();
    private (Action<object[]>, object[], ILifetimeManager)[] subscribersWithParams = Array.Empty<(Action<object[]>, object[], ILifetimeManager)>();

    private int tail;

    /// <summary>
    ///     returns true if there is at least one subscriber
    /// </summary>
    public bool HasSubscriptions => subCount > 0 || paramsSubCount > 0;

    /// <summary>
    ///     Fires the event. All subscribers will be notified
    /// </summary>
    public void Fire(T arg, params object[] param)
    {
        for (var i = 0; i < tail; i++)
            subscribers[i].Item1.Invoke(arg);

        for (var i = 0; i < paramsTail; i++)
            subscribersWithParams[i].Item1.Invoke(new object[] { arg! }.Concat(param).ToArray());
    }

    /// <summary>
    ///     Subscribes to this event such that the given handler will be called when the event fires
    /// </summary>
    /// <param name="handler">the action to run when the event fires</param>
    /// <returns>A subscription that can be disposed when you no loner want to be notified from this event</returns>
    public ILifetime SubscribeUnmanaged(Action<T> handler)
    {
        EnsureRoomForMore();
        var myI = tail++;
        subCount++;
        subscribers[myI] = (handler, new Lifetime());
        
        ((Lifetime)subscribers[myI].Item2).OnDisposed(
            indexes => { 
                subscribers[(int)indexes[0]] = default;
                subCount--;
            }, 
            myI);

        return (Lifetime)subscribers[myI].Item2;
    }

    public ILifetime SubscribeUnmanaged(Action<object[]> handler, params object[] param)
    {
        EnsureRoomForMoreWithParams();
        var myI = paramsTail++;
        paramsSubCount++;
        subscribersWithParams[myI] = (handler, param, new Lifetime());

        ((Lifetime)subscribersWithParams[myI].Item3).OnDisposed(
            indexes => {
                subscribersWithParams[(int)indexes[0]] = default;
                paramsSubCount--;
            }, 
            myI);
    
        return (Lifetime)subscribersWithParams[myI].Item3;
    }

    private void EnsureRoomForMore()
    {
        if (tail != subscribers.Length)
            return;

        var tmp = subscribers;
        subscribers = new (Action<T>, ILifetimeManager)[Math.Max(tmp.Length, 1) * 2];
        Array.Copy(tmp, subscribers, tmp.Length);
    }

    private void EnsureRoomForMoreWithParams()
    {
        if (paramsTail != subscribersWithParams.Length)
            return;

        var tmp = subscribersWithParams;
        subscribersWithParams = new (Action<object[]>, object[], ILifetimeManager)[Math.Max(tmp.Length, 1) * 2];
        Array.Copy(tmp, subscribersWithParams, tmp.Length);
    }

    /// <summary>
    ///     Subscribes to this event such that the given handler will be called when the event fires. Notifications will stop
    ///     when the lifetime associated with the given lifetime manager is disposed.
    /// </summary>
    /// <param name="lifetimeManager">the lifetime manager that determines when to stop being notified</param>
    /// <param name="handler">the action to run when the event fires</param>
    public void SubscribeForLifetime(ILifetimeManager lifetimeManager, Action<T> handler)
    {
        var lt = SubscribeUnmanaged(handler);
        lifetimeManager.OnDisposed(lt);
    }

    public void SubscribeForLifetime(ILifetimeManager lifetimeManager, Action<object[]> handler, params object[] param)
    {
        var lt = SubscribeUnmanaged(handler, param);
        lifetimeManager.OnDisposed(lt);
    }

    /// <summary>
    ///     Subscribes to the event for one notification and then immediately unsubscribes so your callback will only be called
    ///     at most once
    /// </summary>
    /// <param name="handler">The action to run when the event fires</param>
    public void SubscribeOnce(Action<T> handler)
    {
        var lt = new Lifetime();

        void Wrap(T arg)
        {
            try
            {
                handler(arg);
            }
            finally
            {
                lt.Dispose();
            }
        }

        SubscribeForLifetime(lt, Wrap);
 ;   }

    public void SubscribeOnce(Action<object[]> handler, params object[] param)
    {
        var lt = new Lifetime();

        void Wrap(object[] paramPrime)
        {
            try
            {
                handler(paramPrime);
            }
            finally
            {
                lt.Dispose();
            }
        }

        SubscribeForLifetime(lt, Wrap, param);
    }

    /// <summary>
    ///     Creates a lifetime that will end the next time this
    ///     event fires
    /// </summary>
    /// <returns>a lifetime that will end the next time this event fires</returns>
    public Lifetime CreateNextFireLifetime()
    {
        var lifetime = new Lifetime();
        SubscribeOnce((object[] _) => lifetime.Dispose());
        return lifetime;
    }

    public Task<T> CreateNextFireTask()
    {
        var tcs = new TaskCompletionSource<T>();
        SubscribeOnce(arg => tcs.SetResult(arg));
        return tcs.Task;
    }
}