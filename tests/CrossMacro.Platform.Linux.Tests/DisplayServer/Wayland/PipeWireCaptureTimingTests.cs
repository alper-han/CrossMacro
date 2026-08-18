namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PipeWireCaptureTimingTests
{
    [Fact]
    public void Create_WhenTimeoutIsZero_UsesShortImmediateCaptureBudget()
    {
        var timing = PipeWireCaptureTiming.Create(TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromMilliseconds(250), timing.Timeout);
    }

    [Fact]
    public void Create_WhenTimeoutIsPositive_PreservesItWithinFrameBudget()
    {
        var timing = PipeWireCaptureTiming.Create(TimeSpan.FromMilliseconds(750));

        Assert.Equal(TimeSpan.FromMilliseconds(750), timing.Timeout);
    }

    [Fact]
    public void Create_WhenTimeoutExceedsFrameBudget_UsesFrameBudget()
    {
        var requestedTimeout = TimeSpan.FromSeconds(5);
        var timing = PipeWireCaptureTiming.Create(requestedTimeout);

        Assert.Equal(PipeWireCaptureTiming.MaximumFrameTimeout, timing.Timeout);
    }
}
