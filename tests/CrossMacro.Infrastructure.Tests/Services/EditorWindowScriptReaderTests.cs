using CrossMacro.Infrastructure.Services.Editing;
using CrossMacro.Infrastructure.Services.Playback;

namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class EditorWindowScriptReaderTests
{
    [Theory]
    [InlineData("window active title $result")]
    [InlineData("window active \"title\" $result")]
    [InlineData("window active title \"$result\"")]
    [InlineData("window search \"title\" \"Browser window\" \"$result\"")]
    [InlineData("window wait \"class\" \"Browser window\" \"5000\" \"$result\"")]
    [InlineData("window focus \"title\" \"Browser window\"")]
    [InlineData("window close \"address\" \"0x123\"")]
    [InlineData("window move \"12\" \"34\"")]
    [InlineData("window resize \"800\" \"600\"")]
    [InlineData("window getdesktop \"$result\"")]
    [InlineData("window setdesktop \"Work space\"")]
    [InlineData("window setdesktopforwindow \"active\" \"Work space\"")]
    [InlineData("window setdesktopforwindow \"address\" \"0x123\" \"Work space\"")]
    public void RuntimeAcceptedQuotedTokens_RestoreValidEditorAction(string command)
    {
        Assert.Null(RunScriptWindowExecutor.Validate(command));
        Assert.True(EditorWindowScriptReader.TryParseWindowStep(command, out var action));
        Assert.True(action.IsValid());
        var written = EditorScriptWriter.BuildWindowStep(action);
        Assert.Null(RunScriptWindowExecutor.Validate(written));
        Assert.True(EditorWindowScriptReader.TryParseWindowStep(written, out var restored));
        Assert.True(action.TryGetWindowPayload(out var originalPayload));
        Assert.True(restored.TryGetWindowPayload(out var restoredPayload));
        Assert.Equal(originalPayload, restoredPayload);
    }

    [Fact]
    public void Search_RestoresQuotedSelectorAndEscapedTerm()
    {
        Assert.True(EditorWindowScriptReader.TryParseWindowStep(
            "window search \"title\" \"Browser \\\"work\\\"\" \"$result\"", out var action));
        Assert.Equal("title", action.WindowSelectorKind);
        Assert.Equal("Browser \"work\"", action.WindowSelectorValue);
        Assert.Equal("result", action.WindowOutputVariable);
    }
}
