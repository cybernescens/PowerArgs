﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs.Cli;
using PowerArgs;

namespace ArgsTests.CLI
{
    [TestClass]
    [TestCategory(Categories.ConsoleApp)]
    public class VisualTreeTests
    {
        [TestMethod]
        [TestCategory(Categories.ConsoleApp)]
        public void ConsoleAppLifecycleTestBasic()
        {
            ConsoleProvider.Current = new CliUnitTestConsole();
            ConsoleApp app = new ConsoleApp(80, 10);

            app.Invoke(() => {
                int addCounter = 0, removeCounter = 0;

                app.ControlAdded.SubscribeForLifetime(app, (object[] _) => { addCounter++; });
                app.ControlRemoved.SubscribeForLifetime(app, (object[] _) => { removeCounter++; });
                app.LayoutRoot.Id = "LayoutRoot";
                ConsolePanel? panel = app.LayoutRoot.Add(new ConsolePanel() { Id = "First panel" });
                // direct child
                Assert.AreEqual(1, addCounter);
                Assert.AreEqual(0, removeCounter);

                var button = panel.Add(new Button() { Id = "Button on first panel" });

                // grandchild
                Assert.AreEqual(2, addCounter);
                Assert.AreEqual(0, removeCounter);

                var innerPanel = new ConsolePanel() { Id = "InnerPanel" };
                var innerInnerPanel = innerPanel.Add(new ConsolePanel() { Id = "Inner Inner Panel" });

                // no change since not added to the app yet
                Assert.AreEqual(2, addCounter);
                Assert.AreEqual(0, removeCounter);

                panel.Add(innerPanel);

                // both child and grandchild found on add
                Assert.AreEqual(4, addCounter);
                Assert.AreEqual(0, removeCounter);

                // remove a nested child
                innerPanel.Controls.Remove(innerInnerPanel);
                Assert.AreEqual(4, addCounter);
                Assert.AreEqual(1, removeCounter);

                app.LayoutRoot.Controls.Clear();
                Assert.AreEqual(4, addCounter);
                Assert.AreEqual(4, removeCounter);
                app.Stop();
            });
            app.Run();
        }

        
        [TestMethod]
        public void EnsureCantReuseControls()
        {
            ConsoleProvider.Current = new CliUnitTestConsole();
            ConsoleApp app = new ConsoleApp(80, 10);
            app.Invoke(() =>
            {
                var panel = app.LayoutRoot.Add(new ConsolePanel());
                var button = panel.Add(new Button());
                panel.Controls.Remove(button);

                try
                {
                    app.LayoutRoot.Add(button);
                    Assert.Fail("An exception should have been thrown");
                }
                catch (ObjectDisposedException ex)
                {
                    Console.WriteLine(ex.Message);
                    app.Stop();
                }
            });
            app.Run();
        }
    }
}
