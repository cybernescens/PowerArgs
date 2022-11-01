namespace PowerArgs.Cli;

/// <summary>
///     A control that lets the user provide text input
/// </summary>
public class TextBox : ConsoleControl
{
    private static readonly TimeSpan BlinkInterval = TimeSpan.FromMilliseconds(500);
    private bool blinkState;

    private SetIntervalHandle blinkTimerHandle;

    /// <summary>
    ///     Creates a new text box
    /// </summary>
    public TextBox()
    {
        RichTextEditor = new RichTextEditor();
        Height = 1;
        Width = 15;
        CanFocus = true;
        Focused.SubscribeForLifetime(this, TextBox_Focused);
        Unfocused.SubscribeForLifetime(this, TextBox_Unfocused);
        RichTextEditor.SubscribeForLifetime(this, nameof(RichTextEditor.CurrentValue), TextValueChanged);
        KeyInputReceived.SubscribeForLifetime(this, OnKeyInputReceived);
    }

    /// <summary>
    ///     Gets the editor object that controls the rich text capabilities of the text box
    /// </summary>
    public RichTextEditor RichTextEditor { get; }

    /// <summary>
    ///     Gets or sets the value in the text box
    /// </summary>
    public ConsoleString Value
    {
        get => RichTextEditor.CurrentValue ?? ConsoleString.Empty;
        set {
            RichTextEditor.CurrentValue = value;
            RichTextEditor.CursorPosition = value.Length;
        }
    }

    /// <summary>
    ///     Gets or sets a flag that enables or disables the blinking cursor that appears when the text box has focus
    /// </summary>
    public bool BlinkEnabled { get; set; } = true;

    public bool IsInputBlocked { get; set; }

    private void TextValueChanged() { FirePropertyChanged(nameof(Value)); }

    private void TextBox_Focused()
    {
        blinkState = true;
        blinkTimerHandle = Application.SetInterval(
            () => {
                if (HasFocus == false) return;

                blinkState = !blinkState;
                Application.RequestPaint();
            },
            BlinkInterval);
    }

    private void TextBox_Unfocused()
    {
        blinkTimerHandle.Dispose();
        blinkState = false;
    }

    private void OnKeyInputReceived(ConsoleKeyInfo info)
    {
        if (IsInputBlocked) return;

        ConsoleCharacter? prototype = Value.Length == 0 ? null : Value[Value.Length - 1];
        RichTextEditor.RegisterKeyPress(info, prototype);
        blinkState = true;
        Application.ChangeInterval(blinkTimerHandle, BlinkInterval);
    }

    /// <summary>
    ///     paints the text box
    /// </summary>
    /// <param name="context"></param>
    protected override void OnPaint(ConsoleBitmap context)
    {
        var toPaint = RichTextEditor.CurrentValue;

        var offset = 0;
        if (toPaint.Length >= Width && RichTextEditor.CursorPosition > Width - 1)
        {
            offset = RichTextEditor.CursorPosition + 1 - Width;
            toPaint = toPaint.Substring(offset);
        }

        var bgTransformed = new List<ConsoleCharacter>();

        foreach (var c in toPaint)
            if (c.BackgroundColor == ConsoleString.DefaultBackgroundColor &&
                Background != ConsoleString.DefaultBackgroundColor)
            {
                bgTransformed.Add(new ConsoleCharacter(c.Value, Foreground, Background));
            }
            else
            {
                bgTransformed.Add(c);
            }

        context.DrawString(new ConsoleString(bgTransformed), 0, 0);

        if (blinkState && BlinkEnabled)
        {
            var blinkChar = RichTextEditor.CursorPosition >= toPaint.Length
                ? ' '
                : toPaint[RichTextEditor.CursorPosition].Value;

            var pen = new ConsoleCharacter(blinkChar, DefaultColors.FocusContrastColor, DefaultColors.FocusColor);
            context.DrawPoint(pen, RichTextEditor.CursorPosition - offset, 0);
        }
    }
}