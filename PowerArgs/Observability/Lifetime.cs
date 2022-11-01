using System.Runtime.CompilerServices;

namespace PowerArgs;

public interface ILifetime : ILifetimeManager, IDisposable
{
    bool TryDispose();
}

public static class ILifetimeEx
{
    public static Lifetime CreateChildLifetime(this ILifetime lt)
    {
        var ret = new Lifetime();
        lt.OnDisposed(
            () => {
                if (ret.IsExpired == false)
                    ret.Dispose();
            });

        return ret;
    }
}

/// <summary>
///     An object that has a beginning and and end  that can be used to define the lifespan of event and observable
///     subscriptions.
/// </summary>
public class Lifetime : Disposable, ILifetime
{
    private static readonly Lifetime forever = CreateForeverLifetime();
    private LifetimeManager? manager = new();

    /// <summary>
    ///     Creates a new lifetime
    /// </summary>
    public Lifetime() { }

    public LifetimeManager Manager
    {
        get {
            if (IsExpired)
                throw new InvalidOperationException("Lifetime is expired");

            return manager!;
        }
    }

    /// <summary>
    ///     The forever lifetime manager that will never end. Any subscriptions you intend to keep forever should use this
    ///     lifetime so it's easy to spot leaks.
    /// </summary>
    public static LifetimeManager Forever => forever.Manager;

    /// <summary>
    ///     If true then this lifetime has already ended
    /// </summary>
    public bool IsExpired => manager == null;

    /// <summary>
    ///     returns true if the lifetime's Dispose() method is currently running, false otherwise
    /// </summary>
    public bool IsExpiring { get; private set; }

    /// <summary>
    ///     Registers an action to run when this lifetime ends
    /// </summary>
    /// <param name="cleanupCode">code to run when this lifetime ends</param>
    /// <returns>a promis that will resolve after the cleanup code has run</returns>
    public void OnDisposed(Action cleanupCode)
    {
        if (IsExpired)
            return;

        manager!.OnDisposed(cleanupCode);
    }

    /// <summary>
    ///     Registers a disposable to be disposed when this lifetime ends
    /// </summary>
    /// <param name="cleanupCode">an object to dispose when this lifetime ends</param>
    public void OnDisposed(IDisposable cleanupCode)
    {
        if (IsExpired)
            return;

        manager!.OnDisposed(cleanupCode);
    }

    public bool TryDispose()
    {
        if (IsExpired || IsExpiring)
            return false;

        Dispose();
        return true;
    }

    private static Lifetime CreateForeverLifetime()
    {
        var ret = new Lifetime();
        ret.OnDisposed(() => throw new Exception("Forever lifetime expired"));
        return ret;
    }

    protected override void AfterDispose() { Manager.IsExpired = true; }

    /// <summary>
    ///     Delays until this lifetime is complete
    /// </summary>
    /// <returns>an async task</returns>
    public Task AsTask()
    {
        var tcs = new TaskCompletionSource<bool>();
        OnDisposed(SetResultTrue, tcs);
        return tcs.Task;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetResultTrue(object[] tcs) { ((TaskCompletionSource<bool>)tcs[0]).SetResult(true); }

    public void OnDisposed(Action<object[]> cleanupCode, params object[] param)
    {
        if (IsExpired)
            return;

        manager!.OnDisposed(cleanupCode, param);
    }

    /// <summary>
    ///     Creates a new lifetime that will end when any of the given
    ///     lifetimes ends
    /// </summary>
    /// <param name="others">the lifetimes to use to generate this new lifetime</param>
    /// <returns>
    ///     a new lifetime that will end when any of the given
    ///     lifetimes ends
    /// </returns>
    public static Lifetime EarliestOf(params ILifetimeManager[] others) =>
        EarliestOf((IEnumerable<ILifetimeManager>)others);

    /// <summary>
    ///     Creates a new lifetime that will end when all of the given lifetimes end
    /// </summary>
    /// <param name="others">the lifetimes to use to generate this new lifetime</param>
    /// <returns>a new lifetime that will end when all of the given lifetimes end</returns>
    public static Lifetime WhenAll(params ILifetimeManager[] others) => 
        new WhenAllTracker(others);

    /// <summary>
    ///     Creates a new lifetime that will end when any of the given
    ///     lifetimes ends
    /// </summary>
    /// <param name="others">the lifetimes to use to generate this new lifetime</param>
    /// <returns>
    ///     a new lifetime that will end when any of the given
    ///     lifetimes ends
    /// </returns>
    public static Lifetime EarliestOf(IEnumerable<ILifetimeManager> others) => new EarliestOfTracker(others.ToArray());

    /// <summary>
    ///     Runs all the cleanup actions that have been registered
    /// </summary>
    protected override void DisposeManagedResources()
    {
        if (IsExpired)
            return;

        IsExpiring = true;
        manager!.IsExpiring = true;

        try
        {
            foreach (var item in manager!.cleanupActions.ToArray())
                item();

            foreach (var item in manager!.cleanupDisposables.ToArray())
                item.Dispose();

            foreach (var item in manager!.cleanupParameterizedActions.ToArray())
                item.Action(item.Params);

            manager = null;
        }
        finally
        {
            IsExpiring = false;
        }
    }

    private class EarliestOfTracker : Lifetime
    {
        public EarliestOfTracker(IReadOnlyCollection<ILifetimeManager> lts)
        {
            if (lts.Count == 0)
            {
                Dispose();
                return;
            }

            foreach (var lt in lts)
                lt.OnDisposed(() => TryDispose());
        }
    }

    private class WhenAllTracker : Lifetime
    {
        private int remaining;

        public WhenAllTracker(IReadOnlyCollection<ILifetimeManager> lts)
        {
            if (lts.Count == 0)
            {
                Dispose();
                return;
            }

            remaining = lts.Count;
            foreach (var lt in lts)
                lt.OnDisposed(Count);
        }

        private void Count()
        {
            if (Interlocked.Decrement(ref remaining) == 0)
            {
                Dispose();
            }
        }
    }
}