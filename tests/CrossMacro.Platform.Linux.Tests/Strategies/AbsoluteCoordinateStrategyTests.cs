
namespace CrossMacro.Platform.Linux.Tests.Strategies;

public sealed class AbsoluteCoordinateStrategyTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task ProcessPosition_ShouldEmitOneAtomicSampleAfterBothAxesArrive()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.GetAbsolutePositionAsync().Returns((X: 100, Y: 100));
        using var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        using var cts = new CancellationTokenSource(TestTimeout);
        await strategy.InitializeAsync(cts.Token);

        var xResult = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.ABS_X, Value = 10 });
        var yResult = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.ABS_Y, Value = 20 });
        var result = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });

        Assert.False(xResult.HasValue);
        Assert.False(yResult.HasValue);
        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
    }

    [Fact]
    public void ProcessPosition_WhenOnlySyncArrives_ReturnsNoSample()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        using var strategy = new AbsoluteCoordinateStrategy(positionProvider);

        var result = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync, Code = 0, Value = 0 });

        Assert.False(result.HasValue);
    }

    [Fact]
    public async Task Initialize_WhenInitialPositionIsUnavailable_StartsFromOrigin()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.GetAbsolutePositionAsync().Returns(((int X, int Y)?)null);
        using var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        using var cts = new CancellationTokenSource(TestTimeout);

        await strategy.InitializeAsync(cts.Token);
        _ = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.ABS_X, Value = 5 });
        var result = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });

        Assert.Equal(5, result.X);
        Assert.Equal(0, result.Y);
    }
}
