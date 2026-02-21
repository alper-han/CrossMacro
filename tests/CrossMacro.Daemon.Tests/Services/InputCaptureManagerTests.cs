namespace CrossMacro.Daemon.Tests.Services;

using CrossMacro.Daemon.Services;

public class InputCaptureManagerTests
{
    [Fact]
    public void StopCapture_WhenNeverStarted_DoesNotThrow()
    {
        var manager = new InputCaptureManager();

        var ex = Record.Exception(manager.StopCapture);

        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_WhenNeverStarted_DoesNotThrow()
    {
        var manager = new InputCaptureManager();

        var ex = Record.Exception(manager.Dispose);

        Assert.Null(ex);
    }
}
