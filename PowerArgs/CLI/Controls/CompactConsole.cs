﻿namespace PowerArgs.Cli;

public abstract class CompactConsole : ConsolePanel
{
    private CommandLineArgumentsDefinition def;
    private Lifetime? focusLt;
    private Label? outputLabel;

    private Lifetime? refreshLt = new();

    public CompactConsole()
    {
        SubscribeForLifetime(this, nameof(Bounds), () => HardRefresh());
        Ready.SubscribeOnce(
            async () => {
                await Task.Yield();
                HardRefresh();
            });
    }

    public bool IsAssistanceEnabled { get; set; } = true;
    public TextBox? InputBox { get; private set; }
    public ConsoleString? WelcomeMessage { get; set; } = "Welcome to the console".ToWhite();
    public ConsoleString? EscapeMessage { get; set; } = "Press escape to resume".ToGray();

    public bool SuperCompact { get; set; }

    protected abstract CommandLineArgumentsDefinition CreateDefinition();
    protected virtual bool HasHistory() => false;
    protected virtual void AddHistory(string? history) { }
    protected virtual ConsoleString GetHistoryPrevious() => throw new NotImplementedException();
    protected virtual ConsoleString GetHistoryNext() => throw new NotImplementedException();

    protected virtual void OnInputBoxReady() { }

    protected virtual Task Run(ArgAction toRun)
    {
        toRun.Invoke();
        SetOutput(null);
        return Task.CompletedTask;
    }

    public void HardRefresh(ConsoleString? outputValue = null)
    {
        refreshLt?.Dispose();
        refreshLt = new Lifetime();
        var myLt = refreshLt;
        Controls.Clear();

        var minHeight = SuperCompact ? 1 : 5;

        if (Width < 10 || Height < minHeight) return;

        def = CreateDefinition();

        var options = new GridLayoutOptions {
            Columns = new List<GridColumnDefinition> {
                new() { Type = GridValueType.Pixels, Width = 2 },         // 0 empty
                new() { Type = GridValueType.RemainderValue, Width = 1 }, // 1 content
                new() { Type = GridValueType.Pixels, Width = 2 }          // 2 empty
            },
            Rows = new List<GridRowDefinition> {
                new() { Type = GridValueType.Pixels, Height = 1 },         // 0 empty
                new() { Type = GridValueType.Pixels, Height = 1 },         // 1 welcome message
                new() { Type = GridValueType.Pixels, Height = 1 },         // 2 press escape message
                new() { Type = GridValueType.Pixels, Height = 1 },         // 3 empty
                new() { Type = GridValueType.Pixels, Height = 1 },         // 4 input
                new() { Type = GridValueType.Pixels, Height = 1 },         // 5 empty
                new() { Type = GridValueType.RemainderValue, Height = 1 }, // 6 output
                new() { Type = GridValueType.Pixels, Height = 1 }          // 7 empty
            }
        };

        if (SuperCompact)
        {
            options.Rows.RemoveAt(0);                      // empty
            options.Rows.RemoveAt(0);                      // welcome
            options.Rows.RemoveAt(0);                      // press escape
            options.Rows.RemoveAt(0);                      // empty
            options.Rows.RemoveAt(options.Rows.Count - 1); // empty
            options.Rows.RemoveAt(options.Rows.Count - 1); // output
            options.Rows.RemoveAt(options.Rows.Count - 1); // empty
        }

        var gridLayout = Add(new GridLayout(options));

        gridLayout.Fill();
        gridLayout.RefreshLayout();

        var top = SuperCompact ? 0 : 1;

        if (SuperCompact == false)
        {
            var welcomePanel = gridLayout.Add(new ConsolePanel(), 1, top++);
            welcomePanel.Add(new Label { Text = WelcomeMessage }).CenterHorizontally();

            var escapePanel = gridLayout.Add(new ConsolePanel(), 1, top++);
            escapePanel.Add(new Label { Text = EscapeMessage }).CenterHorizontally();

            top++;
        }

        var inputPanel = gridLayout.Add(new ConsolePanel(), 1, top++);
        inputPanel.Add(new Label { Text = "CMD> ".ToConsoleString() });
        InputBox = inputPanel.Add(
            new TextBox {
                X = "CMD> ".Length, Width = inputPanel.Width - "CMD> ".Length, Foreground = ConsoleColor.Gray,
                Background = ConsoleColor.Black
            });

        InputBox.RichTextEditor.TabHandler.TabCompletionHandlers.Add(
            new PowerArgsRichCommandLineReader(def, new List<ConsoleString>(), false));

        OnInputBoxReady();
        top++;
        Application.InvokeNextCycle(
            () => {
                if (myLt == refreshLt)
                {
                    InputBox.Focused.SubscribeForLifetime(
                        refreshLt,
                        () => {
                            if (focusLt != null && focusLt.IsExpired == false && focusLt.IsExpiring == false)
                            {
                                focusLt.Dispose();
                            }

                            focusLt = new Lifetime();

                            Application.FocusManager.GlobalKeyHandlers.PushForLifetime(
                                ConsoleKey.Tab,
                                null,
                                () => {
                                    var forgotten = OnHandleHey(
                                        new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));
                                },
                                focusLt);

                            Application.FocusManager.GlobalKeyHandlers.PushForLifetime(
                                ConsoleKey.Tab,
                                ConsoleModifiers.Shift,
                                () => {
                                    var forgotten = OnHandleHey(
                                        new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, false, false));
                                },
                                focusLt);
                        });

                    InputBox.Unfocused.SubscribeForLifetime(
                        refreshLt,
                        () => {
                            if (focusLt != null && focusLt.IsExpired == false && focusLt.IsExpiring == false)
                            {
                                focusLt.Dispose();
                            }
                        });

                    InputBox.TryFocus();
                }
            });

        if (SuperCompact == false)
        {
            var outputPanel = gridLayout.Add(new ConsolePanel { Background = ConsoleColor.Black }, 1, top);
            outputLabel = outputPanel.Add(
                new Label {
                    Text =
                        string.IsNullOrWhiteSpace(outputValue?.StringValue) == false
                            ? outputValue
                            : string.IsNullOrWhiteSpace(outputLabel?.Text.StringValue) == false
                                ? outputLabel?.Text
                                : CreateAssistiveText(),
                    Mode = LabelRenderMode.MultiLineSmartWrap
                }).Fill();
        }

        InputBox.KeyInputReceived.SubscribeForLifetime(InputBox, async keyInfo => await OnHandleHey(keyInfo));
    }

    private async Task OnHandleHey(ConsoleKeyInfo keyInfo)
    {
        if (InputBox.IsInputBlocked) return;

        OnKeyPress(keyInfo);
        if (keyInfo.Key == ConsoleKey.Enter)
        {
            var output = ConsoleString.Empty;
            try
            {
                var args = Args.Convert(InputBox.Value.ToString());
                AddHistory(InputBox.Value.ToString());

                if (def.ExceptionBehavior?.Policy == ArgExceptionPolicy.StandardExceptionHandling)
                {
                    def.ExceptionBehavior = new ArgExceptionBehavior();
                }

                ArgAction action;
                ConsoleOutInterceptor.Instance.Attach();
                try
                {
                    action = Args.ParseAction(def, args);
                }
                finally
                {
                    ConsoleOutInterceptor.Instance.Detach();
                }

                InputBox.Dispose();
                output = new ConsoleString(ConsoleOutInterceptor.Instance.ReadAndClear());

                if (action.Cancelled == false)
                {
                    var oldDef = Args.GetAmbientDefinition();
                    try
                    {
                        Args.SetAmbientDefinition(def);
                        await Run(action);
                    }
                    finally
                    {
                        Args.SetAmbientDefinition(oldDef);
                    }
                }
            }
            catch (Exception ex)
            {
                var inner = ex;
                if (ex is AggregateException && (ex as AggregateException).InnerExceptions.Count == 1)
                {
                    inner = ex.InnerException;
                }

                if (ex is ArgException == false)
                {
                    throw;
                }

                output = inner.Message.ToRed();
            }
            finally
            {
                if (IsExpired == false)
                {
                    HardRefresh(output);
                }
            }
        }
        else if (keyInfo.Key == ConsoleKey.Tab)
        {
            ConsoleCharacter? prototype = InputBox.Value.Length == 0 ? null : InputBox.Value[InputBox.Value.Length - 1];
            InputBox.RichTextEditor.RegisterKeyPress(keyInfo, prototype);
        }
        else if (keyInfo.Key == ConsoleKey.UpArrow)
        {
            if (HasHistory())
            {
                InputBox.Value = GetHistoryPrevious();
                SetOutput(CreateAssistiveText());
            }
        }
        else if (keyInfo.Key == ConsoleKey.DownArrow)
        {
            if (HasHistory())
            {
                InputBox.Value = GetHistoryNext();
                SetOutput(CreateAssistiveText());
            }
        }
        else if (RichTextCommandLineReader.IsWriteable(keyInfo))
        {
            SetOutput(CreateAssistiveText());
        }

        AfterKeyPress(keyInfo);
    }

    protected virtual void OnKeyPress(ConsoleKeyInfo info) { }
    protected virtual void AfterKeyPress(ConsoleKeyInfo info) { }

    private void SetOutput(ConsoleString? text)
    {
        if (outputLabel != null)
        {
            outputLabel.Text = text;
        }
    }

    public void Write(ConsoleString? text)
    {
        if (outputLabel != null)
        {
            outputLabel.Text += text;
        }
    }

    public void WriteLine(ConsoleString text) => Write(text + "\n");

    public void Clear()
    {
        if (outputLabel != null)
        {
            outputLabel.Text = ConsoleString.Empty;
        }
    }

    protected virtual ConsoleString? Parse(string? content) => ConsoleString.Parse(content);

    public ConsoleString? CreateAssistiveText()
    {
        if (IsAssistanceEnabled == false)
        {
            return ConsoleString.Empty;
        }

        var candidates = def.Actions.Where(a => a.Metadata.WhereAs<OmitFromUsageDocs>().None()).ToList();
        if (InputBox.Value.Length > 0)
        {
            var command = InputBox.Value.Split(" ".ToConsoleString()).FirstOrDefault();
            command = command ?? ConsoleString.Empty;
            candidates = candidates.Where(
                a => a.DefaultAlias.StartsWith(command.StringValue, StringComparison.OrdinalIgnoreCase)).ToList();

            if (candidates.Count == 0)
            {
                return $"\nNo actions start with {InputBox.Value}".ToRed();
            }
        }

        var builder = new ConsoleTableBuilder();

        var headers = new List<ConsoleString?> {
            "command".ToYellow(),
            "description".ToYellow(),
            "example".ToYellow()
        };

        var rows = new List<List<ConsoleString?>>();

        foreach (var candidate in candidates)
        {
            var row = new List<ConsoleString?>();
            rows.Add(row);
            row.Add(candidate.DefaultAlias.ToLower().ToCyan());
            row.Add(Parse(candidate.Description));
            row.Add(
                candidate.HasExamples == false ? ConsoleString.Empty : candidate.Examples.First().Example.ToGreen());

            if (candidates.Count == 1)
            {
                foreach (var arg in candidate.Arguments.Where(
                             a => a.Metadata.Where(m => m is OmitFromUsageDocs).None()))
                {
                    var argDescription = !arg.HasDefaultValue
                        ? ConsoleString.Empty
                        : Parse($"[DarkYellow]\\[Default: [Yellow]{arg.DefaultValue}[DarkYellow]] ");

                    argDescription += string.IsNullOrEmpty(arg.Description)
                        ? ConsoleString.Empty
                        : Parse(arg.Description);

                    argDescription += !arg.IsEnum
                        ? ConsoleString.Empty
                        : "values: ".ToYellow() + string.Join(", ", arg.EnumValuesAndDescriptions).ToYellow();

                    row = new List<ConsoleString?>();
                    rows.Add(row);
                    row.Add(
                        " -".ToWhite() +
                        arg.DefaultAlias.ToLower().ToWhite() +
                        (arg.IsRequired ? "*".ToRed() : ConsoleString.Empty));

                    row.Add(argDescription);
                    row.Add(ConsoleString.Empty);
                }
            }
        }

        return builder.FormatAsTable(headers, rows);
    }
}