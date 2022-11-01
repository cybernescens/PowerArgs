namespace PowerArgs.Cli;

public struct Size
{
    public static readonly Size Zero = new(0, 0);

    private static readonly Tokenizer<Token> tokenizer = new() {
        EscapeSequenceIndicator = null,
        WhitespaceBehavior = WhitespaceBehavior.Include,
        Delimiters = new List<string> { "," }
    };

    public int Width { get; }
    public int Height { get; }

    public Size(int w = 0, int h = 0)
    {
        Width = w;
        Height = h;
    }

    public Size(float w = 0f, float h = 0f)
    {
        Width = Convert.ToInt32(w);
        Height = Convert.ToInt32(h);
    }

    public static Size Deserialize(string serialized)
    {
        var reader = new TokenReader<Token>(tokenizer.Tokenize(serialized));
        var tokens = new Token[2];

        tokens[0] = reader.Advance();
        reader.Expect(",");
        tokens[1] = reader.Advance();

        if (!int.TryParse(tokens[0].Value, out var x))
        {
            throw new FormatException("Could not determine width");
        }

        if (!int.TryParse(tokens[1].Value, out var y))
        {
            throw new FormatException("Could not determine height");
        }

        return new Size(x, y);
    }

    public override string ToString() => $"{Width},{Height}";
}