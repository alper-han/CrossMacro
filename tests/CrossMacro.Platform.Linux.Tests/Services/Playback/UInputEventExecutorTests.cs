namespace CrossMacro.Platform.Linux.Tests.Services.Playback;

using CrossMacro.Core.Models;
using CrossMacro.Platform.Linux.Services.Playback;

public class UInputEventExecutorTests
{
    [Fact]
    public void Methods_WhenNotInitialized_ShouldNotThrow()
    {
        using var executor = new UInputEventExecutor();

        var ex = Record.Exception(() =>
        {
            executor.MoveAbsolute(10, 20);
            executor.MoveRelative(-5, 7);
            executor.EmitButton(1, true);
            executor.EmitKey(30, true);
            executor.EmitScroll(1);
            executor.ReleaseAll();
            executor.Execute(new MacroEvent { Type = EventType.MouseMove, X = 1, Y = 2 }, isRecordedAbsolute: false);
        });

        Assert.Null(ex);
        Assert.False(executor.IsMouseButtonPressed);
    }
}
