
namespace CrossMacro.Core.Tests.Services.Recording;

public sealed class CoordinateStrategyTests
{
    #region RelativeCoordinateStrategy Tests

    [Fact]
    public async Task RelativeCoordinateStrategy_Initialize_ShouldResetState()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();

        // Act
        await strategy.InitializeAsync(CancellationToken.None);

        // Assert - no exception means success
        _ = strategy.Should().NotBeNull();
    }

    [Fact]
    public void RelativeCoordinateStrategy_ProcessPosition_MouseMove_ShouldBufferDeltas()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();
        var xEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };
        var yEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_Y, Value = 20 };

        // Act
        var resultX = strategy.ProcessPosition(xEvent);
        var resultY = strategy.ProcessPosition(yEvent);

        // Assert
        _ = resultX.HasValue.Should().BeFalse();
        _ = resultY.HasValue.Should().BeFalse();
    }

    [Fact]
    public void RelativeCoordinateStrategy_ProcessPosition_Sync_ShouldFlushAccumulatedDeltas()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();
        var xEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };
        var yEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_Y, Value = 20 };
        var syncEvent = new CapturedInputEvent { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        _ = strategy.ProcessPosition(xEvent);
        _ = strategy.ProcessPosition(yEvent);
        var result = strategy.ProcessPosition(syncEvent);

        // Assert
        _ = result.X.Should().Be(10);
        _ = result.Y.Should().Be(20);
    }

    [Fact]
    public void RelativeCoordinateStrategy_ProcessPosition_ButtonEvent_ShouldFlushPendingDeltas()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();
        var xEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 5 };
        var buttonEvent = new CapturedInputEvent { Type = InputEventType.MouseButton, Code = 272, Value = 1 };

        // Act
        _ = strategy.ProcessPosition(xEvent);
        var result = strategy.ProcessPosition(buttonEvent);

        // Assert
        _ = result.X.Should().Be(5);
        _ = result.Y.Should().Be(0);
    }

    [Fact]
    public void RelativeCoordinateStrategy_ProcessPosition_Sync_ShouldResetPendingAfterFlush()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();
        var xEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 10 };
        var syncEvent = new CapturedInputEvent { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        _ = strategy.ProcessPosition(xEvent);
        _ = strategy.ProcessPosition(syncEvent);
        var secondSync = strategy.ProcessPosition(syncEvent);

        // Assert
        _ = secondSync.HasValue.Should().BeFalse();
    }

    [Fact]
    public void RelativeCoordinateStrategy_ProcessPosition_ShouldAccumulateMultipleDeltas()
    {
        // Arrange
        var strategy = new RelativeCoordinateStrategy();
        var xEvent1 = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 5 };
        var xEvent2 = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_X, Value = 3 };
        var yEvent = new CapturedInputEvent { Type = InputEventType.MouseMove, Code = InputEventCode.REL_Y, Value = -2 };
        var syncEvent = new CapturedInputEvent { Type = InputEventType.Sync, Code = 0, Value = 0 };

        // Act
        _ = strategy.ProcessPosition(xEvent1);
        _ = strategy.ProcessPosition(xEvent2);
        _ = strategy.ProcessPosition(yEvent);
        var result = strategy.ProcessPosition(syncEvent);

        // Assert
        _ = result.X.Should().Be(8); // 5 + 3
        _ = result.Y.Should().Be(-2);
    }

    #endregion
}
