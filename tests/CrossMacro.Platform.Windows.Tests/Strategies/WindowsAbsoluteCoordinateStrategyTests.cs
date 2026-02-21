using System.Threading;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Windows.Strategies;
using CrossMacro.TestInfrastructure;
using NSubstitute;
using Xunit;

namespace CrossMacro.Platform.Windows.Tests.Strategies;

public class WindowsAbsoluteCoordinateStrategyTests
{
    [WindowsFact]
    public async Task InitializeAsync_WhenPositionAvailable_UsesItForNonMouseEvents()
    {
        // Arrange
        var provider = Substitute.For<IMousePositionProvider>();
        provider.GetAbsolutePositionAsync().Returns((10, 20));
        var strategy = new WindowsAbsoluteCoordinateStrategy(provider);

        // Act
        await strategy.InitializeAsync(CancellationToken.None);
        var pos = strategy.ProcessPosition(new InputCaptureEventArgs { Type = InputEventType.Key });

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
        var pos = strategy.ProcessPosition(new InputCaptureEventArgs { Type = InputEventType.Sync });

        // Assert
        Assert.Equal((0, 0), pos);
    }
}
