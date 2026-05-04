using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Platform.Abstractions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Core.Tests.Services.Recording;

public class CoordinateStrategyTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    #region RelativeCoordinateStrategy Tests

    [Fact]
    public async Task RelativeCoordinateStrategy_Initialize_ShouldResetState()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();

        // Act
        await strategy.InitializeAsync(CancellationToken.None);

        // Assert - no exception means success
        strategy.Should().NotBeNull();
    }

    [Fact]
    public void RelativeCoordinateStrategy_ProcessPosition_MouseMove_ShouldBufferDeltas()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();
        var xEvent = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };
        var yEvent = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_Y, Value = 20 };

        // Act
        var resultX = strategy.ProcessPosition(xEvent);
        var resultY = strategy.ProcessPosition(yEvent);

        // Assert - should return (0,0) until sync
        resultX.Should().Be((0, 0));
        resultY.Should().Be((0, 0));
    }

    [Fact]
    public void RelativeCoordinateStrategy_ProcessPosition_Sync_ShouldFlushAccumulatedDeltas()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();
        var xEvent = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };
        var yEvent = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_Y, Value = 20 };
        var syncEvent = new InputCaptureEventArgs { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        strategy.ProcessPosition(xEvent);
        strategy.ProcessPosition(yEvent);
        var result = strategy.ProcessPosition(syncEvent);

        // Assert
        result.X.Should().Be(10);
        result.Y.Should().Be(20);
    }

    [Fact]
    public void RelativeCoordinateStrategy_ProcessPosition_ButtonEvent_ShouldFlushPendingDeltas()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();
        var xEvent = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 5 };
        var buttonEvent = new InputCaptureEventArgs { Type = InputEventType.MouseButton, Code = 272, Value = 1 };

        // Act
        strategy.ProcessPosition(xEvent);
        var result = strategy.ProcessPosition(buttonEvent);

        // Assert
        result.X.Should().Be(5);
        result.Y.Should().Be(0);
    }

    [Fact]
    public void RelativeCoordinateStrategy_ProcessPosition_Sync_ShouldResetPendingAfterFlush()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();
        var xEvent = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };
        var syncEvent = new InputCaptureEventArgs { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        strategy.ProcessPosition(xEvent);
        strategy.ProcessPosition(syncEvent);
        var secondSync = strategy.ProcessPosition(syncEvent);

        // Assert - second sync should return (0,0) since pending was cleared
        secondSync.Should().Be((0, 0));
    }

    [Fact]
    public void RelativeCoordinateStrategy_ProcessPosition_ShouldAccumulateMultipleDeltas()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();
        var xEvent1 = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 5 };
        var xEvent2 = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 3 };
        var yEvent = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_Y, Value = -2 };
        var syncEvent = new InputCaptureEventArgs { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        strategy.ProcessPosition(xEvent1);
        strategy.ProcessPosition(xEvent2);
        strategy.ProcessPosition(yEvent);
        var result = strategy.ProcessPosition(syncEvent);

        // Assert
        result.X.Should().Be(8); // 5 + 3
        result.Y.Should().Be(-2);
    }

    #endregion

    #region AbsoluteCoordinateStrategy Tests

    [Fact]
    public async Task AbsoluteCoordinateStrategy_Initialize_ShouldSetInitialPosition()
    {
        // Arrange
        var positionProvider = Substitute.For<IMousePositionProvider>();
        positionProvider.GetAbsolutePositionAsync().Returns((X: 100, Y: 200));
        
        var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        using var cts = new CancellationTokenSource(TestTimeout);

        // Act
        await strategy.InitializeAsync(cts.Token);

        // Assert
        await positionProvider.Received().GetAbsolutePositionAsync();
        
        // Cleanup
        strategy.Dispose();
    }

    [Fact]
    public async Task AbsoluteCoordinateStrategy_ProcessPosition_ShouldAccumulateRelativeDeltas()
    {
        // Arrange
        var positionProvider = Substitute.For<IMousePositionProvider>();
        positionProvider.GetAbsolutePositionAsync().Returns((X: 100, Y: 100));
        
        var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        using var cts = new CancellationTokenSource(TestTimeout);
        await strategy.InitializeAsync(cts.Token);

        var xEvent = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };
        var yEvent = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_Y, Value = 20 };

        // Act
        strategy.ProcessPosition(xEvent);
        var result = strategy.ProcessPosition(yEvent);

        // Assert - should accumulate from initial position
        result.X.Should().Be(110); // 100 + 10
        result.Y.Should().Be(120); // 100 + 20
        
        // Cleanup
        strategy.Dispose();
    }

    [Fact]
    public void AbsoluteCoordinateStrategy_ProcessPosition_Sync_ShouldReturnZero()
    {
        // Arrange
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        var syncEvent = new InputCaptureEventArgs { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        var result = strategy.ProcessPosition(syncEvent);

        // Assert
        result.Should().Be((0, 0));
        
        // Cleanup
        strategy.Dispose();
    }

    [Fact]
    public async Task AbsoluteCoordinateStrategy_Initialize_ShouldHandleNullPosition()
    {
        // Arrange
        var positionProvider = Substitute.For<IMousePositionProvider>();
        positionProvider.GetAbsolutePositionAsync().Returns(((int X, int Y)?)null);
        
        var strategy = new AbsoluteCoordinateStrategy(positionProvider);
        using var cts = new CancellationTokenSource(TestTimeout);

        // Act - should not throw
        await strategy.InitializeAsync(cts.Token);

        // Assert - defaults to (0, 0)
        var xEvent = new InputCaptureEventArgs { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 5 };
        var result = strategy.ProcessPosition(xEvent);
        result.X.Should().Be(5); // 0 + 5

        // Cleanup
        strategy.Dispose();
    }

    #endregion
}
