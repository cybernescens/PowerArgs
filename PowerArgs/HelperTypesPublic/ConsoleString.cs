using System.Collections;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using PowerArgs.Cli;

namespace PowerArgs;

/// <summary>
///     An interface that defines an object that implements ToConsoleString
/// </summary>
public interface ICanBeAConsoleString
{
    /// <summary>
    ///     Formats this object as a ConsoleString
    /// </summary>
    /// <returns>a ConsoleString</returns>
    ConsoleString ToConsoleString();
}

/// <summary>
///     A wrapper for string that encapsulates foreground and background colors.  ConsoleStrings are immutable.
/// </summary>
public class ConsoleString : IEnumerable<ConsoleCharacter>, IComparable<string>, ICanBeAConsoleString
{
    private static readonly Dictionary<ConsoleColor, string> CSSMap = new() {
        { ConsoleColor.Black, "black" }, 
        { ConsoleColor.Blue, "blue" },
        { ConsoleColor.Cyan, "cyan" },
        { ConsoleColor.DarkBlue, "blue" },
        { ConsoleColor.DarkCyan, "cyan" },
        { ConsoleColor.DarkGray, "grey" },
        { ConsoleColor.DarkGreen, "green" },
        { ConsoleColor.DarkMagenta, "magenta" },
        { ConsoleColor.DarkRed, "red" },
        { ConsoleColor.DarkYellow, "yellow" },
        { ConsoleColor.Gray, "grey" },
        { ConsoleColor.Green, "green" },
        { ConsoleColor.Magenta, "magenta" },
        { ConsoleColor.Red, "red" },
        { ConsoleColor.White, "white" },
        { ConsoleColor.Yellow, "yellow" }
    };

    /// <summary>
    ///     Represents an empty string.
    /// </summary>
    public static readonly ConsoleString Empty = new(Array.Empty<ConsoleCharacter>());

    /// <summary>
    ///     Represents a blank console character.
    /// </summary>
    public static readonly ConsoleString Default = new(new [] { ConsoleCharacter.Default });

    /// <summary>
    ///     Represents a new line.
    /// </summary>
    public static readonly ConsoleString LineFeed = new(new [] { ConsoleCharacter.LineFeed });

    private readonly ConsoleCharacter[] characters;

    static ConsoleString()
    {
        ConsoleProvider = new StdConsoleProvider();
        try
        {
            DefaultForegroundColor = ConsoleProvider.ForegroundColor;
            DefaultBackgroundColor = ConsoleProvider.BackgroundColor;
        }
        catch (Exception)
        {
            DefaultForegroundColor = ConsoleColor.Gray;
            DefaultBackgroundColor = ConsoleColor.Black;
        }
    }

    /// <summary>
    ///     Create a new empty ConsoleString
    /// </summary>
    public ConsoleString(params ConsoleCharacter[] chars)
    {
        characters = chars.ToArray();
    }

    /// <summary>
    ///     Creates a new ConsoleString from a collection of ConsoleCharacter objects
    /// </summary>
    /// <param name="chars">The value to use to seed this string</param>
    public ConsoleString(in IEnumerable<ConsoleCharacter> chars) : this(chars.ToArray()) { }

    /// <summary>
    ///     Create a ConsoleString given an initial text value and optional color info.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="foregroundColor">The foreground color (defaults to the console's foreground color at initialization time).</param>
    /// <param name="backgroundColor">The background color (defaults to the console's background color at initialization time).</param>
    /// <param name="underline">If true then underlines in an Ansi supported console.</param>
    public ConsoleString(
        string value,
        RGB? foregroundColor = null,
        RGB? backgroundColor = null,
        bool underline = false) : this(
        value == string.Empty
            ? Empty
            : value.Select(c => new ConsoleCharacter(c, foregroundColor, backgroundColor, underline)).ToArray()) { }

    /// <summary>
    ///     The console provider to use when writing output
    /// </summary>
    public static IConsoleProvider ConsoleProvider { get; set; }

    /// <summary>
    ///     Gets the console's default foreground color
    /// </summary>
    public static RGB DefaultForegroundColor { get; set; }

    /// <summary>
    ///     Gets the console's default background color
    /// </summary
    public static RGB DefaultBackgroundColor { get; set; }

    /// <summary>
    ///     Gets the string value of this ConsoleString.  Useful when using the debugger.
    /// </summary>
    public string StringValue => ToString();

    /// <summary>
    ///     The length of the string.
    /// </summary>
    public int Length => characters.Length;

    public ConsoleString Darker
    {
        get {
            var buffer = new ConsoleCharacter[Length];
            for (var i = 0; i < Length; i++)
                buffer[i] = new ConsoleCharacter(
                    this[i].Value,
                    this[i].ForegroundColor.Darker,
                    this[i].BackgroundColor.Darker);

            return new ConsoleString(buffer);
        }
    }

    /// <summary>
    ///     Returns true if all characters have the default foreground and background color
    /// </summary>
    public bool IsUnstyled =>
        !this.Any(c => c.ForegroundColor != DefaultForegroundColor || c.BackgroundColor != DefaultBackgroundColor);

    /// <summary>
    ///     Gets the character at the specified index
    /// </summary>
    /// <param name="index">the index of the character to find</param>
    /// <returns>the character at the specified index</returns>
    public ConsoleCharacter this[int index] => characters[index];

    /// <summary>
    ///     Formats this object as a ConsoleString
    /// </summary>
    /// <returns>a ConsoleString</returns>
    public ConsoleString ToConsoleString() => this;

    /// <summary>
    ///     Compare this ConsoleString to another ConsoleString.
    /// </summary>
    /// <param name="other">The ConsoleString to compare to.</param>
    /// <returns>True if equal, false otherwise</returns>
    public int CompareTo(string? other) => string.Compare(ToString(), other, StringComparison.Ordinal);

    /// <summary>
    ///     Gets an enumerator for this string
    /// </summary>
    /// <returns>an enumerator for this string</returns>
    public IEnumerator<ConsoleCharacter> GetEnumerator() => ((IEnumerable<ConsoleCharacter>)characters).GetEnumerator();

    /// <summary>
    ///     Gets an enumerator for this string
    /// </summary>
    /// <returns>an enumerator for this string</returns>
    IEnumerator IEnumerable.GetEnumerator() => characters.GetEnumerator();

    public ReadOnlySpan<ConsoleCharacter> AsSpan() => characters.AsSpan();

    /// <summary>
    ///     Converts a collection of plain strings into ConsoleStrings
    /// </summary>
    /// <param name="plainStrings">the input strings</param>
    /// <param name="foregroundColor">the foreground color of all returned ConsoleStrings</param>
    /// <param name="backgroundColor">the background color of all returned ConsoleStrings</param>
    /// <returns>a collection of ConsoleStrings</returns>
    public static List<ConsoleString> ToConsoleStrings(
        IEnumerable<string?> plainStrings,
        RGB? foregroundColor = null,
        RGB? backgroundColor = null)
    {
        return plainStrings.Select(s => new ConsoleString(s, foregroundColor, backgroundColor)).ToList();
    }

    /// <summary>
    ///     Returns a new string that Appends the given value to this one using the formatting of the last character or the
    ///     default formatting if this ConsoleString is empty.
    /// </summary>
    /// <param name="value">The string to append.</param>
    public ConsoleString AppendUsingCurrentFormat(string? value)
    {
        if (Length == 0)
            return ImmutableAppend(value);

        var prototype = this[^1];
        return ImmutableAppend(value, prototype.ForegroundColor, prototype.BackgroundColor);
    }

    /// <summary>
    ///     Replaces all occurrences of the given string with the replacement value using the specified formatting.
    /// </summary>
    /// <param name="toFind">The substring to find</param>
    /// <param name="toReplace">The replacement value</param>
    /// <param name="foregroundColor">The foreground color (defaults to the console's foreground color at initialization time).</param>
    /// <param name="backgroundColor">The background color (defaults to the console's background color at initialization time).</param>
    /// <param name="comparison">Specifies how characters are compared</param>
    /// <returns>A new ConsoleString with the replacements.</returns>
    public ConsoleString Replace(
        string toFind,
        string toReplace,
        RGB? foregroundColor = null,
        RGB? backgroundColor = null,
        StringComparison comparison = StringComparison.InvariantCulture)
    {
        var ret = new List<ConsoleCharacter>(this);

        var startIndex = 0;

        while (true)
        {
            var chars = new char[ret.Count];
            for (var i = 0; i < ret.Count; i++)
                chars[i] = ret[i].Value;

            var toString = new string(chars);
            var currentIndex = toString.IndexOf(toFind, startIndex, comparison);
            if (currentIndex < 0) break;

            for (var i = 0; i < toFind.Length; i++) ret.RemoveAt(currentIndex);

            var range = new ConsoleCharacter[toReplace.Length];
            for (var i = 0; i < toReplace.Length; i++)
                range[i] = new ConsoleCharacter(toReplace[i], foregroundColor, backgroundColor);

            ret.InsertRange(currentIndex, range);
            startIndex = currentIndex + toReplace.Length;
        }

        return new ConsoleString(ret);
    }

    /// <summary>
    ///     Highlights all occurrences of the given string with the desired foreground and background color.
    /// </summary>
    /// <param name="toFind">The substring to find</param>
    /// <param name="foregroundColor">The foreground color (defaults to the console's foreground color at initialization time).</param>
    /// <param name="backgroundColor">The background color (defaults to the console's background color at initialization time).</param>
    /// <param name="comparison">Specifies how characters are compared</param>
    /// <returns>A new ConsoleString with the highlights.</returns>
    public ConsoleString? Highlight(
        string? toFind,
        RGB? foregroundColor = null,
        RGB? backgroundColor = null,
        StringComparison comparison = StringComparison.InvariantCulture)
    {
        var ret = new List<ConsoleCharacter>(this);
        if (string.IsNullOrEmpty(toFind))
            return new ConsoleString(ret);

        var startIndex = 0;

        while (true)
        {
            var toString = new string(ret.Select(r => r.Value).ToArray());
            var currentIndex = toString.IndexOf(toFind, startIndex, comparison);
            if (currentIndex < 0) break;

            var replacement = string.Empty;
            for (var i = 0; i < toFind.Length; i++)
            {
                replacement += ret[currentIndex].Value;
                ret.RemoveAt(currentIndex);
            }

            ret.InsertRange(
                currentIndex,
                replacement.Select(c => new ConsoleCharacter(c, foregroundColor, backgroundColor)));

            startIndex = currentIndex + replacement.Length;
        }

        return new ConsoleString(ret);
    }

    /// <summary>
    ///     Creates a new ConsoleString with the same characters as this one, but with a
    ///     new background color
    /// </summary>
    /// <param name="bg">the new background color</param>
    /// <returns>A  new string with a different background color</returns>
    public ConsoleString ToDifferentBackground(RGB bg) => 
        new(this.Select(c => new ConsoleCharacter(c.Value, c.ForegroundColor, bg)));

    /// <summary>
    ///     Creates a new ConsoleString with the sams characters as this one, but with an underlined style.
    /// </summary>
    /// <returns></returns>
    public ConsoleString ToUnderlined() => new(this.Select(c => c.ToUnderlined()));

    /// <summary>
    ///     Returns a new ConsoleString that is a copy of this ConsoleString, but applies the given style to the range of
    ///     characters specified.
    /// </summary>
    /// <param name="start">the start index to apply the highlight</param>
    /// <param name="length">the number of characters to apply the highlight</param>
    /// <param name="foregroundColor">
    ///     the foreground color to apply to the highlighted characters or null to use the default
    ///     foreground color
    /// </param>
    /// <param name="backgroundColor">
    ///     the background color to apply to the highlighted characters or null to use the default
    ///     background color
    /// </param>
    /// <returns>
    ///     a new ConsoleString that is a copy of this ConsoleString, but applies the given style to the range of
    ///     characters specified.
    /// </returns>
    public ConsoleString? HighlightSubstring(
        int start,
        int length,
        RGB? foregroundColor = null,
        RGB? backgroundColor = null) =>
        new(this.Select(
            (x, i) => i >= start && i < start + length
                ? new ConsoleCharacter(x.Value, foregroundColor, backgroundColor)
                : x));

    /// <summary>
    ///     Replaces all matches of the given regular expression with the replacement value using the specified formatting.
    /// </summary>
    /// <param name="regex">The regular expression to find.</param>
    /// <param name="toReplace">The replacement value</param>
    /// <param name="foregroundColor">The foreground color (defaults to the console's foreground color at initialization time).</param>
    /// <param name="backgroundColor">The background color (defaults to the console's background color at initialization time).</param>
    /// <returns></returns>
    public ConsoleString ReplaceRegex(
        string regex,
        string toReplace,
        RGB? foregroundColor = null,
        RGB? backgroundColor = null)
    {
        var ret = new ConsoleString(this);
        var matches = Regex.Matches(ToString(), regex);
        foreach (Match match in matches)
            ret = ret.Replace(match.Value, toReplace ?? match.Value, foregroundColor, backgroundColor);

        return ret;
    }

    /// <summary>
    ///     Finds the index of a given substring in this ConsoleString.
    /// </summary>
    /// <param name="toFind">The substring to search for.</param>
    /// <param name="comparison">Specifies how characters are compared</param>
    /// <returns>The first index of the given substring or -1 if the substring was not found.</returns>
    public int IndexOf(string? toFind, StringComparison comparison = StringComparison.InvariantCulture) =>
        IndexOf(toFind.ToConsoleString(), comparison);

    /// <summary>
    ///     Finds the index of a given substring in this ConsoleString.
    /// </summary>
    /// <param name="toFind">The substring to search for. The styles of the strings must match.</param>
    /// <param name="comparison">Specifies how characters are compared</param>
    /// <returns>The first index of the given substring or -1 if the substring was not found.</returns>
    public int IndexOf(ConsoleString? toFind, StringComparison comparison = StringComparison.InvariantCulture)
    {
        if (toFind == null) return -1;
        if (toFind == Empty) return 0;

        var j = 0;
        var k = 0;
        for (var i = 0; i < Length; i++)
        {
            j = 0;
            k = 0;

            while (toFind[j].ForegroundColor == characters[i + k].ForegroundColor &&
                   toFind[j].BackgroundColor == characters[i + k].BackgroundColor &&
                   (toFind[j] + "").Equals("" + characters[i + k].Value, comparison))
            {
                j++;
                k++;
                if (j == toFind.Length) return i;
                if (i + k == Length) return -1;
            }
        }

        return -1;
    }

    /// <summary>
    ///     Determines if this ConsoleString starts with the given string
    /// </summary>
    /// <param name="substr">the substring to look for</param>
    /// <param name="comparison">Specifies how characters are compared</param>
    /// <returns>true if this ConsoleString starts with the given substring, false otherwise</returns>
    public bool StartsWith(string substr, StringComparison comparison = StringComparison.InvariantCulture) =>
        IndexOf(substr, comparison) == 0;

    /// <summary>
    ///     Determines if this ConsoleString ends with the given string
    /// </summary>
    /// <param name="substr">the substring to look for</param>
    /// <param name="comparison">Specifies how characters are compared</param>
    /// <returns>true if this ConsoleString ends with the given substring, false otherwise</returns>
    public bool EndsWith(string substr, StringComparison comparison = StringComparison.InvariantCulture) =>
        substr.Length <= Length && Substring(Length - substr.Length).StringValue.Equals(substr, comparison);

    /// <summary>
    ///     Determines if this ConsoleString contains the given substring.
    /// </summary>
    /// <param name="substr">The substring to search for.</param>
    /// <param name="comparison">Specifies how characters are compared</param>
    /// <returns>True if found, false otherwise.</returns>
    public bool Contains(string substr, StringComparison comparison = StringComparison.InvariantCulture) =>
        IndexOf(substr, comparison) >= 0;

    /// <summary>
    ///     Determines if this ConsoleString contains the given substring.
    /// </summary>
    /// <param name="substr">The substring to search for.</param>
    /// <param name="comparison">Specifies how characters are compared</param>
    /// <returns>True if found, false otherwise.</returns>
    public bool Contains(ConsoleString? substr, StringComparison comparison = StringComparison.InvariantCulture) =>
        IndexOf(substr, comparison) >= 0;

    /// <summary>
    ///     Get a substring of this ConsoleString starting at the given index.
    /// </summary>
    /// <param name="start">the start index.</param>
    /// <returns>A new ConsoleString representing the substring requested.</returns>
    public ConsoleString Substring(int start) => Substring(start, Length - start);

    /// <summary>
    ///     Returns a new string that has had all leading whitespace removed
    /// </summary>
    /// <returns></returns>
    public ConsoleString TrimStart()
    {
        var toTrim = 0;
        for (var i = 0; i < Length; i++)
        {
            if (!char.IsWhiteSpace(characters[i].Value))
            {
                break;
            }

            toTrim++;
        }

        return Substring(toTrim);
    }

    /// <summary>
    ///     Get a substring of this ConsoleString starting at the given index and with the given length.
    /// </summary>
    /// <param name="start">the start index.</param>
    /// <param name="length">the number of characters to return</param>
    /// <returns>A new ConsoleString representing the substring requested.</returns>
    public ConsoleString Substring(int start, int length)
    {
        var ret = new List<ConsoleCharacter>();
        for (var i = start; i < start + length; i++)
            ret.Add(characters[i]);

        return new ConsoleString(ret);
    }

    /// <summary>
    ///     Writes the string representation of the given object to the console using the specified colors.
    /// </summary>
    /// <param name="o">The object to write</param>
    /// <param name="fg">The foreground color to use</param>
    /// <param name="bg">The background color to use</param>
    public static void Write(object? o, RGB? fg = null, RGB? bg = null)
    {
        var str = o == null ? "" : o.ToString();
        new ConsoleString(str, fg, bg).Write();
    }

    /// <summary>
    ///     Writes the string representation of the given object to the console using the specified colors and appends a
    ///     newline.
    /// </summary>
    /// <param name="o">The object to write</param>
    /// <param name="fg">The foreground color to use</param>
    /// <param name="bg">The background color to use</param>
    public static void WriteLine(object? o, RGB? fg = null, RGB? bg = null)
    {
        var str = o == null ? "" : o.ToString();
        new ConsoleString(str, fg, bg).WriteLine();
    }

    /// <summary>
    ///     Writes the given ConsoleString to the console
    /// </summary>
    /// <param name="str">the string to write</param>
    public static void Write(ConsoleString str) { str.Write(); }

    /// <summary>
    ///     Writes the given ConsoleString to the console and appends a newline
    /// </summary>
    /// <param name="str">the string to write</param>
    public static void WriteLine(ConsoleString str) { str.WriteLine(); }

    /// <summary>
    ///     Writes the string to the console using the specified colors.
    /// </summary>
    /// <param name="str">The object to write</param>
    /// <param name="fg">The foreground color to use</param>
    /// <param name="bg">The background color to use</param>
    public static void Write(string? str, RGB? fg = null, RGB? bg = null) { new ConsoleString(str, fg, bg).Write(); }

    /// <summary>
    ///     Writes the string to the console using the specified colors and appends a newline.
    /// </summary>
    /// <param name="str">The object to write</param>
    /// <param name="fg">The foreground color to use</param>
    /// <param name="bg">The background color to use</param>
    public static void WriteLine(string? str, RGB? fg = null, RGB? bg = null)
    {
        new ConsoleString(str, fg, bg).WriteLine();
    }

    /// <summary>
    ///     Write this ConsoleString to the console using the desired style.
    /// </summary>
    public void Write()
    {
        if (ConsoleOutInterceptor.Instance.IsInitialized)
        {
            ConsoleOutInterceptor.Instance.Write(this);
        }
        else if (PowerArgs.ConsoleProvider.Fancy == false)
        {
            var buffer = "";

            RGB existingForeground = ConsoleProvider.ForegroundColor,
                existingBackground = ConsoleProvider.BackgroundColor;

            try
            {
                RGB currentForeground = existingForeground, currentBackground = existingBackground;
                foreach (var character in this)
                {
                    if (character.ForegroundColor != currentForeground ||
                        character.BackgroundColor != currentBackground)
                    {
                        ConsoleProvider.Write(buffer);
                        ConsoleProvider.ForegroundColor = character.ForegroundColor;
                        ConsoleProvider.BackgroundColor = character.BackgroundColor;
                        currentForeground = character.ForegroundColor;
                        currentBackground = character.BackgroundColor;
                        buffer = "";
                    }

                    buffer += character.Value;
                }

                if (buffer.Length > 0) ConsoleProvider.Write(buffer);
            }
            finally
            {
                ConsoleProvider.ForegroundColor = existingForeground;
                ConsoleProvider.BackgroundColor = existingBackground;
            }
        }
        else
        {
            var buffer = "";

            RGB existingForeground = ConsoleProvider.ForegroundColor,
                existingBackground = ConsoleProvider.BackgroundColor;

            try
            {
                RGB currentForeground = existingForeground, currentBackground = existingBackground;
                var currentUnderlined = false;
                foreach (var character in this)
                {
                    if (character.ForegroundColor != currentForeground ||
                        character.BackgroundColor != currentBackground ||
                        character.IsUnderlined != currentUnderlined)
                    {
                        if (buffer.Length > 0)
                        {
                            WriteFancy(buffer, currentForeground, currentBackground, currentUnderlined);
                        }

                        currentForeground = character.ForegroundColor;
                        currentBackground = character.BackgroundColor;
                        currentUnderlined = character.IsUnderlined;
                        buffer = "";
                    }

                    buffer += character.Value;
                }

                if (buffer.Length > 0)
                {
                    WriteFancy(buffer, currentForeground, currentBackground, currentUnderlined);
                    buffer = "";
                }
            }
            finally
            {
                SetColorsFancy(existingBackground, existingBackground);
            }
        }
    }

    private void WriteFancy(string content, in RGB fg, in RGB bg, bool underlined)
    {
        var toWrite = string.Empty;

        if (underlined)
        {
            toWrite += Ansi.Text.UnderlinedOn;
        }

        toWrite += Ansi.Cursor.SavePosition;
        toWrite += Ansi.Color.Foreground.Rgb(fg);
        toWrite += Ansi.Color.Background.Rgb(bg);
        toWrite += content;

        if (underlined)
        {
            toWrite += Ansi.Text.UnderlinedOff;
        }

        Console.Write(toWrite.Replace("\n", Ansi.Cursor.Move.NextLine()));
    }

    private void SetColorsFancy(in RGB fg, in RGB bg)
    {
        var toWrite = string.Empty;
        toWrite += Ansi.Cursor.SavePosition;
        toWrite += Ansi.Color.Foreground.Rgb(fg);
        toWrite += Ansi.Color.Background.Rgb(bg);
        Console.Write(toWrite);
    }

    /// <summary>
    ///     Write this ConsoleString to the console using the desired style.  A newline is appended.
    /// </summary>
    public void WriteLine()
    {
        var withNewLine = this + Environment.NewLine;
        withNewLine.Write();
    }

    /// <summary>
    ///     Get the string representation of this ConsoleString.
    /// </summary>
    /// <returns></returns>
    public override string ToString() { return new string(this.Select(c => c.Value).ToArray()); }

    /// <summary>
    ///     Serializes the console string, preserving the formatting by inserting color markers inside the string. You can
    ///     later parse the serialized string back into a ConsoleString structure.
    /// </summary>
    /// <param name="implicitDefaultsMode">
    ///     if true, characters that use the system's default foreground and background colors
    ///     will be encoded with the knowledge that those characters were 'default' rather than explicitly capturing the
    ///     default values
    /// </param>
    /// <returns>A string that can be later parsed into a ConsoleString structure</returns>
    public string Serialize(bool implicitDefaultsMode = false)
    {
        if (Length == 0) return string.Empty;

        var currentFg = this[0].ForegroundColor;
        var currentBg = this[0].BackgroundColor;
        var currentUnderlined = false;

        var defaultFg = new ConsoleCharacter(' ').ForegroundColor;
        var defaultBg = new ConsoleCharacter(' ').BackgroundColor;

        var builder = new StringBuilder();

        if (implicitDefaultsMode == false || currentFg != defaultFg)
        {
            builder.Append($"[{currentFg}]");
        }

        if (implicitDefaultsMode == false || currentBg != defaultBg)
        {
            builder.Append($"[B={currentBg}]");
        }

        foreach (var c in this)
        {
            if (c.IsUnderlined && currentUnderlined == false)
            {
                builder.Append("[U]");
                currentUnderlined = true;
            }
            else if (c.IsUnderlined == false && currentUnderlined)
            {
                builder.Append("[!U]");
                currentUnderlined = false;
            }

            if (c.ForegroundColor != currentFg && c.BackgroundColor != currentBg)
            {
                currentFg = c.ForegroundColor;
                currentBg = c.BackgroundColor;

                if (implicitDefaultsMode && currentFg == defaultFg && currentBg == defaultBg)
                {
                    builder.Append("[D]");
                }
                else
                {
                    builder.Append($"[{c.ForegroundColor}]");
                    builder.Append($"[B={c.BackgroundColor}]");
                }
            }
            else if (c.ForegroundColor != currentFg)
            {
                currentFg = c.ForegroundColor;
                if (implicitDefaultsMode && currentFg == defaultFg && currentBg == defaultBg)
                {
                    builder.Append("[D]");
                }
                else
                {
                    builder.Append($"[{c.ForegroundColor}]");
                }
            }
            else if (c.BackgroundColor != currentBg)
            {
                currentBg = c.BackgroundColor;
                if (implicitDefaultsMode && currentFg == defaultFg && currentBg == defaultBg)
                {
                    builder.Append("[D]");
                }
                else
                {
                    builder.Append($"[B={c.BackgroundColor}]");
                }
            }

            if (c.Value == '[' || c.Value == ']')
            {
                builder.Append(@"\");
            }

            builder.Append(c.Value);
        }

        var ret = builder.ToString();
        return ret;
    }

    /// <summary>
    ///     Parses a serialized ConsoleString that's either been hand crafted or output from the ConsoleString.Serialize()
    ///     method.
    ///     To indicate that characters should be yellow, preceed those characters with [Yellow].
    ///     To indicate that the background of characters should be green, preceed those characters with [B=Green].
    ///     To indicate that characters should use the current console's default styling, preceed those characters with [D].
    ///     If the input string does not start with any of these indicators then all characters up until the first indicator
    ///     will be treated as if they were preceeded with [D].
    ///     In these examples I used Yellow and Green. You can substitute these with any color from the System.ConsoleColor
    ///     enum.
    ///     If the input string contains a '[' or a ']' then those must be escaped with a '\'.
    /// </summary>
    /// <param name="serializedConsoleString">The serialized string to parse</param>
    /// <param name="defaultFg">
    ///     optionally specify the default foreground color to apply for characters with an explicit
    ///     default
    /// </param>
    /// <param name="defaultBg">
    ///     optionally specify the default background color to apply for characters with an explicit
    ///     default
    /// </param>
    /// <returns>a rehydrated ConsoleString</returns>
    public static ConsoleString Parse(string? serializedConsoleString, RGB? defaultFg = null, RGB? defaultBg = null)
    {
        var tokenizer = new Tokenizer<Token>();
        tokenizer.Delimiters.Add("[");
        tokenizer.Delimiters.Add("]");
        tokenizer.Delimiters.Add("=");

        var reader = new TokenReader<Token>(tokenizer.Tokenize(serializedConsoleString));

        var chars = new List<ConsoleCharacter>();

        defaultFg ??= new ConsoleCharacter(' ').ForegroundColor;
        defaultBg ??= new ConsoleCharacter(' ').BackgroundColor;

        var currentFg = defaultFg.Value;
        var currentBg = defaultBg.Value;
        var currentUnderlined = false;
        while (reader.TryAdvance(out var t))
        {
            if (t.Value != "[")
            {
                chars.AddRange(new ConsoleString(t.Value, currentFg, currentBg, currentUnderlined));
            }
            else
            {
                t = reader.Advance(true);

                if (reader.Peek(true).Value == "=")
                {
                    if (t.Value != "B")
                    {
                        throw new FormatException($"Expected 'B' @ {t.Position}");
                    }

                    reader.Advance(true);     // read the equals
                    t = reader.Advance(true); // read the token after the equals
                    if (Enum.TryParse(t.Value, out ConsoleColor consoleColor))
                    {
                        currentBg = consoleColor;
                    }
                    else if (RGB.TryParse(t.Value, out currentBg) == false)
                    {
                        throw new FormatException($"Expected a color, got {t.Value} @ {t.Position}");
                    }
                }
                else if (t.Value == "D")
                {
                    currentFg = defaultFg.Value;
                    currentBg = defaultBg.Value;
                }
                else if (t.Value == "U")
                {
                    currentUnderlined = true;
                }
                else if (t.Value == "!U")
                {
                    currentUnderlined = false;
                }
                else
                {
                    if (Enum.TryParse(t.Value, out ConsoleColor consoleColor))
                    {
                        currentFg = consoleColor;
                    }
                    else if (RGB.TryParse(t.Value, out currentFg) == false)
                    {
                        throw new FormatException($"Expected a color, got {t.Value} @ {t.Position}");
                    }
                }

                reader.Expect("]", true);
            }
        }

        return new ConsoleString(chars);
    }

    /// <summary>
    ///     Splits this ConsoleString into segments given a split value
    /// </summary>
    /// <param name="splitValue">the value to split on</param>
    /// <param name="comparer">optionally override the comparison function</param>
    /// <returns>A list of segments</returns>
    public List<ConsoleString> Split(string? splitValue, IEqualityComparer<string?>? comparer = null) =>
        Split(splitValue.ToConsoleString(), new ConsoleStringEqualityComparer(comparer, false));

    /// <summary>
    ///     Splits this ConsoleString into segments given a split value
    /// </summary>
    /// <param name="splitValue">the value to split on</param>
    /// <param name="comparer">optionally override the comparison function</param>
    /// <returns>A list of segments</returns>
    public List<ConsoleString> Split(ConsoleString splitValue, IEqualityComparer<ConsoleString>? comparer = null)
    {
        if (splitValue == null)
        {
            throw new ArgumentNullException($"{nameof(splitValue)} cannot be null");
        }

        comparer ??= ConsoleStringEqualityComparer.Default;
        var chunks = new List<ConsoleString>();
        var currentChunk = new List<ConsoleCharacter>();

        for (var i = 0; i < Length; i++)
        {
            var splitMatch = new List<ConsoleCharacter>();

            for (var j = i; i < Length && splitMatch.Count < splitValue.Length; j++)
                if (this[j].Value != splitValue[splitMatch.Count].Value)
                {
                    break;
                }
                else
                {
                    splitMatch.Add(this[j]);
                }

            if (comparer.Equals(new ConsoleString(splitMatch), splitValue))
            {
                if (currentChunk.Count > 0)
                {
                    chunks.Add(new ConsoleString(currentChunk));
                    currentChunk = new List<ConsoleCharacter>();
                }

                i += splitValue.Length - 1;
            }
            else
            {
                currentChunk.Add(this[i]);
            }
        }

        if (currentChunk.Count > 0)
        {
            chunks.Add(new ConsoleString(currentChunk));
        }

        return chunks;
    }

    /// <summary>
    ///     Converts this ConsoleString to a ConsoleBitmap. Tabs will be replaced with 4 spaces.
    /// </summary>
    /// <returns>
    ///     a ConsoleBitmap whose height equals the number of lines of text in the input string and whose width equals
    ///     that length of the longest line in the input string.
    /// </returns>
    public ConsoleBitmap ToConsoleBitmap()
    {
        var str = Replace("\r\n", "\n").Replace("\r", "\n").Replace("\t", "    ");
        var lines = str.Split("\n");
        var h = lines.Count;
        var w = lines.Select(l => l.Length).Max();
        var ret = new ConsoleBitmap(w, h);

        var x = 0;
        var y = 0;

        foreach (var c in str)
        {
            if (c.Value == '\n')
            {
                x = 0;
                y++;
            }
            else
            {
                ret.Pixels[x++, y] = c;
            }
        }

        return ret;
    }

    /// <summary>
    ///     Converts this ConsoleString into an html div tag that can be inserted into a web page and rendered by a web
    ///     browser.
    ///     The div will have:
    ///     a class attribute set to 'powerargs-console-string'
    ///     a style attribute set to font-family:Consolas and background-color set to the given background color
    ///     Each chunk of text that shares the same foreground and background color will be HTML encoded and then placed within
    ///     a span tag inside
    ///     the main div. Each span will have a style property with a value for color and background-color.
    ///     Newlines will be converted to br tags.
    /// </summary>
    /// <param name="divBackground">
    ///     optionally set the background color of the div, defaults to the current Console's default
    ///     background color
    /// </param>
    /// <param name="indent">if true, the spans will be indented inside the parent div</param>
    /// <param name="htmlEncode">if true, the function will html encode content before inserting markup</param>
    /// <returns>a string that is a valid html div. Null inputs will result in a null return value. Empty string wi</returns>
    public string ToHtmlDiv(RGB? divBackground = null, bool indent = false, bool htmlEncode = true)
    {
        var backgroundColor = divBackground ?? DefaultBackgroundColor;
        var str = Replace("\r\n", "\n").Replace("\r", "\n");

        var ret = new StringBuilder();

        ret.Append(
            $"<div class='powerargs-console-string' style='font-family:Consolas;background-color:{CSSMap[backgroundColor]}'>");

        if (indent)
        {
            ret.Append('\n');
        }

        foreach (var chunk in str.GroupByFormatting())
        {
            var fg = CSSMap[chunk[0].ForegroundColor];
            var bg = CSSMap[chunk[0].BackgroundColor];

            var encodedChunkString =
                (htmlEncode
                    ? WebUtility.HtmlEncode(new string(chunk.Select(c => c.Value).ToArray()))
                    : new string(chunk.Select(c => c.Value).ToArray())).Replace("\n", "<br/>");

            if (indent)
            {
                ret.Append("  ");
            }

            ret.Append($"<span style='color:{fg};background-color:{bg};'>{encodedChunkString}</span>");

            if (indent)
            {
                ret.Append("\n");
            }
        }

        ret.Append("</div>");

        return ret.ToString();
    }

    /// <summary>
    ///     Splits this string into groups where each character in each group has the same foreground and background color
    /// </summary>
    /// <returns>groups where each character in each group has the same foreground and background color</returns>
    public List<List<ConsoleCharacter>> GroupByFormatting()
    {
        var ret = new List<List<ConsoleCharacter>>();

        if (Length == 0) return ret;

        var lastFg = this[0].ForegroundColor;
        var lastBg = this[0].BackgroundColor;

        var currentChunk = new List<ConsoleCharacter>();
        ret.Add(currentChunk);
        foreach (var c in this)
        {
            if (c.ForegroundColor != lastFg || c.BackgroundColor != lastBg)
            {
                currentChunk = new List<ConsoleCharacter> { c };
                ret.Add(currentChunk);
                lastFg = c.ForegroundColor;
                lastBg = c.BackgroundColor;
            }
            else
            {
                currentChunk.Add(c);
            }
        }

        return ret;
    }

    /// <summary>
    ///     Compare this ConsoleString to another ConsoleString or a plain string.
    /// </summary>
    /// <param name="obj">The ConsoleString or plain string to compare to.</param>
    /// <returns>True if equal, false otherwise</returns>
    public override bool Equals(object? obj) =>
        obj switch {
            string s         => Equals(s.ToConsoleString()!),
            ConsoleString cs => Equals(cs),
            null             => false,
            _                => false
        };

    public bool Equals(ConsoleString other) => ConsoleStringEqualityComparer.StyleGnostic.Equals(other, this);

    /// <summary>
    ///     Gets the hashcode of the underlying string
    /// </summary>
    /// <returns>the hashcode of the underlying string</returns>
    public override int GetHashCode() => ToString().GetHashCode();

    /// <summary>
    ///     Operator overload that concatenates 2 ConsoleString instances and returns a new one.
    /// </summary>
    /// <param name="a">The left operand</param>
    /// <param name="b">The right operand</param>
    /// <returns>A new, concatenated ConsoleString</returns>
    public static ConsoleString? operator +(ConsoleString? a, ConsoleString? b)
    {
        if (a == null && b == null)
            return null;

        if (a == null || a.Length == 0)
            return new ConsoleString(b!.ToArray());

        if (b == null || b.Length == 0)
            return new ConsoleString(a.ToArray());

        var ret = a.ImmutableAppend(b);
        return ret;
    }

    /// <summary>
    ///     Operator overload that concatenates a ConsoleString with a string and returns a new one.
    /// </summary>
    /// <param name="a">The left operand</param>
    /// <param name="b">The right operand</param>
    /// <returns>A new, concatenated ConsoleString</returns>
    public static ConsoleString? operator +(ConsoleString? a, string? b)
    {
        if (a == null && b == null)
            return null;

        if ((a == null || a.Length == 0) && b != null)
            return new ConsoleString(b.Select(x => new ConsoleCharacter(x)).ToArray());

        if (string.IsNullOrEmpty(b) && a != null)
            return new ConsoleString(a.ToArray());

        var ret = a!.ImmutableAppend(b!);
        return ret;
    }

    /// <summary>
    ///     Compares 2 ConsoleStrings for equality.
    /// </summary>
    /// <param name="a">The left operand</param>
    /// <param name="b">The right operand</param>
    /// <returns>True if they are the same, false otherwise</returns>
    public static bool operator ==(ConsoleString? a, ConsoleString? b) =>
        (a, b) switch {
            (null, null) => true,
            (null, _)    => false,
            (_, null)    => false,
            (_, _)       => a.Equals(b)
        };

    /// <summary>
    ///     Compares 2 ConsoleStrings for inequality.
    /// </summary>
    /// <param name="a">The left operand</param>
    /// <param name="b">The right operand</param>
    /// <returns>False if they are the same, true otherwise</returns>
    public static bool operator !=(ConsoleString? a, ConsoleString? b) =>
        (a, b) switch {
            (null, null) => false,
            (null, _)    => true,
            (_, null)    => true,
            (_, _)       => !a.Equals(b)
        };
        
    /// <summary>
    ///     Changes the foreground of this string to black, and optionally forces the background of all characters to the given
    ///     color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToBlack(RGB? bg = null, bool underlined = false) => 
        To(ConsoleColor.Black, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to dark blue, and optionally forces the background of all characters to the
    ///     given color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToDarkBlue(RGB? bg = null, bool underlined = false) =>
        To(ConsoleColor.DarkBlue, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to dark green, and optionally forces the background of all characters to the
    ///     given color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToDarkGreen(RGB? bg = null, bool underlined = false) =>
        To(ConsoleColor.DarkGreen, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to dark cyan, and optionally forces the background of all characters to the
    ///     given color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToDarkCyan(RGB? bg = null, bool underlined = false) =>
        To(ConsoleColor.DarkCyan, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to dark red, and optionally forces the background of all characters to the
    ///     given color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToDarkRed(RGB? bg = null, bool underlined = false) =>
        To(ConsoleColor.DarkRed, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to dark magenta, and optionally forces the background of all characters to
    ///     the given color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToDarkMagenta(RGB? bg = null, bool underlined = false) =>
        To(ConsoleColor.DarkMagenta, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to dark yellow, and optionally forces the background of all characters to the
    ///     given color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToDarkYellow(RGB? bg = null, bool underlined = false) =>
        To(ConsoleColor.DarkYellow, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to gray, and optionally forces the background of all characters to the given
    ///     color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToGray(RGB? bg = null, bool underlined = false) => To(ConsoleColor.Gray, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to dark gray, and optionally forces the background of all characters to the
    ///     given color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToDarkGray(RGB? bg = null, bool underlined = false) =>
        To(ConsoleColor.DarkGray, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to blue, and optionally forces the background of all characters to the given
    ///     color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToBlue(RGB? bg = null, bool underlined = false) => To(ConsoleColor.Blue, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to green, and optionally forces the background of all characters to the given
    ///     color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToGreen(RGB? bg = null, bool underlined = false) => To(ConsoleColor.Green, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to cyan, and optionally forces the background of all characters to the given
    ///     color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToCyan(RGB? bg = null, bool underlined = false) => To(ConsoleColor.Cyan, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to red, and optionally forces the background of all characters to the given
    ///     color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToRed(RGB? bg = null, bool underlined = false) => To(ConsoleColor.Red, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to magenta, and optionally forces the background of all characters to the
    ///     given color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToMagenta(RGB? bg = null, bool underlined = false) =>
        To(ConsoleColor.Magenta, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to yellow, and optionally forces the background of all characters to the
    ///     given color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToYellow(RGB? bg = null, bool underlined = false) => To(ConsoleColor.Yellow, bg, underlined);

    /// <summary>
    ///     Changes the foreground of this string to white, and optionally forces the background of all characters to the given
    ///     color.
    /// </summary>
    /// <param name="bg">
    ///     The new background color for all characters or null to preserve each character's current background
    ///     color
    /// </param>
    /// <returns>a new ConsoleString with the desired color changes</returns>
    public ConsoleString ToWhite(RGB? bg = null, bool underlined = false) => To(ConsoleColor.White, bg, underlined);

    private ConsoleString To(RGB color, RGB? bg, bool underlined)
    {
        return new ConsoleString(
            this.Select(c => new ConsoleCharacter(c.Value, color, bg ?? c.BackgroundColor, underlined)).ToArray());
    }

    private ConsoleString ImmutableAppend(string value, RGB? foregroundColor = null, RGB? backgroundColor = null)
    {
        return new ConsoleString(
            this.Select(x => new ConsoleCharacter(x.Value, x.ForegroundColor, x.BackgroundColor, x.IsUnderlined))
                .ToArray()
                .Concat(value.Select(x => new ConsoleCharacter(x, foregroundColor, backgroundColor)).ToArray())
                .ToArray());
    }

    private ConsoleString ImmutableAppend(ConsoleString other)
    {
        return ConsoleStringEqualityComparer.StyleGnostic.Equals(other, Empty)
            ? new ConsoleString(this.Select(x => x).ToArray()) 
            : new ConsoleString(
                this.Select(x => new ConsoleCharacter(x.Value, x.ForegroundColor, x.BackgroundColor, x.IsUnderlined))
                    .ToArray()
                    .Concat(other.Select(x => x))
                    .ToArray());
    }
}

/// <summary>
///     Extensions that make it easy to work with ConsoleStrings
/// </summary>
public static class ConsoleStringX
{
    /// <summary>
    ///     Converts the given enumeration of console characters to a console string
    /// </summary>
    /// <param name="buffer">the characters to convert to a console string</param>
    /// <returns>the new console string</returns>
    public static ConsoleString ToConsoleString(this IEnumerable<ConsoleCharacter> buffer) => new(buffer);

    /// <summary>
    ///     Converts the given enumeration of console characters to a normal string
    /// </summary>
    /// <param name="buffer">the characters to convert to a normal string</param>
    /// <returns>the new string</returns>
    public static string ToNormalString(this IEnumerable<ConsoleCharacter> buffer) =>
        buffer.ToConsoleString().ToString();
}

/// <summary>
///     An equality comparer for ConsoleString objects
/// </summary>
public class ConsoleStringEqualityComparer : IEqualityComparer<ConsoleString>, IEqualityComparer<string>
{
    /// <summary>
    ///     The default equality comparer that will require styles to be equal and will simply call string.Equals for string
    ///     equality
    /// </summary>
    public static readonly ConsoleStringEqualityComparer Default = new();

    /// <summary>
    ///     A style agnostic version of the equality comparer
    /// </summary>
    public static readonly IEqualityComparer<ConsoleString> StyleAgnostic = new ConsoleStringEqualityComparer(requireStylesToBeEqual: false);

    public static readonly IEqualityComparer<ConsoleString> StyleGnostic = new ConsoleStringEqualityComparer(requireStylesToBeEqual: true);

    /// <summary>
    ///     Creates a new console string equality comparer.
    /// </summary>
    /// <param name="valueComparer">
    ///     the comparer to use to compare the characters in the string or null to use a default that
    ///     will use the string.Equals method
    /// </param>
    /// <param name="requireStylesToBeEqual">
    ///     If true, the comparer will require the two strings to have the same foreground and
    ///     background colors for each character. If false only the characters will be compared.
    /// </param>
    public ConsoleStringEqualityComparer(IEqualityComparer<string>? valueComparer = null, bool requireStylesToBeEqual = true)
    {
        ValueComparer = valueComparer ?? this;
        RequireStylesToBeEqual = requireStylesToBeEqual;
    }

    /// <summary>
    ///     Gets the string equality comparer used for string evaluations
    /// </summary>
    public IEqualityComparer<string> ValueComparer { get; }

    /// <summary>
    ///     Gets a boolean that indicates whether or not this comparer requires the foreground and background color to match in
    ///     order to be equal
    /// </summary>
    public bool RequireStylesToBeEqual { get; }

    /// <summary>
    ///     Compares the two ConsoleString objects for equality
    /// </summary>
    /// <param name="a">the first string</param>
    /// <param name="b">the second string</param>
    /// <returns>true if equal, false otherwise</returns>
    bool IEqualityComparer<ConsoleString>.Equals(ConsoleString? a, ConsoleString? b)
    {
        if (a == null && b == null)
            return true;

        var (x, y) = (a ?? ConsoleString.Empty, b ?? ConsoleString.Empty);
        var valueEqual = x.Length == y.Length && ValueComparer.Equals(x.StringValue, y.StringValue);
        if (valueEqual == false) return false;

        if (RequireStylesToBeEqual == false) return true;

        for (var i = 0; i < x.Length; i++)
        {
            if (x[i].ForegroundColor != y[i].ForegroundColor || x[i].BackgroundColor != y[i].BackgroundColor)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Gets the hash code for the given string
    /// </summary>
    /// <param name="obj">the string to hash</param>
    /// <returns>a hash code for the given string</returns>
    public int GetHashCode(ConsoleString obj) => obj.GetHashCode();

    /// <summary>
    ///     Compares the given strings for equality
    /// </summary>
    /// <param name="x">the first string</param>
    /// <param name="y">the second string</param>
    /// <returns></returns>
    bool IEqualityComparer<string>.Equals(string? x, string? y) => (x ?? string.Empty).Equals(y ?? string.Empty);

    /// <summary>
    ///     Gets the hash code for the given string
    /// </summary>
    /// <param name="obj">the string to hash</param>
    /// <returns>a hash code for the given string</returns>
    public int GetHashCode(string? obj) => ValueComparer.GetHashCode(obj ?? string.Empty);
}