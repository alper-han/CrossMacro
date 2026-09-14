namespace CrossMacro.Core.Tests.Models;

public sealed class EditorActionStateTests
{
    [Fact]
    public void Clone_KeepsIndependentStateForEveryActionFamily()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.MouseMove,
            X = 12,
            ScriptVariableName = "source",
            ScreenX = 34,
            ShellCommand = "source command",
            ScreenshotOutputPath = "source.png",
            WindowSelectorValue = "source window",
            ImageSearchMatchModeWasExplicit = true,
            PreferLegacyScriptText = true,
        };
        var clone = action.Clone();
        clone.X = 56;
        clone.ScriptVariableName = "clone";
        clone.ScreenX = 78;
        clone.ShellCommand = "clone command";
        clone.ScreenshotOutputPath = "clone.png";
        clone.WindowSelectorValue = "clone window";
        clone.ImageSearchMatchModeWasExplicit = false;
        clone.PreferLegacyScriptText = false;

        Assert.NotEqual(action.Id, clone.Id);
        Assert.Equal(12, action.X);
        Assert.Equal("source", action.ScriptVariableName);
        Assert.Equal(34, action.ScreenX);
        Assert.Equal("source command", action.ShellCommand);
        Assert.Equal("source.png", action.ScreenshotOutputPath);
        Assert.Equal("source window", action.WindowSelectorValue);
        Assert.True(action.ImageSearchMatchModeWasExplicit);
        Assert.True(action.PreferLegacyScriptText);
    }
}
