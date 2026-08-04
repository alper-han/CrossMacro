namespace CrossMacro.Platform.Linux.Tests.Strategies;

public sealed class X11LogicalRelativeCoordinateStrategyTests
{
    [Fact]
    public async Task ProcessPosition_ConvertsRootCoordinatesToLogicalDeltas()
    {
        var provider = Substitute.For<IMousePositionProvider>();
        _ = provider.GetAbsolutePositionAsync().Returns((100, 80));
        using var strategy = new X11LogicalRelativeCoordinateStrategy(provider);
        await strategy.InitializeAsync(CancellationToken.None);

        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = 110,
        });
        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 75,
        });
        var sample = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });

        _ = sample.Should().Be(CoordinateSample.Create(10, -5));
    }

    [Fact]
    public async Task ProcessPosition_WhenInitialPositionIsUnavailable_UsesFirstFrameAsBaseline()
    {
        var provider = Substitute.For<IMousePositionProvider>();
        _ = provider.GetAbsolutePositionAsync().Returns((ValueTuple<int, int>?)null);
        using var strategy = new X11LogicalRelativeCoordinateStrategy(provider);
        await strategy.InitializeAsync(CancellationToken.None);

        ProcessFrame(strategy, 400, 300);
        var sample = ProcessFrame(strategy, 405, 290);

        Assert.Equal(CoordinateSample.Create(5, -10), sample);
    }

    private static CoordinateSample ProcessFrame(
        X11LogicalRelativeCoordinateStrategy strategy,
        int x,
        int y)
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
        return strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });
    }
}
