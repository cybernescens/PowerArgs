﻿using PowerArgs;
using PowerArgs.Cli;
using PowerArgs.Cli.Physics;

namespace Benchmarks;

public class AnimationBenchmark : UIBenchmark
{
    protected override float RunActual(ConsoleApp app)
    {
        app.Invoke(
            async () => {
                var square = app.LayoutRoot.Add(new ConsolePanel { Width = 20, Height = 10, Background = RGB.Green })
                    .CenterVertically();

                var startTime = DateTime.UtcNow;
                while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(3))
                {
                    square.X = square.Right() == app.LayoutRoot.Width ? (int)0 : square.X + (int)1;
                    await app.RequestPaintAsync();
                }

                app.Stop();
            });

        app.Run();
        return app.TotalPaints;
    }
}