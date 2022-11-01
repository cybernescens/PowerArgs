namespace PowerArgs;

/// <summary>
///     A class that provides context for consumers of the RichTextCommandLineReader
/// </summary>
public class RichCommandLineContext
{
    private bool hasFreshTokens;

    internal RichCommandLineContext(ConsoleHistoryManager historyManager)
    {
        Buffer = new List<ConsoleCharacter>();
        HistoryManager = historyManager;

        Tokenizer = new Tokenizer<Token>();
        Tokenizer.DoubleQuoteBehavior = DoubleQuoteBehavior.IncludeQuotedTokensAsStringLiterals;
        Tokenizer.WhitespaceBehavior = WhitespaceBehavior.DelimitAndInclude;
        hasFreshTokens = false;
    }

    /// <summary>
    ///     For now this is here to support the mode when this context is used within a ConsoleApp.
    /// </summary>
    internal bool DisableConsoleRefresh { get; set; }
    /// <summary>
    ///     Gets the console implementation that's being used to read the input
    /// </summary>
    public IConsoleProvider Console { get; internal set; }

    /// <summary>
    ///     The current buffer of characters on the command line.  If you manipulate the buffer during tab completion or other
    ///     key handling
    ///     then you should call RefreshConsole() or ReplaceConsole() so that the updates get reflected.
    /// </summary>
    public List<ConsoleCharacter> Buffer { get; private set; }

    /// <summary>
    ///     The key that was last pressed and is currently being processed.
    /// </summary>
    public ConsoleKeyInfo KeyPressed { get; internal set; }

    /// <summary>
    ///     You should set this flag if your handler is goind to take care of updating the console manually.  If false, the
    ///     reader will
    ///     apply the keypress to the console automatically.
    /// </summary>
    public bool Intercept { get; set; }

    /// <summary>
    ///     Gets a reference to the history manager that contains previous command line values.
    /// </summary>
    internal ConsoleHistoryManager HistoryManager { get; }

    /// <summary>
    ///     Gets a reference to the character that is about to be written
    /// </summary>
    public ConsoleCharacter CharacterToWrite { get; internal set; }

    /// <summary>
    ///     You should set this to true if you want to commit the line being read.  Typically this is only set by the enter key
    ///     handler.
    /// </summary>
    public bool IsFinished { get; set; }

    /// <summary>
    ///     Gets the value of the cursor left position when the read operation began
    /// </summary>
    public int ConsoleStartLeft { get; internal set; }

    /// <summary>
    ///     Gets the value of the cursor top position when the read operation began
    /// </summary>
    public int ConsoleStartTop { get; internal set; }

    /// <summary>
    ///     Gets the cursor position, relative to the buffer as opposed to the absolute left and right positions within the
    ///     console.
    /// </summary>
    public int BufferPosition { get; set; }

    /// <summary>
    ///     Gets the tokens that were last parsed.  This is not always populated for you since not all key handlers require
    ///     tokenizing
    ///     the input.  If you need this in your handler then first call RefreshTokenInfo().
    /// </summary>
    public List<Token> Tokens { get; private set; }

    /// <summary>
    ///     Gets the tokenizer used to tokenize command line input.  By default it knows how to handle string literals and
    ///     escape
    ///     sequences that are appropriate for a command line.
    /// </summary>
    public Tokenizer<Token> Tokenizer { get; }

    /// <summary>
    ///     Gets the token that maps to the current BufferPosition. This is not always populated for you since not all key
    ///     handlers require tokenizing
    ///     the input.  If you need this in your handler then first call RefreshTokenInfo().
    /// </summary>
    public Token? CurrentToken { get; private set; }

    /// <summary>
    ///     Gets the index of the current token within the list of tokens.  This is not always populated for you since not all
    ///     key handlers require tokenizing
    ///     the input.  If you need this in your handler then first call RefreshTokenInfo().
    /// </summary>
    public int CurrentTokenIndex { get; private set; }

    /// <summary>
    ///     Gets the non whitespace token that comes immediately before the current token, or null if there is no previous non
    ///     whitespace token.  This is not always populated for you since not all key handlers require tokenizing
    ///     the input.  If you need this in your handler then first call RefreshTokenInfo().
    /// </summary>
    public Token? PreviousNonWhitespaceToken
    {
        get {
            Token? ret = null;
            for (var i = CurrentTokenIndex - 1; i >= 0; i--)
            {
                var token = Tokens[i];
                if (string.IsNullOrWhiteSpace(token.Value) == false)
                {
                    ret = token;
                    break;
                }
            }

            return ret;
        }
    }

    internal void Reset()
    {
        Intercept = false;
        IsFinished = false;
        KeyPressed = default;
        hasFreshTokens = false;
    }

    /// <summary>
    ///     Returns the portion of the buffer represented by the given token
    /// </summary>
    /// <param name="t">the token whose position to use to look into the buffer</param>
    /// <returns>the portion of the buffer represented by the given token</returns>
    public ConsoleString GetBufferSubstringFromToken(Token t)
    {
        var buffer = new List<ConsoleCharacter>();
        for (var i = t.StartIndex; i < t.EndIndex; i++)
            buffer.Add(Buffer[i]);

        return new ConsoleString(buffer);
    }

    /// <summary>
    ///     Clears the console
    /// </summary>
    public void ClearConsole()
    {
        Console.CursorLeft = ConsoleStartLeft;
        Console.CursorTop = ConsoleStartTop;

        for (var i = 0; i < Buffer.Count; i++)
            Console.Write(" ");
    }

    /// <summary>
    ///     Rewrites the console using the latest values in the Buffer, preserving the cursor position with an optional
    ///     adjustment.
    /// </summary>
    /// <param name="leftAdjust">
    ///     Adjusts the left cursor position by the desired amound.  If you want the cursor to stay where
    ///     it was then use 0.
    /// </param>
    /// <param name="topAdjust">
    ///     Adjusts the top cursor position by the desired amound.  If you want the cursor to stay where it
    ///     was then use 0.
    /// </param>
    public void RefreshConsole(int leftAdjust, int topAdjust)
    {
        if (DisableConsoleRefresh)
        {
            return;
        }

        var left = Console.CursorLeft;
        var top = Console.CursorTop;
        Console.CursorLeft = ConsoleStartLeft;
        Console.CursorTop = ConsoleStartTop;
        for (var i = 0; i < Buffer.Count; i++)
            Console.Write(Buffer[i]);

        Console.Write(" ");
        Console.Write(" ");

        var desiredLeft = left + leftAdjust;

        if (desiredLeft == Console.BufferWidth)
        {
            Console.CursorLeft = top == ConsoleStartTop ? ConsoleStartLeft : 0;
            Console.CursorTop = top + 1;
        }
        else if (desiredLeft == -1)
        {
            Console.CursorLeft = Console.BufferWidth - 1;
            Console.CursorTop = top - 1;
        }
        else
        {
            Console.CursorLeft = desiredLeft;
            Console.CursorTop = top;
        }
    }

    /// <summary>
    ///     Rewrites the console using the latest values in the Buffer and moves the cursor to the end of the line.
    /// </summary>
    /// <param name="newBuffer">The new line of text that will replace the current buffer.</param>
    public void ReplaceConsole(ConsoleString newBuffer)
    {
        Console.CursorLeft = ConsoleStartLeft;
        Console.CursorTop = ConsoleStartTop;
        for (var i = 0; i < newBuffer.Length; i++)
            Console.Write(newBuffer[i]);

        var newLeft = Console.CursorLeft;
        var newTop = Console.CursorTop;

        for (var i = 0; i < Buffer.Count - newBuffer.Length; i++)
            Console.Write(" ");

        Console.CursorTop = newTop;
        Console.CursorLeft = newLeft;
        Buffer = newBuffer.ToList();
    }

    /// <summary>
    ///     Runs the tokenizer if it hasn't already been run on the current key press.  You can pass a force flag if you want
    ///     to
    ///     force the tokenizer to run.  You would need to do this only if you've manually changed the buffer within your
    ///     handler.
    /// </summary>
    /// <param name="force">
    ///     If true, then the tokenizer is run no matter what.  If false, the tokenizer only runs if it hasn't
    ///     yet run on this keystroke.
    /// </param>
    public void RefreshTokenInfo(bool force = false)
    {
        if (hasFreshTokens && force == false)
        {
            return;
        }

        Tokens = Tokenizer.Tokenize(new ConsoleString(Buffer).ToString());

        if (Tokens.Count == 0)
        {
            Tokens.Add(new Token(0, 1, 1));
        }

        CurrentToken = null;
        for (var i = 0; i < Tokens.Count; i++)
        {
            var token = Tokens[i];
            if (BufferPosition < token.EndIndex && BufferPosition >= token.StartIndex)
            {
                // BUFFER---------: a command line string
                // BUFFER POSITION:   [       ]
                CurrentToken = token;
                CurrentTokenIndex = i;
                break;
            }
        }

        if (CurrentToken == null)
        {
            CurrentToken = Tokens[^1];
            CurrentTokenIndex = Tokens.Count - 1;
        }
    }

    /// <summary>
    ///     Takes the value of KeyPressed.KeyChar and writes it to the console in the current buffer position.
    /// </summary>
    public void WriteCharacterForPressedKey()
    {
        var c = new ConsoleCharacter(KeyPressed.KeyChar);
        if (BufferPosition == Buffer.Count)
        {
            Buffer.Add(c);
            Console.Write(c);
        }
        else
        {
            Buffer.Insert(BufferPosition, c);
            RefreshConsole(1, 0);
        }
    }

    /// <summary>
    ///     Performs an auto complete of the given token.
    /// </summary>
    /// <param name="currentToken">the token to complete</param>
    /// <param name="completion">
    ///     the completed token.  Note that it is not required that the completion string starts with the
    ///     current token value, though it usually does.
    /// </param>
    public void CompleteCurrentToken(Token currentToken, ConsoleString completion)
    {
        var quoteStatus = GetQuoteStatus(Buffer, BufferPosition - 1);
        var readyToEnd = quoteStatus != QuoteStatus.ClosedQuote;
        var endTarget = quoteStatus == QuoteStatus.NoQuotes ? ' ' : '"';

        int currentTokenStartIndex;
        for (currentTokenStartIndex = BufferPosition - 1; currentTokenStartIndex >= 0; currentTokenStartIndex--)
        {
            if (Buffer[currentTokenStartIndex].Value == endTarget && readyToEnd)
            {
                if (endTarget == ' ')
                {
                    currentTokenStartIndex++;
                }

                break;
            }
            else if (Buffer[currentTokenStartIndex].Value == endTarget)
            {
                readyToEnd = true;
            }
        }

        if (currentTokenStartIndex == -1) currentTokenStartIndex = 0;

        var insertThreshold = currentTokenStartIndex + currentToken.Value.Length;

        var newBuffer = new List<ConsoleCharacter>(Buffer);
        for (var completionIndex = 0; completionIndex < completion.Length; completionIndex++)
        {
            if (completionIndex + currentTokenStartIndex == newBuffer.Count)
            {
                newBuffer.Add(completion[completionIndex]);
            }
            else if (completionIndex + currentTokenStartIndex < insertThreshold)
            {
                newBuffer[completionIndex + currentTokenStartIndex] = completion[completionIndex];
            }
            else
            {
                newBuffer.Insert(completionIndex + currentTokenStartIndex, completion[completionIndex]);
            }
        }

        while (newBuffer.Count > currentTokenStartIndex + completion.Length)
        {
            newBuffer.RemoveAt(currentTokenStartIndex + completion.Length);
        }

        ReplaceConsole(new ConsoleString(newBuffer));
    }

    internal bool IsCursorOnToken(Token t)
    {
        if (Buffer.Count == 0 && t.StartIndex == 0 && t.EndIndex == 0)
        {
            // the buffer is empty, return true if we have an empty token
            return true;
        }

        if (Buffer.Count == 0)
        {
            // the buffer is empty and the given token is not, throw
            throw new ArgumentException("The given token does not appear to be a part of the buffer");
        }

        // never say that the cursor is on a whitespace token - not sure about this :(
        if (string.IsNullOrWhiteSpace(t.Value)) return false;

        // the cursor is at a point in the buffer before the current token
        if (BufferPosition < t.StartIndex) return false;

        // the cursor is pointing at a specific character within the current token
        if (BufferPosition <= t.EndIndex) return true;

        return false;
    }

    private QuoteStatus GetQuoteStatus(List<ConsoleCharacter> chars, int startPosition)
    {
        var open = false;

        for (var i = 0; i <= startPosition; i++)
        {
            var c = chars[i];
            if (i > 0 && c == '"' && chars[i - 1] == '\\')
            {
                // escaped
            }
            else if (c == '"')
            {
                open = !open;
            }
        }

        if (open) 
            return QuoteStatus.OpenedQuote;

        if (chars.LastIndexOf(new ConsoleCharacter('"')) > chars.LastIndexOf(new ConsoleCharacter(' ')))
            return QuoteStatus.ClosedQuote;

        return QuoteStatus.NoQuotes;
    }

    private enum QuoteStatus
    {
        OpenedQuote,
        ClosedQuote,
        NoQuotes
    }
}