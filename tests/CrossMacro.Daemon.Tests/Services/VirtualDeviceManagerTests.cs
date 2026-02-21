namespace CrossMacro.Daemon.Tests.Services;

using CrossMacro.Daemon.Services;

public class VirtualDeviceManagerTests
{
    [Fact]
    public void SendEvent_WhenNotConfigured_DoesNotThrow()
    {
        var manager = new VirtualDeviceManager();

        var ex = Record.Exception(() => manager.SendEvent(type: 1, code: 2, value: 3));

        Assert.Null(ex);
    }

    [Fact]
    public void Reset_WhenNotConfigured_DoesNotThrow()
    {
        var manager = new VirtualDeviceManager();

        var ex = Record.Exception(manager.Reset);

        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var manager = new VirtualDeviceManager();

        var ex = Record.Exception(() =>
        {
            manager.Dispose();
            manager.Dispose();
        });

        Assert.Null(ex);
    }
}
