
namespace CrossMacro.Platform.Windows.Tests.Strategies;

public sealed class WindowsAbsoluteCoordinateStrategyTests
{
    [WindowsFact]
    public async Task InitializeAsync_WhenPositionAvailable_UsesItForNonMouseEvents()
    {
        // Arrange
        var provider = Substitute.For<IMousePositionProvider>();
        _ = provider.GetAbsolutePositionAsync().Returns((10, 20));
        var strategy = new WindowsAbsoluteCoordinateStrategy(provider);

        // Act
        await strategy.InitializeAsync(CancellationToken.None);
        var pos = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Key });

        // Assert
        Assert.Equal(CoordinateSample.Create(10, 20), pos);
    }

    [WindowsFact]
    public void ProcessPosition_WhenSyncEvent_ReturnsNoSample()
    {
        // Arrange
        var provider = Substitute.For<IMousePositionProvider>();
        var strategy = new WindowsAbsoluteCoordinateStrategy(provider);

        // Act
        var pos = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });

        // Assert
        Assert.False(pos.HasValue);
    }

    [Fact]
    public async Task ProcessPosition_WhenCaptureProvidesAbsoluteAxes_UsesEventCoordinatesWithoutRequerying()
    {
        var provider = Substitute.For<IMousePositionProvider>();
        _ = provider.GetAbsolutePositionAsync().Returns((10, 20));
        var strategy = new WindowsAbsoluteCoordinateStrategy(provider);
        await strategy.InitializeAsync(CancellationToken.None);

        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_X,
            Value = -120,
        });
        _ = strategy.ProcessPosition(new CapturedInputEvent
        {
            Type = InputEventType.MouseMove,
            Code = InputEventCode.ABS_Y,
            Value = 450,
        });
        var sample = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });

        Assert.Equal(CoordinateSample.Create(-120, 450), sample);
        _ = provider.Received(1).GetAbsolutePositionAsync();
    }
}
