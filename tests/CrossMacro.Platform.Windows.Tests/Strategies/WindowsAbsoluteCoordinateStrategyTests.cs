
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
        Assert.Equal((10, 20), pos);
    }

    [WindowsFact]
    public void ProcessPosition_WhenSyncEvent_ReturnsZero()
    {
        // Arrange
        var provider = Substitute.For<IMousePositionProvider>();
        var strategy = new WindowsAbsoluteCoordinateStrategy(provider);

        // Act
        var pos = strategy.ProcessPosition(new CapturedInputEvent { Type = InputEventType.Sync });

        // Assert
        Assert.Equal((0, 0), pos);
    }
}
