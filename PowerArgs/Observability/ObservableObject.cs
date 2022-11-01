using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace PowerArgs;

public interface IObservableObject
{
    bool SuppressEqualChanges { get; set; }
    IDisposable SubscribeUnmanaged(string propertyName, Action handler);
    void SubscribeForLifetime(ILifetimeManager lifetimeManager, string propertyName, Action handler);
    IDisposable SynchronizeUnmanaged(string propertyName, Action handler);
    void SynchronizeForLifetime(string propertyName, Action handler, ILifetimeManager? lifetimeManager);
    object? GetPrevious(string propertyName);
    T? Get<T>(string name);
    void Set<T>(T? value, string name);
    Lifetime GetPropertyValueLifetime(string propertyName);
}

/// <summary>
///     A class that makes it easy to define an object with observable properties
/// </summary>
public class ObservableObject : Lifetime, IObservableObject
{
    /// <summary>
    ///     Subscribe or synchronize using this key to receive notifications when any property changes
    /// </summary>
    public const string AnyProperty = "*";

    private readonly Dictionary<string, object?> previousValues;
    private readonly Dictionary<string, Event> subscribers;
    private readonly Dictionary<string, object?> values;

    /// <summary>
    ///     Creates a new bag and optionally sets the notifier object.
    /// </summary>
    public ObservableObject(IObservableObject? proxy = null)
    {
        SuppressEqualChanges = true;
        subscribers = new Dictionary<string, Event>();
        values = new Dictionary<string, object?>();
        previousValues = new Dictionary<string, object?>();
        DeepObservableRoot = proxy;
    }

    /// <summary>
    ///     DeepObservableRoot
    /// </summary>
    public IObservableObject? DeepObservableRoot { get; }

    public string? CurrentlyChangingPropertyName { get; private set; }
    public object? CurrentlyChangingPropertyValue { get; private set; }

    /// <summary>
    ///     Set to true if you want to suppress notification events for properties that get set to their existing values.
    /// </summary>
    public bool SuppressEqualChanges { get; set; }

    /// <summary>
    ///     This should be called by a property getter to get the value
    /// </summary>
    /// <typeparam name="T">The type of property to get</typeparam>
    /// <param name="name">The name of the property to get</param>
    /// <returns>The property's current value</returns>
    public T? Get<T>([CallerMemberName] string name = "")
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        if (!values.TryGetValue(name, out var ret))
            return default;

        if (ret is T typedret)
            return typedret;

        if (ret is null)
            return default;

        return (T)Convert.ChangeType(ret!, typeof(T));
    }

    object? IObservableObject.GetPrevious(string name) => GetPrevious<object>(name);

    /// <summary>
    ///     This should be called by a property getter to set the value.
    /// </summary>
    /// <typeparam name="T">The type of property to set</typeparam>
    /// <param name="value">The value to set</param>
    /// <param name="name">The name of the property to set</param>
    public void Set<T>(T? value, [CallerMemberName] string name = "")
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        var current = Get<object>(name);
        var isEqualChange = EqualsSafe(current, value);

        if (values.ContainsKey(name))
        {
            if (SuppressEqualChanges == false || isEqualChange == false)
                previousValues[name] = current;

            values[name] = value;
        }
        else
        {
            values.Add(name, value);
        }

        if (SuppressEqualChanges == false || isEqualChange == false)
        {
            CurrentlyChangingPropertyName = name;
            CurrentlyChangingPropertyValue = value;
            FirePropertyChanged(name);
        }
    }

    /// <summary>
    ///     Subscribes to be notified when the given property changes.
    /// </summary>
    /// <param name="propertyName">
    ///     The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be
    ///     notified of any property change.
    /// </param>
    /// <param name="handler">The action to call for notifications</param>
    /// <returns>A subscription that will receive notifications until it is disposed</returns>
    public IDisposable SubscribeUnmanaged(string propertyName, Action handler)
    {
        if (string.IsNullOrEmpty(propertyName))
            throw new ArgumentNullException(nameof(propertyName));

        if (!subscribers.TryGetValue(propertyName, out var evForProperty))
        {
            evForProperty = new Event();
            subscribers.Add(propertyName, evForProperty);
        }

        return evForProperty.SubscribeUnmanaged(handler);
    }

    /// <summary>
    ///     Subscribes to be notified when the given property changes.  The subscription expires when
    ///     the given lifetime manager's lifetime ends.
    /// </summary>
    /// <param name="lifetimeManager">the lifetime manager that determines when the subscription ends</param>
    /// <param name="propertyName">
    ///     The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be
    ///     notified of any property change.
    /// </param>
    /// <param name="handler">The action to call for notifications</param>
    public void SubscribeForLifetime(ILifetimeManager lifetimeManager, string propertyName, Action handler)
    {
        if (string.IsNullOrEmpty(propertyName))
            throw new ArgumentNullException(nameof(propertyName));

        var sub = SubscribeUnmanaged(propertyName, handler);
        lifetimeManager?.OnDisposed(sub);
    }

    /// <summary>
    ///     Subscribes to be notified when the given property changes and also fires an initial notification immediately.
    /// </summary>
    /// <param name="propertyName">
    ///     The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be
    ///     notified of any property change.
    /// </param>
    /// <param name="handler">The action to call for notifications</param>
    /// <returns>A subscription that will receive notifications until it is disposed</returns>
    public IDisposable SynchronizeUnmanaged(string propertyName, Action handler)
    {
        if (string.IsNullOrEmpty(propertyName))
            throw new ArgumentNullException(nameof(propertyName));

        handler();
        return SubscribeUnmanaged(propertyName, handler);
    }

    /// <summary>
    ///     Subscribes to be notified when the given property changes and also fires an initial notification.  The subscription
    ///     expires when
    ///     the given lifetime manager's lifetime ends.
    /// </summary>
    /// <param name="propertyName">
    ///     The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be
    ///     notified of any property change.
    /// </param>
    /// <param name="handler">The action to call for notifications</param>
    /// <param name="lifetimeManager">the lifetime manager that determines when the subscription ends</param>
    public void SynchronizeForLifetime(string propertyName, Action handler, ILifetimeManager? lifetimeManager)
    {
        if (string.IsNullOrEmpty(propertyName))
            throw new ArgumentNullException(nameof(propertyName));

        var sub = SynchronizeUnmanaged(propertyName, handler);
        lifetimeManager?.OnDisposed(sub);
    }

    public Lifetime GetPropertyValueLifetime(string propertyName)
    {
        var ret = new Lifetime();
        IDisposable? sub = null;
        sub = SubscribeUnmanaged(
            propertyName,
            () => {
                sub?.Dispose();
                ret.Dispose();
            });

        return ret;
    }

    public IReadOnlyDictionary<string, object?> ToDictionary() => new ReadOnlyDictionary<string, object?>(values);

    /// <summary>
    ///     returns true if this object has a property with the given key
    /// </summary>
    /// <param name="key">the property name</param>
    /// <returns>true if this object has a property with the given key</returns>
    public bool ContainsKey(string key) => values.ContainsKey(key!);

    /// <summary>
    ///     returns true if this object has a property with the given key and val was populated
    /// </summary>
    /// <typeparam name="T">the type of property to get</typeparam>
    /// <param name="key">the name of the property</param>
    /// <param name="val">the output value</param>
    /// <returns>true if this object has a property with the given key and val was populated</returns>
    public bool TryGetValue<T>(string key, out T? val)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        if (values.TryGetValue(key, out var oVal))
        {
            val = oVal != null ? (T)oVal : default;
            return true;
        }

        val = default;
        return false;
    }

    /// <summary>
    ///     Gets the previous value of the given property name
    /// </summary>
    /// <typeparam name="T">the type of property to get</typeparam>
    /// <param name="name">the name of the property</param>
    /// <returns>the previous value or default(T) if there was none</returns>
    public T? GetPrevious<T>([CallerMemberName] string name = "") =>
        previousValues.TryGetValue(name, out var ret)
            ? (ret != null ? (T)ret : default)
            : default;

    public void Set<T>(ref T? current, T? value, [CallerMemberName] string name = "")
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        var isEqualChange = EqualsSafe(current, value);

        if (SuppressEqualChanges == false || isEqualChange == false)
            previousValues[name!] = current;

        current = value;

        if (SuppressEqualChanges && isEqualChange)
            return;

        CurrentlyChangingPropertyName = name;
        CurrentlyChangingPropertyValue = value;
        FirePropertyChanged(name);
    }

    protected void SetHardIf<T>(ref T? current, T? value, Func<bool> condition, [CallerMemberName] string name = "")
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        if (condition() == false) 
            return;

        current = value;
        CurrentlyChangingPropertyName = name;
        CurrentlyChangingPropertyValue = value;
        FirePropertyChanged(name);
    }

    /// <summary>
    ///     Subscribes to be notified once when the given property changes.
    /// </summary>
    /// <param name="propertyName">
    ///     The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be
    ///     notified of any property change.
    /// </param>
    /// <param name="handler">The action to call for notifications</param>
    public void SubscribeOnce(string propertyName, Action handler)
    {
        IDisposable sub = null!;
        var wrappedAction = () => {
            handler();
            sub?.Dispose();
        };

        sub = SubscribeUnmanaged(propertyName, wrappedAction);
    }

    /// <summary>
    ///     Subscribes to be notified once when the given property changes.
    /// </summary>
    /// <param name="propertyName">
    ///     The name of the property to subscribe to or ObservableObject.AnyProperty if you want to be
    ///     notified of any property change.
    /// </param>
    /// <param name="toCleanup">The disposable to cleanup the next time the property changes</param>
    public void SubscribeOnce(string propertyName, IDisposable toCleanup) =>
        SubscribeOnce(propertyName, toCleanup.Dispose);

    /// <summary>
    ///     Fires the PropertyChanged event with the given property name.
    /// </summary>
    /// <param name="propertyName">the name of the property that changed</param>
    public void FirePropertyChanged(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            throw new ArgumentNullException(nameof(propertyName));

        if (subscribers.TryGetValue(propertyName, out var ev))
            ev!.Fire();

        if (subscribers.TryGetValue(AnyProperty, out var ev2))
            ev2!.Fire();
    }

    /// <summary>
    ///     A generic equals implementation that allows nulls to be passed for either parameter.  Objects should not call this
    ///     from
    ///     within their own equals method since that will cause a stack overflow.  The Equals() functions do not get called if
    ///     the two
    ///     inputs reference the same object.
    /// </summary>
    /// <param name="a">The first object to test</param>
    /// <param name="b">The second object to test</param>
    /// <returns>True if the values are equal, false otherwise.</returns>
    public static bool EqualsSafe(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if ((a == null) ^ (b == null)) return false;
        if (ReferenceEquals(a, b)) return true;

        return a.Equals(b);
    }
}