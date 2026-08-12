namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PipeWireCaptureTimingTests
{
    [Fact]
    public void Create_WhenActivationSupportsSettlingAndTimeoutIsZero_UsesBoundedPriming()
    {
        var timing = PipeWireCaptureTiming.Create(streamActivationSupported: true, TimeSpan.Zero);

        Assert.Equal(PipeWireCaptureTiming.ImmediateCaptureSettleTimeout, timing.Timeout);
        Assert.True(timing.RequiresSettlingFrame);
    }

    [Fact]
    public void Create_WhenActivationIsUnavailableAndTimeoutIsZero_PreservesImmediateTimeout()
    {
        var timing = PipeWireCaptureTiming.Create(streamActivationSupported: false, TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, timing.Timeout);
        Assert.False(timing.RequiresSettlingFrame);
    }

    [Fact]
    public void Create_WhenTimeoutIsPositive_PreservesRequestedTimeout()
    {
        var requestedTimeout = TimeSpan.FromSeconds(5);
        var timing = PipeWireCaptureTiming.Create(streamActivationSupported: true, requestedTimeout);

        Assert.Equal(requestedTimeout, timing.Timeout);
        Assert.True(timing.RequiresSettlingFrame);
    }
}
