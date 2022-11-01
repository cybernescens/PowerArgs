using System.Collections;
using System.Runtime.CompilerServices;

namespace PowerArgs;

internal interface IObservableCollection : IEnumerable
{
    int LastModifiedIndex { get; }
    Event<object?> Added { get; }
    Event<object?> Removed { get; }
    Event<IIndexAssignment> AssignedToIndex { get; }
    Event Changed { get; }
    object? this[int index] { get; set; }

    void RemoveAt(int index);
    void Insert(int index, object? item);
}

public interface IIndexAssignment
{
    int Index { get; set; }
    object? OldValue { get; }
    object? NewValue { get; }
}

public class IndexAssignment<T> : IIndexAssignment
{
    public T? OldValue { get; set; }
    public T? NewValue { get; set; }
    public int Index { get; set; }

    object? IIndexAssignment.OldValue => OldValue;
    object? IIndexAssignment.NewValue => NewValue;
}

/// <summary>
///     An observable list implementation
/// </summary>
/// <typeparam name="T">the type of elements this collection will contain</typeparam>
public class ObservableCollection<T> : IList<T>, IObservableCollection, IObservableObject
{
    private readonly Event<object> _untypedAdded = new();
    private readonly Event<object> _untypedRemove = new();
    private readonly Dictionary<T, Lifetime?> membershipLifetimes = new();
    private readonly ObservableObject observable;
    private readonly Event<IIndexAssignment> untyped_Assigned = new();
    private readonly List<T> wrapped = new();

    /// <summary>
    ///     Initialized the collection
    /// </summary>
    public ObservableCollection()
    {
        observable = new ObservableObject(this);
    }

    public string? Id
    {
        get => observable.Get<string>();
        set => observable.Set(value);
    }

    /// <summary>
    ///     Called before an item is added to the list
    /// </summary>
    public Event<T> BeforeAdded { get; } = new();

    /// <summary>
    ///     Called after an item is removed from the list
    /// </summary>
    public Event<T> BeforeRemoved { get; } = new();

    /// <summary>
    ///     Called when an element is added to this list
    /// </summary>
    public Event<T> Added { get; } = new();

    /// <summary>
    ///     Called when an element is removed from this list
    /// </summary>
    public Event<T> Removed { get; } = new();

    /// <summary>
    ///     Called whenever an index assignment is made
    /// </summary>
    public Event<IndexAssignment<T>> AssignedToIndex { get; } = new();

    /// <summary>
    ///     Returns the index of the given item in the list
    /// </summary>
    /// <param name="item">the item to look for</param>
    /// <returns>the index or a negative number if the element is not in the list</returns>
    public int IndexOf(T item) => wrapped.IndexOf(item);

    /// <summary>
    ///     Inserts the given item into the list at the specified position
    /// </summary>
    /// <param name="index">the index to insert the item into</param>
    /// <param name="item">the item to insert</param>
    public void Insert(int index, T item)
    {
        LastModifiedIndex = index;
        FireBeforeAdded(item);
        wrapped.Insert(index, item);
        FireAdded(item);
    }

    /// <summary>
    ///     Removes the element at the specified index
    /// </summary>
    /// <param name="index">the index of the item to remove</param>
    public void RemoveAt(int index)
    {
        LastModifiedIndex = index;
        var item = wrapped[index];
        FireBeforeRemoved(item);
        wrapped.RemoveAt(index);
        FireRemoved(item);
    }

    /// <summary>
    ///     Gets or sets the value at a particular index
    /// </summary>
    /// <param name="index">the index of the item to get or set</param>
    /// <returns>the value at a particular index</returns>
    public T this[int index]
    {
        get => wrapped[index];
        set {
            var oldItem = wrapped[index];
            if (SuppressEqualChanges && ObservableObject.EqualsSafe(oldItem, value))
                return;

            LastModifiedIndex = index;

            FireBeforeRemoved(oldItem);
            FireBeforeAdded(value);

            wrapped[index] = value;

            FireAssignedToIndex(value, oldItem);
            FireRemoved(oldItem);
            FireAdded(value);
        }
    }

    /// <summary>
    ///     Adds the given item to the list
    /// </summary>
    /// <param name="item">the item to add</param>
    public void Add(T item)
    {
        LastModifiedIndex = Count;
        FireBeforeAdded(item);
        wrapped.Add(item);
        FireAdded(item);
    }

    /// <summary>
    ///     Removes all items from the collection
    /// </summary>
    public void Clear()
    {
        var items = wrapped.ToArray();
        wrapped.Clear();
        LastModifiedIndex = 0;
        for (var i = 0; i < items.Length; i++)
            FireRemoved(items[i]);
    }

    /// <summary>
    ///     Tests to see if the list contains the given item
    /// </summary>
    /// <param name="item">the item to look for</param>
    /// <returns>true if the list contains the given item, false otherwise</returns>
    public bool Contains(T item) => wrapped.Contains(item);

    /// <summary>
    ///     Copies values from this list into the given array starting at the given index in the destination
    /// </summary>
    /// <param name="array">the destination array</param>
    /// <param name="arrayIndex">the index in the destination array to start the copy</param>
    public void CopyTo(T[] array, int arrayIndex) { wrapped.CopyTo(array, arrayIndex); }

    /// <summary>
    ///     Gets the number of items in the list
    /// </summary>
    public int Count => wrapped.Count;

    /// <summary>
    ///     Always returns false
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    ///     Removes the given item from the list
    /// </summary>
    /// <param name="item">the item to remove</param>
    /// <returns>true if an item was removed, false if the item was not found in the list</returns>
    public bool Remove(T item)
    {
        if (!wrapped.Contains(item))
            return false;

        FireBeforeRemoved(item);

        for (var i = 0; i < wrapped.Count; i++)
        {
            if (ObservableObject.EqualsSafe(wrapped[i], item))
            {
                LastModifiedIndex = i;
                wrapped.RemoveAt(i);
                break;
            }
        }

        FireRemoved(item);
        return true;
    }

    /// <summary>
    ///     Gets an enumerator for this list
    /// </summary>
    /// <returns>an enumerator for this list</returns>
    public IEnumerator<T> GetEnumerator() => wrapped.GetEnumerator();

    /// <summary>
    ///     Gets an enumerator for this list
    /// </summary>
    /// <returns>an enumerator for this list</returns>
    IEnumerator IEnumerable.GetEnumerator() => wrapped.GetEnumerator();

    public int LastModifiedIndex { get; private set; }
    Event<object> IObservableCollection.Added => _untypedAdded;
    Event<object> IObservableCollection.Removed => _untypedRemove;
    Event<IIndexAssignment> IObservableCollection.AssignedToIndex => untyped_Assigned;

    /// <summary>
    ///     Called whenever this list changes.  You may receive one event for multiple changes
    ///     if the changes were atomic (e.g. after calling Clear()).
    /// </summary>
    public Event Changed { get; } = new();

    void IObservableCollection.Insert(int index, object? item) => Insert(index, (T)item);

    object IObservableCollection.this[int index]
    {
        get => this[index];
        set => this[index] = (T)value;
    }

    public bool SuppressEqualChanges { get; set; } = true;

    public IDisposable SubscribeUnmanaged(string propertyName, Action handler) =>
        observable.SubscribeUnmanaged(propertyName, handler);

    public void SubscribeForLifetime(ILifetimeManager lifetimeManager, string propertyName, Action handler) =>
        observable.SubscribeForLifetime(lifetimeManager, propertyName, handler);

    public IDisposable SynchronizeUnmanaged(string propertyName, Action handler) =>
        observable.SynchronizeUnmanaged(propertyName, handler);

    public void SynchronizeForLifetime(string propertyName, Action handler, ILifetimeManager? lifetimeManager) =>
        observable.SynchronizeForLifetime(propertyName, handler, lifetimeManager);

    public T? Get<T>([CallerMemberName] string name = "") => observable.Get<T>(name);
    public void Set<T>(T? value, [CallerMemberName] string name = "") => observable.Set(value);
    public Lifetime GetPropertyValueLifetime(string propertyName) => observable.GetPropertyValueLifetime(propertyName);

    /// <summary>
    ///     Gets the previous value of the given property
    /// </summary>
    /// <param name="name">the name of the property to lookup</param>
    /// <returns>the previous value or null if there was no value</returns>
    public object? GetPrevious(string name) => observable.GetPrevious<object>(name);

    public void SynchronizeForLifetime(
        Action<T> addAction,
        Action<T> removeAction,
        Action changedAction,
        ILifetimeManager? manager)
    {
        Added.SubscribeForLifetime(manager, addAction);
        Removed.SubscribeForLifetime(manager, removeAction);
        Changed.SubscribeForLifetime(manager, changedAction);

        foreach (var obj in this.ToArray())
            addAction(obj);

        changedAction();
    }

    /// <summary>
    ///     Fires the Added event for the given item
    /// </summary>
    /// <param name="item">The item that was added</param>
    internal void FireAdded(T? item)
    {
        membershipLifetimes.Add(item, new Lifetime());
        Added.Fire(item);
        _untypedAdded.Fire(item);
        Changed.Fire();
    }

    /// <summary>
    ///     Fired the Removed event for the given item
    /// </summary>
    /// <param name="item">The item that was removed</param>
    internal void FireRemoved(T? item)
    {
        Removed.Fire(item);
        _untypedRemove.Fire(item);
        Changed.Fire();
        var itemLifetime = membershipLifetimes[item];
        membershipLifetimes.Remove(item);
        itemLifetime.Dispose();
    }

    internal void FireAssignedToIndex(T? added, T? removed)
    {
        var assignmentArgs = new IndexAssignment<T?>
            { Index = LastModifiedIndex, NewValue = added, OldValue = removed };

        AssignedToIndex.Fire(assignmentArgs);
        untyped_Assigned.Fire(assignmentArgs);
        Changed.Fire();
    }

    public ILifetimeManager? GetMembershipLifetime(T item) => membershipLifetimes[item];

    internal void FireBeforeAdded(T? item) { BeforeAdded.Fire(item); }

    internal void FireBeforeRemoved(T? item) { BeforeRemoved.Fire(item); }
}