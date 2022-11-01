using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using PowerArgs.Cli;
using PowerArgs.Cli.Physics;

namespace ArgsTests.CLI.Physics;

[TestClass]
[TestCategory(Categories.Physics)]
public class E2EPhysicsTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void E2ESimplestApp()
    {
        var app = new ConsoleApp(1, 1);
        var panel = app.LayoutRoot.Add(new SpaceTimePanel(new SpaceTime(1, 1)));
        var lastAge = TimeSpan.Zero;
        panel.SpaceTime.InvokeNextCycle(
            () => {
                var element = new SpacialElement();
                panel.SpaceTime.Add(element);

                panel.SpaceTime.Invoke(
                    () => Task.Factory.StartNew(() => {
                        while (true)
                        {
                            lastAge = element.CalculateAge();
                            if (Time.CurrentTime.Now == TimeSpan.FromSeconds(.5))
                            {
                                Time.CurrentTime.Stop();
                            }

                            return Task.Yield();
                        }
                    }));
            });

        panel.SpaceTime.Start().Then(() => app.Stop());
        app.Start().Wait();
        Assert.AreEqual(.5, lastAge.TotalSeconds);
    }
}