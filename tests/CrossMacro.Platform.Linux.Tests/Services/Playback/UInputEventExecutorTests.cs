using CrossMacro.Core.Services.Playback;

namespace CrossMacro.Platform.Linux.Tests.Services.Playback;


public sealed class UInputEventExecutorTests
{
    [Fact]
    public void Methods_WhenNotInitialized_ShouldNotThrow()
    {
        using var executor = new UInputEventExecutor();

        var ex = Record.Exception(() =>
        {
            executor.MoveAbsolute(10, 20);
            executor.MoveRelative(-5, 7);
            executor.EmitButton(1, pressed: true);
            executor.EmitKey(30, pressed: true);
            executor.EmitScroll(1);
            executor.ReleaseAll();
            executor.Execute(new MacroEvent { Type = EventType.MouseMove, X = 1, Y = 2 }, MouseCoordinateMode.Relative);
            executor.Execute(new MacroEvent { Type = EventType.MouseMove, X = 1, Y = 2 }, MouseCoordinateMode.Absolute);
            executor.Execute(new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Left, X = 1, Y = 2 }, coordinateMode: null);
        });

        Assert.Null(ex);
        Assert.False(executor.IsMouseButtonPressed);
    }

    [Fact]
    public void Execute_LogicalRelativeWithoutKnownPosition_ShouldNotFallBackToRawMovement()
    {
        using var executor = new UInputEventExecutor();
        var macroEvent = new MacroEvent { Type = EventType.MouseMove, X = 4, Y = -2 };

        var exception = Record.Exception(() => executor.Execute(
            macroEvent,
            MouseCoordinateMode.Relative,
            MouseCoordinateSpace.LogicalDesktop));

        _ = exception.Should().BeOfType<LogicalRelativePositionUnavailableException>();
    }
}
