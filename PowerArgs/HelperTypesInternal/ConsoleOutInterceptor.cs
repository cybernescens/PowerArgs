using System.Text;

namespace PowerArgs;

/// <summary>
///     A singleton text writer that can be used to intercept console output.
/// </summary>
public class ConsoleOutInterceptor : TextWriter
{
    private static readonly Lazy<ConsoleOutInterceptor> interceptor = new(() => new ConsoleOutInterceptor());
    
    private readonly object lockObject = new();
    private readonly List<ConsoleCharacter> intercepted = new();

    private ConsoleOutInterceptor() { }

    /// <summary>
    ///     returns true if the instance is initialized and is intercepting
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    ///     Gets the interceptor, initializing it if needed.
    /// </summary>
    public static ConsoleOutInterceptor Instance => interceptor.Value;

    /// <summary>
    ///     Returns System.Text.Encoding.Default
    /// </summary>
    public override Encoding Encoding => Encoding.Default;

    /// <summary>
    ///     Attaches the interceptor to the Console so that it starts intercepting output
    /// </summary>
    public void Attach()
    {
        IsInitialized = true;
        Console.SetOut(this);
    }

    /// <summary>
    ///     Detaches the interceptor.  Console output will be written as normal.
    /// </summary>
    public void Detach()
    {
        var standardOutput = new StreamWriter(Console.OpenStandardOutput());
        standardOutput.AutoFlush = true;
        Console.SetOut(standardOutput);
        IsInitialized = false;
    }

    /// <summary>
    ///     Intercepts the Write event
    /// </summary>
    /// <param name="buffer">the string buffer</param>
    /// <param name="index">the start index</param>
    /// <param name="count">number of chars to write</param>
    public override void Write(char[] buffer, int index, int count)
    {
        lock (lockObject)
        {
            for (var i = index; i < index + count; i++)
                intercepted.Add(new ConsoleCharacter(buffer[i]));
        }
    }

    /// <summary>
    ///     Intercepts the Write event
    /// </summary>
    /// <param name="value">the char to write</param>
    public override void Write(char value)
    {
        lock (lockObject)
        {
            intercepted.Add(new ConsoleCharacter(value));
        }
    }

    /// <summary>
    ///     Intercepts the Write event
    /// </summary>
    /// <param name="value">the string to write</param>
    public override void Write(string? value)
    {
        if (value == null)
            return;

        lock (lockObject)
        {
            intercepted.AddRange(value.Select(c => new ConsoleCharacter(c)).ToArray());
        }
    }

    /// <summary>
    ///     Pretends to intercept a ConsoleString
    /// </summary>
    /// <param name="value">the string to intercept</param>
    public void Write(ConsoleString value)
    {
        lock (lockObject)
        {
            intercepted.AddRange(value.ToArray());
        }
    }

    /// <summary>
    ///     Reads the queued up intercepted characters and then clears the queue as an atomic operation.
    ///     This method is thread safe.
    /// </summary>
    /// <returns>The queued up intercepted characters</returns>
    public Queue<ConsoleCharacter> ReadAndClear()
    {
        lock (lockObject)
        {
            var ret = new Queue<ConsoleCharacter>(intercepted);
            intercepted.Clear();
            return ret;
        }
    }
}