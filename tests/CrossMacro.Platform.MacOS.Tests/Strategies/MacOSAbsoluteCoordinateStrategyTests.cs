using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Strategies;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Strategies;

public class MacOSAbsoluteCoordinateStrategyTests
{
    [Fact]
    public void ProcessPosition_WhenSyncEvent_ReturnsZeroTuple()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy();

        var result = strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.Sync
        });

        Assert.Equal((0, 0), result);
    }

    [Fact]
    public void ProcessPosition_WhenNonMouseMoveEvent_ReturnsLastKnownPosition()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy();

        strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 42
        });
        strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 99
        });

        var result = strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.Key,
            Code = InputEventCode.KEY_A,
            Value = 1
        });

        Assert.Equal((42, 99), result);
    }

    [Fact]
    public void ProcessPosition_WhenAbsXEvent_UpdatesOnlyX()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy();

        strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 15
        });

        var result = strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 320
        });

        Assert.Equal((320, 15), result);
    }

    [Fact]
    public void ProcessPosition_WhenAbsYEvent_UpdatesOnlyY()
    {
        var strategy = new MacOSAbsoluteCoordinateStrategy();

        strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 640
        });

        var result = strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 480
        });

        Assert.Equal((640, 480), result);
    }
}
