namespace PowerArgs;

/// <summary>
///     An interface that defined the contract for associating cleanup
///     code with a lifetime
/// </summary>
public interface ILifetimeManager
{
    /// <summary>
    ///     returns true if expired
    /// </summary>
    bool IsExpired { get; }

    /// <summary>
    ///     returns true if expiring
    /// </summary>
    bool IsExpiring { get; }

    /// <summary>
    ///     Registers the given cleanup code to run when the lifetime being
    ///     managed by this manager ends
    /// </summary>
    /// <param name="cleanupCode">the code to run</param>
    /// <returns>a Task that resolves after the cleanup code runs</returns>
    void OnDisposed(Action cleanupCode);

    /// <summary>
    ///     Registers the given disposable to dispose when the lifetime being
    ///     managed by this manager ends
    /// </summary>
    /// <param name="obj">the object to dispose</param>
    /// <returns>a Task that resolves after the object is disposed</returns>
    void OnDisposed(IDisposable obj);
}

public static class ILifetimeManagerEx
{
    /// <summary>
    ///     Delays until this lifetime is complete
    /// </summary>
    /// <returns>an async task</returns>
    public static async Task AwaitEndOfLifetime(this ILifetimeManager manager)
    {
        while (manager.IsExpired == false)
        {
            await Task.Yield();
        }
    }
}

/// <summary>
///     An implementation of ILifetimeManager
/// </summary>
public class LifetimeManager : ILifetimeManager
{
    internal readonly List<Action> cleanupActions = new();
    internal readonly List<IDisposable> cleanupDisposables = new();
    internal readonly List<(Action<object[]> Action, object[] Params)> cleanupParameterizedActions = new();

    /// <summary>
    ///     Creates the lifetime manager
    /// </summary>
    public LifetimeManager() { }

    /// <summary>
    ///     returns true if expired
    /// </summary>
    public bool IsExpired { get; internal set; }
    public bool IsExpiring { get; internal set; }

    /// <summary>
    ///     Registers the given disposable to dispose when the lifetime being
    ///     managed by this manager ends
    /// </summary>
    /// <param name="obj">the object to dispose</param>
    public void OnDisposed(IDisposable obj)
    {
        cleanupDisposables.Add(obj);
    }

    /// <summary>
    ///     Registers the given cleanup code to run when the lifetime being
    ///     managed by this manager ends
    /// </summary>
    /// <param name="cleanupCode">the code to run</param>
    public void OnDisposed(Action cleanupCode)
    {
        cleanupActions.Add(cleanupCode);
    }

    public void OnDisposed(Action<object[]> cleanupCode, object[] param)
    {
        cleanupParameterizedActions.Add((cleanupCode, param));
    }

    public void OnDisposed(Action<object> cleanupCode, object param)
    {
        OnDisposed(o => cleanupCode(o[0]), new[] { param });
    }
}