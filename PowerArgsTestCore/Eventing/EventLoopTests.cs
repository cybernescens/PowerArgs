using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace ArgsTests;

[TestClass]
[TestCategory(Categories.Eventing)]
public class EventLoopTests
{
    [TestMethod]
    public async Task TestEventLoopBasic()
    {
        var loop = new EventLoop();
        var fired = 0;

        loop.Invoke(
            async () => {
                Assert.AreEqual(0, loop.Cycle);
                fired++;
                await Task.Yield();
                Assert.AreEqual(1, loop.Cycle);
                fired++;
                loop.Stop();
            });

        await loop.Start();
        Assert.AreEqual(2, fired);
    }

    [TestMethod]
    public async Task TestEventLoopSynchronousException()
    {
        var expectedError = "This is the expected error message";
        var loop = new EventLoop();
        loop.Invoke(() => throw new Exception(expectedError));
        try
        {
            await loop.Start();
            Assert.Fail("An exception should have been thrown");
        }
        catch (Exception ex)
        {
            Assert.AreEqual(expectedError, ex.Clean().Single().Message);
        }
    }

    [TestMethod]
    public async Task TestEventLoopAsynchronousException()
    {
        var expectedError = "This is the expected error message";
        var loop = new EventLoop();
        loop.Invoke(
            async () => {
                await Task.Yield();
                throw new Exception(expectedError);
            });

        try
        {
            await loop.Start();
            Assert.Fail("An exception should have been thrown");
        }
        catch (Exception ex)
        {
            Assert.AreEqual(expectedError, ex.Clean().Single().Message);
        }
    }
}