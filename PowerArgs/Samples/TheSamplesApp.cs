using PowerArgs.Cli;
using PowerArgs.Cli.Physics;

namespace PowerArgs.Samples;

public class TheSamplesApp : ConsoleApp
{
    public const string MenuTag = "TheSamplesAppMenuItem";
    private ConsoleControl? currentContent;

    private Button? currentNavButton;
    private GridLayout? layout;

    public TheSamplesApp() { InvokeNextCycle(Init); }

    private void Init()
    {
        InitLayout();
        InitSplitter();
        InitMenu();
        InitNavKeys();
    }

    private void InitLayout()
    {
        layout = LayoutRoot.Add(
            new GridLayout(
                new GridLayoutOptions {
                    Columns = new List<GridColumnDefinition> {
                        new() { Width = 40, Type = GridValueType.Pixels },
                        new() { Width = 1, Type = GridValueType.Pixels },
                        new() { Width = 1, Type = GridValueType.RemainderValue }
                    },
                    Rows = new List<GridRowDefinition> {
                        new() { Height = 1, Type = GridValueType.Percentage }
                    }
                })).Fill();

        layout.RefreshLayout();
    }

    private void InitMenu()
    {
        var menuStack = layout.Add(new StackPanel { Orientation = Orientation.Vertical }, 0, 0);

        menuStack.Add(new Label { Text = "Samples Menu".ToYellow(underlined: true) });
        menuStack.Add(new Label { Text = ConsoleString.Empty });
        var overviewButton = menuStack.Add(
            new Button { Tag = MenuTag, Shortcut = new KeyboardShortcut(ConsoleKey.O), Text = "Overview".ToWhite() });

        SetupMenuItem(
            overviewButton,
            () => {
                var panel = new ConsolePanel();
                var label = panel.Add(new Label { Text = "Welcome to the PowerArgs sample app.".ToGreen() })
                    .CenterBoth();

                return panel;
            });

        var calculatorButton = menuStack.Add(
            new Button {
                Tag = MenuTag, Shortcut = new KeyboardShortcut(ConsoleKey.C), Text = "Calculator program".ToWhite()
            });

        SetupMenuItem(
            calculatorButton,
            () => {
                var panel = new ConsolePanel();
                var console = panel.Add(
                        new SampleConsole(() => new CommandLineArgumentsDefinition(typeof(CalculatorProgram))))
                    .Fill();

                return panel;
            });

        var args = new PerfTestArgs { Test = TestCase.FallingChars };
        var perfButton = menuStack.Add(
            new Button { Tag = MenuTag, Shortcut = new KeyboardShortcut(ConsoleKey.P), Text = "Perf Test".ToWhite() });

        SetupMenuItem(
            perfButton,
            () => {
                var panel = new StackPanel { Height = 3, Orientation = Orientation.Vertical };
                panel.Add(new Form(FormOptions.FromObject(args)) { Height = 2 }).FillHorizontally();
                var runButton = panel.Add(
                    new Button { Text = "Run".ToWhite(), Shortcut = new KeyboardShortcut(ConsoleKey.R) });

                runButton.Pressed.SubscribeOnce(
                    () => {
                        panel.Controls.Clear();
                        var console = panel.Add(new PerfTest(args)).Fill();
                    });

                InvokeNextCycle(() => panel.Descendents.Where(d => d.CanFocus).FirstOrDefault()?.TryFocus());

                return panel;
            });

        var colorArgs = new ColorTestArgs
            { From = ConsoleColor.Black, To = ConsoleColor.Green, Mode = ConsoleMode.VirtualTerminal };

        var colorButton = menuStack.Add(
            new Button { Tag = MenuTag, Shortcut = new KeyboardShortcut(ConsoleKey.R), Text = "RGB Test".ToWhite() });

        SetupMenuItem(
            colorButton,
            () => {
                var panel = new ConsolePanel { Height = 4 };
                panel.Add(new Form(FormOptions.FromObject(colorArgs)) { Height = 3 }).FillHorizontally();
                var runButton = panel.Add(
                    new Button { Y = 3, Text = "Run".ToWhite(), Shortcut = new KeyboardShortcut(ConsoleKey.R) });

                runButton.Pressed.SubscribeOnce(
                    () => {
                        panel.Controls.Clear();

                        if (colorArgs.Mode == ConsoleMode.VirtualTerminal)
                        {
                            ConsoleProvider.Fancy = true;
                        }

                        if (colorArgs.Mode == ConsoleMode.Console)
                        {
                            ConsoleProvider.Fancy = false;
                        }

                        var toColor = panel.Add(
                                new ConsolePanel { Width = 20, Height = 3, Background = colorArgs.From })
                            .CenterBoth();

                        var label = toColor
                            .Add(
                                new Label { Text = toColor.Background.ToRGBString().ToWhite(toColor.Background, true) })
                            .CenterBoth();

                        RGB.AnimateAsync(
                            new RGBAnimationOptions {
                                Transitions = new List<KeyValuePair<RGB, RGB>> {
                                    new(colorArgs.From, colorArgs.To)
                                },
                                Duration = 1500,
                                EasingFunction = Animator.EaseInOut,
                                AutoReverse = true,
                                Loop = this,
                                AutoReverseDelay = 500,
                                OnColorsChanged = c => {
                                    toColor.Background = c[0];
                                    label.Text = toColor.Background.ToRGBString().ToWhite(toColor.Background, true);
                                }
                            });
                    });

                InvokeNextCycle(() => panel.Descendents.Where(d => d.CanFocus).FirstOrDefault()?.TryFocus());

                return panel;
            });

        overviewButton.Pressed.Fire();
    }

    private void InitSplitter()
    {
        layout.Add(new Divider { Foreground = ConsoleColor.DarkGray, Orientation = Orientation.Vertical }, 1, 0);
    }

    private void InitNavKeys()
    {
        FocusManager.GlobalKeyHandlers.PushForLifetime(
            ConsoleKey.Escape,
            null,
            () => {
                if (currentContent != null &&
                    FocusManager.FocusedControl != null &&
                    (FocusManager.FocusedControl == currentContent ||
                        (currentContent is ConsolePanel &&
                            (currentContent as ConsolePanel).Descendents.Contains(FocusManager.FocusedControl)))
                   )
                {
                    currentNavButton?.TryFocus();
                }
                else
                {
                    AnimatedDialog.Show(
                        dialogHandle => {
                            var contentBg = RGB.Yellow;
                            var bgCompliment = contentBg.GetCompliment();
                            var textColor = RGB.Black.CalculateDistanceTo(bgCompliment) < RGB.MaxDistance * .75f
                                ? RGB.Black
                                : bgCompliment;

                            var panel = new ConsolePanel {
                                Height = 11, Width = ConsoleMath.Round(LayoutRoot.Width * .5f), Background = contentBg
                            };

                            var label = panel.Add(
                                    new Label {
                                        Text = "Press enter to quit or escape to resume".ToConsoleString(
                                            textColor,
                                            contentBg)
                                    })
                                .CenterBoth();

                            FocusManager.GlobalKeyHandlers.PushForLifetime(ConsoleKey.Enter, null, () => Stop(), panel);
                            FocusManager.GlobalKeyHandlers.PushForLifetime(
                                ConsoleKey.Escape,
                                null,
                                dialogHandle.CloseDialog,
                                panel);

                            return panel;
                        }, new AnimatedDialogOptions { Parent = LayoutRoot });
                }
            },
            this);
    }

    private void SetupMenuItem(Button? b, Func<ConsoleControl> viewFactory)
    {
        b.Pressed.SubscribeForLifetime(
            b,
            () => {
                foreach (var button in layout.Descendents.WhereAs<Button>().Where(bt => MenuTag.Equals(bt.Tag)))
                    button.Text = button.Text.ToWhite();

                b.Text = b.Text.ToCyan();
                SetContent(viewFactory());
                currentNavButton = b;
            });
    }

    private void SetContent(ConsoleControl? newContent)
    {
        layout.Remove(currentContent);
        currentContent = newContent;
        layout.Add(currentContent, 2, 0);

        InvokeNextCycle(
            () => {
                if (currentContent.IsExpired == false)
                {
                    return;
                }

                if (newContent.CanFocus)
                {
                    newContent.TryFocus();
                }
                else if (newContent is ConsolePanel panel)
                {
                    panel.Descendents.FirstOrDefault(d => d.CanFocus)?.TryFocus();
                }
            });
    }
}

public class ColorTestArgs
{
    public RGB From { get; set; }
    public RGB To { get; set; }
    public ConsoleMode Mode { get; set; }
}

public class SampleConsole : CompactConsole
{
    private readonly Func<CommandLineArgumentsDefinition> factory;

    public SampleConsole(Func<CommandLineArgumentsDefinition> factory) { this.factory = factory; }

    protected override CommandLineArgumentsDefinition CreateDefinition() => factory();

    protected override Task Run(ArgAction toRun)
    {
        ConsoleOutInterceptor.Instance.Attach();
        toRun.Invoke();
        var output = ConsoleOutInterceptor.Instance.ReadAndClear();
        ConsoleOutInterceptor.Instance.Detach();
        WriteLine(new ConsoleString(output));
        return Task.CompletedTask;
    }
}