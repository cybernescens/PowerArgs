﻿namespace PowerArgs.Cli.Physics;

/// <summary>
///     An interface for a time function that can be plugged into a time simulation
/// </summary>
public interface ITimeFunction
{
    string Id { get; set; }

    /// <summary>
    ///     An event that will be fired when this function is added to a time model
    /// </summary>
    Event Added { get; }

    /// <summary>
    ///     Gets the lifetime of this time function. The end point of the lifetime
    ///     will be when this function is removed from a time model.
    /// </summary>
    Lifetime Lifetime { get; }
}

/// <summary>
///     A base class to use for general purpose time functions that implements all but the
///     functional elements of the time function interface
/// </summary>
public abstract class TimeFunction : ITimeFunction
{
    private readonly HashSet<string> tags;

    public TimeFunction() { tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase); }

    public IEnumerable<string> Tags => tags;
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     An event that will be fired when this function is added to a time model
    /// </summary>
    public Event Added { get; } = new();

    /// <summary>
    ///     Internal state
    /// </summary>
    internal TimeFunctionInternalState InternalState { get; set; } = new(null, TimeSpan.Zero);

    /// <summary>
    ///     Gets the lifetime of this time function. The end point of the lifetime
    ///     will be when this function is removed from a time model.
    /// </summary>
    public Lifetime Lifetime { get; } = new();

    public void AddTag(string tag) => tags.Add(tag);
    public void RemoveTag(string tag) => tags.Remove(tag);

    public void AddTags(IEnumerable<string> tags)
    {
        foreach (var tag in tags)
            this.tags.Add(tag);
    }

    public bool HasSimpleTag(string tag) => tags.Contains(tag);

    public bool HasValueTag(string tag) =>
        tags.Any(t => t.StartsWith(tag + ":", StringComparison.OrdinalIgnoreCase));

    public string GetTagValue(string key)
    {
        key = key.ToLower();
        
        if (TryGetTagValue(key, out var value) == false)
            throw new ArgumentException("There is no value for key: " + key);

        return value!;
    }

    public bool TryGetTagValue(string key, out string? value)
    {
        key = key.ToLower();

        if (HasValueTag(key))
        {
            var tag = tags.First(t => t.ToLower().StartsWith(key + ":", StringComparison.Ordinal));
            value = ParseTagValue(tag);
            return true;
        }

        value = null;
        return false;
    }

    private string ParseTagValue(string tag)
    {
        var splitIndex = tag.IndexOf(':');
        if (splitIndex <= 0) throw new ArgumentException("No tag value present for tag: " + tag);

        var val = tag.Substring(splitIndex + 1, tag.Length - (splitIndex + 1));
        return val;
    }

    /// <summary>
    ///     Gets the age of the given function defined as the amount of simulation time that the function has been a part of
    ///     the model.
    /// </summary>
    /// <param name="function">the function to target</param>
    /// <returns>The age, as a time span</returns>
    public TimeSpan CalculateAge() =>
        Time.CurrentTime.Now - InternalState.AddedTime;
}

/// <summary>
///     Extension methods that target the ITimeFunction interface
/// </summary>
internal static class ITimeFunctionExtensions
{
    /// <summary>
    ///     Determines if the given function is currently attached to a time simulation
    /// </summary>
    /// <param name="function">the function to target</param>
    /// <returns>true if attached to a time model, false otherwise</returns>
    public static bool IsAttached(this TimeFunction function) =>
        function.InternalState.AttachedTime != null;
}

/// <summary>
///     A bookkeeping class that is used internally
/// </summary>
internal record TimeFunctionInternalState(Time? AttachedTime, TimeSpan AddedTime);
