namespace PowerArgs;

/// <summary>
///     A wrapper for char that encapsulates foreground and background colors.
/// </summary>
public readonly partial struct ConsoleCharacter : ICanBeAConsoleString, IEquatable<ConsoleCharacter>
{
    /// <summary>
    ///     The value of the character
    /// </summary>
    public char Value { get; }

    /// <summary>
    ///     The console foreground color to use when printing this character.
    /// </summary>
    public RGB ForegroundColor { get; }

    /// <summary>
    ///     The console background color to use when printing this character.
    /// </summary>
    public RGB BackgroundColor { get; }

    /// <summary>
    ///     True if this character should be underlined when printed
    /// </summary>
    public bool IsUnderlined { get; }

    public static readonly ConsoleCharacter Default = new(
        ' ',
        ConsoleString.DefaultForegroundColor,
        ConsoleString.DefaultBackgroundColor);

    public static readonly ConsoleCharacter LineFeed = new(
        '\n',
        ConsoleString.DefaultForegroundColor,
        ConsoleString.DefaultBackgroundColor);

    public static readonly ConsoleCharacter Null = new(
        char.MinValue,
        ConsoleString.DefaultForegroundColor,
        ConsoleString.DefaultBackgroundColor);

    /// <summary>
    ///     Create a new ConsoleCharacter given a char value and optionally set the foreground or background coor.
    /// </summary>
    /// <param name="value">The character value</param>
    /// <param name="foregroundColor">The foreground color (defaults to the console's foreground color at initialization time).</param>
    /// <param name="backgroundColor">The background color (defaults to the console's background color at initialization time).</param>
    /// <param name="underline"></param>
    public ConsoleCharacter(
        in char value = ' ',
        in RGB? foregroundColor = null,
        in RGB? backgroundColor = null,
        in bool underline = false)
    {
        Value = value;
        IsUnderlined = underline;
        ForegroundColor = foregroundColor ?? ConsoleString.DefaultForegroundColor;
        BackgroundColor = backgroundColor ?? ConsoleString.DefaultBackgroundColor;
    }

    /// <summary>
    ///     Write this formatted character to the console
    /// </summary>
    public void Write() { new ConsoleString(new[] { this }).Write(); }

    /// <summary>
    ///     Gets the string representation of the character
    /// </summary>
    /// <returns></returns>
    public override string ToString() => Value.ToString();

    /// <summary>
    ///     ConsoleCharacters can be compared to other ConsoleCharacter instances or char values.
    /// </summary>
    /// <param name="obj">The ConsoleCharacter or char to compare to.</param>
    /// <returns></returns>
    public override bool Equals(object? obj) =>
        obj switch {
            char c              => Value.Equals(c),
            ConsoleCharacter cc => Equals(cc),
            null                => false,
            _                   => false
        };

    public bool Equals(ConsoleCharacter other) =>
        Value == other.Value &&
        ForegroundColor == other.ForegroundColor &&
        BackgroundColor == other.BackgroundColor &&
        IsUnderlined == other.IsUnderlined;

    public bool EqualsIn(in ConsoleCharacter other) =>
        Value == other.Value &&
        ForegroundColor == other.ForegroundColor &&
        BackgroundColor == other.BackgroundColor &&
        IsUnderlined == other.IsUnderlined;

    /// <summary>
    ///     Operator overload for Equals
    /// </summary>
    /// <param name="a">The first operand</param>
    /// <param name="b">The second operand</param>
    /// <returns></returns>
    public static bool operator ==(in ConsoleCharacter a, in ConsoleCharacter b) => a.EqualsIn(b);

    /// <summary>
    ///     Operator overload for !Equals
    /// </summary>
    /// <param name="a">The first operand</param>
    /// <param name="b">The second operand</param>
    /// <returns></returns>
    public static bool operator !=(in ConsoleCharacter a, in ConsoleCharacter b) => a.EqualsIn(b) == false;

    /// <summary>
    ///     Operator overload for Equals
    /// </summary>
    /// <param name="a">The first operand</param>
    /// <param name="b">The second operand</param>
    /// <returns></returns>
    public static bool operator ==(in ConsoleCharacter a, in char b) => 
        a.EqualsIn(new ConsoleCharacter(b, a.ForegroundColor, a.BackgroundColor, a.IsUnderlined));

    /// <summary>
    ///     Operator overload for !Equals
    /// </summary>
    /// <param name="a">The first operand</param>
    /// <param name="b">The second operand</param>
    /// <returns></returns>
    public static bool operator !=(in ConsoleCharacter a, in  char b) =>
        a.EqualsIn(new ConsoleCharacter(b, a.ForegroundColor, a.BackgroundColor, a.IsUnderlined)) == false;

    public static explicit operator ConsoleCharacter(in char c) => new(c);

    public static explicit operator char(in ConsoleCharacter c) => c.Value;

    /// <summary>
    ///     Override of GetHashcode that returns the internal char's hashcode.
    /// </summary>
    /// <returns>the internal char's hashcode.</returns>
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    ///     Formats this object as a ConsoleString
    /// </summary>
    /// <returns>a ConsoleString</returns>
    public ConsoleString ToConsoleString() { return new ConsoleString(new[] { this }); }

    public ConsoleCharacter ToUnderlined() => new(Value, ForegroundColor, BackgroundColor, true);
}