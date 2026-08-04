
namespace CrossMacro.Platform.Linux.Tests.Strategies;

public sealed class AbsoluteCoordinateStrategyTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task AbsoluteCoordinateStrategy_Initialize_ShouldSetInitialPosition()
    {
        // Arrange
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.GetAbsolutePositionAsync().Returns((X: 100, Y: 200));

        var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        using var cts = new CancellationTokenSource(TestTimeout);

        // Act
        await strategy.InitializeAsync(cts.Token);

        // Assert
        _ = await positionProvider.Received().GetAbsolutePositionAsync();

        // Cleanup
        strategy.Dispose();
    }

    [Fact]
    public async Task AbsoluteCoordinateStrategy_ProcessPosition_ShouldUseAbsoluteRootCoordinates()
    {
        // Arrange
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.GetAbsolutePositionAsync().Returns((X: 100, Y: 100));

        var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        using var cts = new CancellationTokenSource(TestTimeout);
        await strategy.InitializeAsync(cts.Token);

        var xEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.ABS_X, Value = 10 };
        var yEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.ABS_Y, Value = 20 };

        // Act
        var xResult = strategy.ProcessPosition(xEvent);
        var yResult = strategy.ProcessPosition(yEvent);
        var result = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });

        // Assert - should emit one atomic sample after both axes have been accumulated
        _ = xResult.HasValue.Should().BeFalse();
        _ = yResult.HasValue.Should().BeFalse();
        _ = result.X.Should().Be(10);
        _ = result.Y.Should().Be(20);

        // Cleanup
        strategy.Dispose();
    }

    [Fact]
    public void AbsoluteCoordinateStrategy_ProcessPosition_Sync_ShouldReturnNoSample()
    {
        // Arrange
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        var syncEvent = new CapturedInputEvent { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        var result = strategy.ProcessPosition(syncEvent);

        // Assert
        _ = result.HasValue.Should().BeFalse();

        // Cleanup
        strategy.Dispose();
    }

    [Fact]
    public async Task AbsoluteCoordinateStrategy_Initialize_ShouldHandleNullPosition()
    {
        // Arrange
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.GetAbsolutePositionAsync().Returns(((int X, int Y)?)null);

        var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        using var cts = new CancellationTokenSource(TestTimeout);

        // Act - should not throw
        await strategy.InitializeAsync(cts.Token);

        // Assert - defaults to (0, 0)
        var xEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.ABS_X, Value = 5 };
        _ = strategy.ProcessPosition(xEvent);
        var result = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });
        _ = result.X.Should().Be(5);

        // Cleanup
        strategy.Dispose();
    }
}
