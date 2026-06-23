using CrossMacro.Core.Models;
using CrossMacro.UI.Services;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Services;

public sealed class PlaybackExecutionPlannerTests
{
    [Theory]
    [InlineData("  PIXELCOLOR 10 20 color")]
    [InlineData("pixelcolor 10 20 color")]
    [InlineData("pixelcolor rel 1 2")]
    [InlineData("waitcolor 11 22 00FFAA 2500")]
    [InlineData("waitcolor 11 22 00FFAA")]
    [InlineData("pixelsearch 0 0 3 3 123456")]
    [InlineData("pixelsearch 0 0 3 3 123456 x y")]
    [InlineData("pixelsearch 0 0 3 3 123456 tolerance 10")]
    public void HasPlayableEvents_WhenScreenReadingScriptStepIsPresent_ReturnsTrue(string scriptStep)
    {
        var macro = new MacroSequence
        {
            Name = "Screen Reading Macro",
            ScriptSteps = [scriptStep]
        };

        PlaybackExecutionPlanner.HasPlayableEvents(macro).Should().BeTrue();
    }

    [Theory]
    [InlineData("click left")]
    [InlineData("set mode fast")]
    [InlineData("pixelcolorful 10 20 color")]
    public void HasPlayableEvents_WhenNoEventsAndNoScreenReadingCommand_ReturnsFalse(string scriptStep)
    {
        var macro = new MacroSequence
        {
            Name = "Script Macro",
            ScriptSteps = [scriptStep]
        };

        PlaybackExecutionPlanner.HasPlayableEvents(macro).Should().BeFalse();
    }
}
