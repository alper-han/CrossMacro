
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
    public async Task AbsoluteCoordinateStrategy_ProcessPosition_ShouldAccumulateRelativeDeltas()
    {
        // Arrange
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.GetAbsolutePositionAsync().Returns((X: 100, Y: 100));

        var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        using var cts = new CancellationTokenSource(TestTimeout);
        await strategy.InitializeAsync(cts.Token);

        var xEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };
        var yEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_Y, Value = 20 };

        // Act
        _ = strategy.ProcessPosition(xEvent);
        var result = strategy.ProcessPosition(yEvent);

        // Assert - should accumulate from initial position
        _ = result.X.Should().Be(110); // 100 + 10
        _ = result.Y.Should().Be(120); // 100 + 20

        // Cleanup
        strategy.Dispose();
    }

    [Fact]
    public void AbsoluteCoordinateStrategy_ProcessPosition_Sync_ShouldReturnZero()
    {
        // Arrange
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        var syncEvent = new CapturedInputEvent { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        var result = strategy.ProcessPosition(syncEvent);

        // Assert
        _ = result.Should().Be((0, 0));

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
        var xEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 5 };
        var result = strategy.ProcessPosition(xEvent);
        _ = result.X.Should().Be(5); // 0 + 5

        // Cleanup
        strategy.Dispose();
    }
}
