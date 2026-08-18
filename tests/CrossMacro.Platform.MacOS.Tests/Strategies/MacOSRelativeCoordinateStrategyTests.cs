
namespace CrossMacro.Platform.MacOS.Tests.Strategies;

public sealed class MacOSRelativeCoordinateStrategyTests
{
    [Fact]
    public async Task ProcessPosition_WhenSyncEvent_ReturnsDeltaFromInitialPosition()
    {
        var strategy = new MacOSRelativeCoordinateStrategy(() => (100, 200));
        await strategy.InitializeAsync(CancellationToken.None);

        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 115,
        });
        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 190,
        });

        var result = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.Sync,
        });

        Assert.Equal(CoordinateSample.Create(15, -10), result);
    }

    [Fact]
    public async Task ProcessPosition_WhenMultipleSyncEvents_ReturnsDeltaFromPreviousAbsoluteSample()
    {
        var strategy = new MacOSRelativeCoordinateStrategy(() => (100, 200));
        await strategy.InitializeAsync(CancellationToken.None);

        MoveTo(strategy, 115, 190);
        Assert.Equal(CoordinateSample.Create(15, -10), Sync(strategy));

        MoveTo(strategy, 120, 210);
        Assert.Equal(CoordinateSample.Create(5, 20), Sync(strategy));
    }

    [Fact]
    public async Task ProcessPosition_WhenButtonArrivesBeforeSync_FlushesPendingDelta()
    {
        var strategy = new MacOSRelativeCoordinateStrategy(() => (10, 10));
        await strategy.InitializeAsync(CancellationToken.None);

        MoveTo(strategy, 12, 15);

        var result = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseButton,
            Code = InputEventCode.BTN_LEFT,
            Value = 1,
        });

        Assert.Equal(CoordinateSample.Create(2, 5), result);
        Assert.False(Sync(strategy).HasValue);
    }

    [Fact]
    public async Task ProcessPosition_WhenButtonArrivesBetweenAxisSamples_DoesNotFlushPartialDelta()
    {
        var strategy = new MacOSRelativeCoordinateStrategy(() => (10, 10));
        await strategy.InitializeAsync(CancellationToken.None);

        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 12,
        });

        var buttonResult = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseButton,
            Code = InputEventCode.BTN_LEFT,
            Value = 1,
        });

        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 15,
        });

        Assert.False(buttonResult.HasValue);
        Assert.Equal(CoordinateSample.Create(2, 5), Sync(strategy));
    }

    private static void MoveTo(MacOSRelativeCoordinateStrategy strategy, int x, int y)
    {
        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = x,
        });
        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = y,
        });
    }

    private static CoordinateSample Sync(MacOSRelativeCoordinateStrategy strategy)
    {
        return strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.Sync,
        });
    }
}
