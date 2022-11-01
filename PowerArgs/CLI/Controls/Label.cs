namespace PowerArgs.Cli;

/// <summary>
///     Determines how a label renders
/// </summary>
public enum LabelRenderMode
{
    /// <summary>
    ///     Render the text on a single line and auto size the width based on the text
    /// </summary>
    SingleLineAutoSize,

    /// <summary>
    ///     Render on multiple lines, breaking spaces and punctuation near the control's width.  Good for paragraph text.
    /// </summary>
    MultiLineSmartWrap,

    /// <summary>
    ///     Manually size the label, truncation can occur
    /// </summary>
    ManualSizing
}

/// <summary>
///     A control that displays text
/// </summary>
public class Label : ConsoleControl
{
    internal static readonly ConsoleString? Null = "<null>".ToConsoleString(DefaultColors.DisabledColor);

    private ConsoleString? _cleanCache;

    private LabelRenderMode _mode;

    private readonly List<List<ConsoleCharacter>> lines;

    /// <summary>
    ///     Creates a new label
    /// </summary>
    public Label()
    {
        Height = 1;
        Mode = LabelRenderMode.SingleLineAutoSize;
        CanFocus = false;
        lines = new List<List<ConsoleCharacter>>();

        SubscribeForLifetime(this, nameof(Text), HandleTextChanged);
        SubscribeForLifetime(this, nameof(Mode), HandleTextChanged);
        SubscribeForLifetime(this, nameof(MaxHeight), HandleTextChanged);
        SubscribeForLifetime(this, nameof(MaxWidth), HandleTextChanged);
        SynchronizeForLifetime(nameof(Bounds), HandleTextChanged, this);
        Text = ConsoleString.Empty;
    }

    /// <summary>
    ///     Gets or sets the text displayed on the label
    /// </summary>
    public ConsoleString Text
    {
        get => Get<ConsoleString>() ?? ConsoleString.Empty;
        set {
            _cleanCache = null;
            Set(value);
        }
    }

    /// <summary>
    ///     Gets or sets the max width.  This is only used in the single line auto size mode.
    /// </summary>
    public int? MaxWidth
    {
        get => Get<int?>();
        set => Set(value);
    }

    /// <summary>
    ///     Gets or sets the max height.  This is only used in the multi line smart wrap mode.
    /// </summary>
    public int? MaxHeight
    {
        get => Get<int?>();
        set => Set(value);
    }

    private ConsoleString? CleanText
    {
        get {
            if (Text == null) return Null;

            _cleanCache = _cleanCache ?? Text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\t", "    ");
            return _cleanCache;
        }
    }

    /// <summary>
    ///     Gets or sets the render mode
    /// </summary>
    public LabelRenderMode Mode
    {
        get => _mode;
        set => SetHardIf(ref _mode, value, () => value != _mode);
    }

    public static ConsolePanel CreatePanelWithCenteredLabel(ConsoleString str)
    {
        var ret = new ConsolePanel();
        ret.Add(new Label { Text = str }).CenterBoth();
        return ret;
    }

    private void HandleTextChanged()
    {
        lines.Clear();
        var clean = CleanText;
        if (Mode == LabelRenderMode.ManualSizing)
        {
            lines.Add(new List<ConsoleCharacter>());
            foreach (var c in clean)
                if (c.Value == '\n')
                {
                    lines.Add(new List<ConsoleCharacter>());
                }
                else
                {
                    lines.Last().Add(c);
                }
        }
        else if (Mode == LabelRenderMode.SingleLineAutoSize)
        {
            Height = 1;

            if (MaxWidth.HasValue)
            {
                Width = Math.Min(MaxWidth.Value, clean.Length);
            }
            else
            {
                Width = clean.Length;
            }

            lines.Add(clean.ToList());
        }
        else
        {
            DoSmartWrap();
        }
    }

    private void DoSmartWrap()
    {
        List<ConsoleCharacter> currentLine = null;

        var cleaned = CleanText;
        var cleanedString = cleaned.ToString();

        var tokenizer = new Tokenizer<Token>();
        tokenizer.Delimiters.Add(".");
        tokenizer.Delimiters.Add("?");
        tokenizer.Delimiters.Add("!");
        tokenizer.WhitespaceBehavior = WhitespaceBehavior.DelimitAndInclude;
        tokenizer.DoubleQuoteBehavior = DoubleQuoteBehavior.NoSpecialHandling;
        var tokens = tokenizer.Tokenize(cleanedString);

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (currentLine == null)
            {
                SmartWrapNewLine(lines, ref currentLine);
            }

            if (token.Value == "\n")
            {
                SmartWrapNewLine(lines, ref currentLine);
            }
            else if (currentLine.Count + token.Value.Length <= Width)
            {
                currentLine.AddRange(cleaned.Substring(token.StartIndex, token.Value.Length));
            }
            else
            {
                SmartWrapNewLine(lines, ref currentLine);

                var toAdd = cleaned.Substring(token.StartIndex, token.Value.Length).TrimStart();

                foreach (var c in toAdd)
                {
                    if (currentLine.Count == Width)
                    {
                        SmartWrapNewLine(lines, ref currentLine);
                    }

                    currentLine.Add(c);
                }
            }
        }

        if (MaxHeight.HasValue)
        {
            Height = Math.Min(lines.Count, MaxHeight.Value);
        }
        else
        {
            Height = lines.Count;
        }
    }

    private void SmartWrapNewLine(List<List<ConsoleCharacter>> lines, ref List<ConsoleCharacter> currentLine)
    {
        currentLine = new List<ConsoleCharacter>();
        lines.Add(currentLine);
    }

    protected override void OnPaint(ConsoleBitmap context)
    {
        for (var y = 0; y < lines.Count; y++)
        {
            if (y >= Height)
            {
                break;
            }

            var line = lines[y];

            for (var x = 0; x < line.Count && x < Width; x++)
            {
                var pen = HasFocus
                    ? new ConsoleCharacter(line[x].Value, DefaultColors.FocusContrastColor, DefaultColors.FocusColor)
                    : line[x];

                context.DrawPoint(pen, x, y);
            }
        }
    }
}