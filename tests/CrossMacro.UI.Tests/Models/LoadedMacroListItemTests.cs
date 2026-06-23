using CrossMacro.Core.Models;
using CrossMacro.UI.Models;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Models;

public sealed class LoadedMacroListItemTests
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
    public void EventCount_WhenOnlyScreenReadingScriptStep_CountsScriptStep(string scriptStep)
    {
        var item = new LoadedMacroListItem(new MacroSequence
        {
            Name = "Screen Reading Macro",
            ScriptSteps = [scriptStep]
        });

        item.EventCount.Should().Be(1);
    }

    [Fact]
    public void EventCount_WhenEventsAndMirroredScriptSteps_CountsEventsAndScreenReadingStepsOnly()
    {
        var item = new LoadedMacroListItem(new MacroSequence
        {
            Name = "Mixed Macro",
            Events = { new MacroEvent { Type = EventType.Click, Button = MouseButton.Left } },
            ScriptSteps =
            [
                "pixelcolor 10 20 color",
                "waitcolor 11 22 00FFAA 2500",
                "pixelsearch 0 0 3 3 123456 x y",
                "click left"
            ]
        });

        item.EventCount.Should().Be(4);
    }

    [Theory]
    [InlineData("click left")]
    [InlineData("set mode fast")]
    [InlineData("pixelcolorful 10 20 color")]
    public void EventCount_WhenScriptStepIsNotScreenReadingCommand_DoesNotCountScriptStep(string scriptStep)
    {
        var item = new LoadedMacroListItem(new MacroSequence
        {
            Name = "Script Macro",
            ScriptSteps = [scriptStep]
        });

        item.EventCount.Should().Be(0);
    }
}
