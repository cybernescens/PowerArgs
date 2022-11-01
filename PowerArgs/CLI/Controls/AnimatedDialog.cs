using PowerArgs.Cli.Physics;

namespace PowerArgs.Cli;

/// <summary>
///     An object that will allow you do configre the dialog as well as close it.
/// </summary>
public class DialogHandle
{
    private readonly Lifetime callerLifetime = new();

    internal DialogHandle() { }

    /// <summary>
    ///     Set this to override the border color of the dialog. By default the dialog will try to find a dark version of your
    ///     content's background color.
    /// </summary>
    public RGB? BorderColor { get; set; }

    internal ILifetimeManager CallerLifetime => callerLifetime;

    /// <summary>
    ///     Closes the dialog.
    /// </summary>
    public void CloseDialog() => callerLifetime.Dispose();
}

public class AnimatedDialogOptions
{
    public ConsolePanel Parent { get; init; } = null!;
    public bool PushPop { get; init; } = true;
    public float SpeedPercentage { get; init; } = 1;
    public bool AllowEscapeToClose { get; init; } = false;
    public bool AllowEnterToClose { get; init; } = false;
    public int ZIndex { get; init; } = 0;
}

/// <summary>
///     Utility that lets you add animated dialogs to your ConsoleApps.
/// </summary>
public class AnimatedDialog
{
    /// <summary>
    ///     Shows a dialog on top of the current ConsoleApp.
    /// </summary>
    /// <param name="contentFactory">
    ///     A callback where you are given a handle that can be used to configure the dialog.
    ///     It also has a method that lets you close the dialog. This callback should return the dialog content.
    /// </param>
    public static async void Show(Func<DialogHandle, Container> contentFactory, AnimatedDialogOptions options)
    {
        using (var dialogLt = new Lifetime())
        {
            var handle = new DialogHandle();
            if (options.PushPop)
            {
                options.Parent.Application.FocusManager.Push();

                if (options.AllowEscapeToClose)
                {
                    options.Parent.Application.FocusManager.GlobalKeyHandlers.PushForLifetime(
                        ConsoleKey.Escape,
                        null,
                        () => { handle.CloseDialog(); },
                        dialogLt);
                }

                if (options.AllowEnterToClose)
                {
                    options.Parent.Application.FocusManager.GlobalKeyHandlers.PushForLifetime(
                        ConsoleKey.Enter,
                        null,
                        () => { handle.CloseDialog(); },
                        dialogLt);
                }

                dialogLt.OnDisposed(options.Parent.Application.FocusManager.Pop);
            }

            var content = contentFactory(handle);
            content.IsVisible = false;
            var dialogContainer = options.Parent.Add(
                    new BorderPanel(content)
                        { BorderColor = handle.BorderColor, Background = content.Background, Width = 1, Height = 1 })
                .CenterBoth();

            dialogContainer.ZIndex = options.ZIndex;
            await Forward(
                300 * options.SpeedPercentage,
                dialogLt,
                percentage => dialogContainer.Width = Math.Max(1, ConsoleMath.Round((4 + content.Width) * percentage)));

            await Forward(
                200 * options.SpeedPercentage,
                dialogLt,
                percentage => dialogContainer.Height = Math.Max(
                    1,
                    ConsoleMath.Round((2 + content.Height) * percentage)));

            content.IsVisible = true;
            await handle.CallerLifetime.AwaitEndOfLifetime();
            content.IsVisible = false;
            await Reverse(
                150 * options.SpeedPercentage,
                dialogLt,
                percentage => dialogContainer.Height = Math.Max(1, (int)Math.Floor((2 + content.Height) * percentage)));

            await Task.Delay((int)(200 * options.SpeedPercentage));
            await Reverse(
                200 * options.SpeedPercentage,
                dialogLt,
                percentage => dialogContainer.Width = Math.Max(1, ConsoleMath.Round((4 + content.Width) * percentage)));

            dialogContainer.Dispose();
        }
    }

    private static Task Forward(float duration, Lifetime? lt, Action<float> setter) =>
        AnimateCommon(duration, lt, setter, 0, 1);

    private static Task Reverse(float duration, Lifetime? lt, Action<float> setter) =>
        AnimateCommon(duration, lt, setter, 1, 0);

    private static async Task AnimateCommon(float duration, Lifetime? lt, Action<float> setter, float from, float to)
    {
        if (duration == 0)
        {
            setter(to);
        }
        else
        {
            await Animator.AnimateAsync(
                new FloatAnimatorOptions {
                    From = from,
                    To = to,
                    Duration = duration,
                    EasingFunction = Animator.EaseInOut,
                    IsCancelled = () => lt.IsExpired,
                    Setter = percentage => setter(percentage)
                });
        }
    }
}