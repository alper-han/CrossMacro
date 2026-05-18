using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.MacOS.Strategies;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Strategies;

public class MacOSRelativeCoordinateStrategyTests
{
    [Fact]
    public async Task ProcessPosition_WhenSyncEvent_ReturnsDeltaFromInitialPosition()
    {
        var strategy = new MacOSRelativeCoordinateStrategy(() => (100, 200));
        await strategy.InitializeAsync(CancellationToken.None);

        strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 115
        });
        strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 190
        });

        var result = strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.Sync
        });

        Assert.Equal((15, -10), result);
    }

    [Fact]
    public async Task ProcessPosition_WhenMultipleSyncEvents_ReturnsDeltaFromPreviousAbsoluteSample()
    {
        var strategy = new MacOSRelativeCoordinateStrategy(() => (100, 200));
        await strategy.InitializeAsync(CancellationToken.None);

        MoveTo(strategy, 115, 190);
        Assert.Equal((15, -10), Sync(strategy));

        MoveTo(strategy, 120, 210);
        Assert.Equal((5, 20), Sync(strategy));
    }

    [Fact]
    public async Task ProcessPosition_WhenButtonArrivesBeforeSync_FlushesPendingDelta()
    {
        var strategy = new MacOSRelativeCoordinateStrategy(() => (10, 10));
        await strategy.InitializeAsync(CancellationToken.None);

        MoveTo(strategy, 12, 15);

        var result = strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseButton,
            Code = InputEventCode.BTN_LEFT,
            Value = 1
        });

        Assert.Equal((2, 5), result);
        Assert.Equal((0, 0), Sync(strategy));
    }

    [Fact]
    public async Task ProcessPosition_WhenButtonArrivesBetweenAxisSamples_DoesNotFlushPartialDelta()
    {
        var strategy = new MacOSRelativeCoordinateStrategy(() => (10, 10));
        await strategy.InitializeAsync(CancellationToken.None);

        strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 12
        });

        var buttonResult = strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseButton,
            Code = InputEventCode.BTN_LEFT,
            Value = 1
        });

        strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 15
        });

        Assert.Equal((0, 0), buttonResult);
        Assert.Equal((2, 5), Sync(strategy));
    }

    private static void MoveTo(MacOSRelativeCoordinateStrategy strategy, int x, int y)
    {
        strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = x
        });
        strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = y
        });
    }

    private static (int X, int Y) Sync(MacOSRelativeCoordinateStrategy strategy)
    {
        return strategy.ProcessPosition(new InputCaptureEventArgs
        {
            Type = InputEventType.Sync
        });
    }
}
