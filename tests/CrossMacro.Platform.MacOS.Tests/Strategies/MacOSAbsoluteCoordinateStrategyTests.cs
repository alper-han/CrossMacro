
namespace CrossMacro.Platform.MacOS.Tests.Strategies;

public sealed class MacOSAbsoluteCoordinateStrategyTests
{
    [Fact]
    public void ProcessPosition_WhenSyncEvent_ReturnsNoSample()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy();

        var result = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.Sync,
        });

        Assert.False(result.HasValue);
    }

    [Fact]
    public void ProcessPosition_WhenNonMouseMoveEvent_ReturnsLastKnownPosition()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy();

        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 42,
        });
        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 99,
        });

        var result = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.Key,
            Code = InputEventCode.KEY_A,
            Value = 1,
        });

        Assert.Equal(CoordinateSample.Create(42, 99), result);
    }

    [Fact]
    public void ProcessPosition_WhenAxesArriveSeparately_EmitsOneAtomicSampleOnSync()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy();

        var yResult = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 15,
        });

        var xResult = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 320,
        });
        var result = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });

        Assert.False(yResult.HasValue);
        Assert.False(xResult.HasValue);
        Assert.Equal(CoordinateSample.Create(320, 15), result);
    }

    [Fact]
    public void ProcessPosition_WhenOnlyOneAxisChanges_EmitsUpdatedPositionOnSync()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy();

        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 640,
        });

        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 480,
        });
        var result = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });

        Assert.Equal(CoordinateSample.Create(640, 480), result);
    }
}
