
namespace CrossMacro.UI.Tests.Services;

public sealed class PlaybackExecutionPlannerTests
{
    [Fact]
    public void CreatePlan_WhenSelectedOnly_UsesSelectedMacroWithoutSequence()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        var selected = new MacroSequence { Name = "Selected" };
        selected.ScriptSteps.Add("click left");
        _ = session.AddMacro(selected);
        session.PlaybackMode = LoadedMacroPlaybackMode.SelectedOnly;
        var fallback = new MacroSequence { Name = "Fallback" };

        var plan = PlaybackExecutionPlanner.CreatePlan(session, fallback);

        _ = plan.Mode.Should().Be(LoadedMacroPlaybackMode.SelectedOnly);
        _ = plan.ActiveMacro.Should().BeSameAs(selected);
        _ = plan.UsesSequence.Should().BeFalse();
        _ = plan.ValidationError.Should().BeNull();
    }

    [Fact]
    public void CreatePlan_WhenSequentialCycleContainsEmptyMacro_ReturnsValidationError()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        var playable = new MacroSequence { Name = "Playable" };
        playable.ScriptSteps.Add("click left");
        _ = session.AddMacro(playable);
        _ = session.AddMacro(new MacroSequence { Name = "Empty" });
        session.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var plan = PlaybackExecutionPlanner.CreatePlan(session, fallbackMacro: null);

        _ = plan.ActiveMacro.Should().BeNull();
        _ = plan.UsesSequence.Should().BeTrue();
        _ = plan.SequenceSnapshot.Should().HaveCount(2);
        _ = plan.ValidationError.Should().Contain("Empty");
    }

    [Fact]
    public void GetPreviewMacro_WhenSequentialCycleHasNoSelection_UsesFirstLoadedMacro()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        var first = new MacroSequence { Name = "First" };
        first.ScriptSteps.Add("click left");
        _ = session.AddMacro(first);
        _ = session.AddMacro(new MacroSequence { Name = "Second" });
        session.SelectedMacroItem = null;
        session.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var preview = PlaybackExecutionPlanner.GetPreviewMacro(session, fallbackMacro: null);

        _ = preview.Should().BeSameAs(first);
    }

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
            ScriptSteps = { scriptStep },
        };

        _ = PlaybackExecutionPlanner.HasPlayableEvents(macro).Should().BeTrue();
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
            ScriptSteps = { scriptStep },
        };

        _ = PlaybackExecutionPlanner.HasPlayableEvents(macro).Should().BeFalse();
    }
}
