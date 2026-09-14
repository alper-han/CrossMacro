namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class PlaybackRequirementsTests
{
    [Theory]
    [InlineData("clipboard get value", true, false, false, false)]
    [InlineData("window active title value", true, false, false, false)]
    [InlineData("key down 30", true, false, true, false)]
    [InlineData("move abs 1 2", false, false, true, true)]
    [InlineData("  MoVe rel-logical 1 2", false, false, true, true)]
    [InlineData("imageclick icon", true, true, true, true)]
    [InlineData("pixelcolor 1 2 value", true, true, false, false)]
    [InlineData("delay 1", true, false, false, false)]
    public void Analyze_ClassifiesRuntimeResourcesOnce(string step, bool runtime, bool screen, bool input, bool absoluteDevice)
    {
        var macro = new MacroSequence { SkipInitialZeroZero = true, ScriptSteps = { step } };

        var requirements = PlaybackRequirements.Analyze(macro);

        Assert.Equal(runtime, requirements.HasRuntimeSteps);
        Assert.Equal(runtime, requirements.HasOnlyRuntimeSteps);
        Assert.Equal(screen, requirements.HasScreenReadingSteps);
        Assert.Equal(input, requirements.RequiresRuntimeInput);
        Assert.Equal(absoluteDevice, requirements.RequiresAbsoluteDevice);
    }

    [Fact]
    public void Analyze_LogicalRelativeEventsAllowCooperation_UntilRuntimeStepsArePresent()
    {
        var macro = new MacroSequence
        {
            SkipInitialZeroZero = true,
            Events = { new MacroEvent { Type = EventType.MouseMove, CoordinateMode = MouseCoordinateMode.Relative, CoordinateSpace = MouseCoordinateSpace.LogicalDesktop, X = 2 } },
        };

        var relativeOnly = PlaybackRequirements.Analyze(macro);
        macro.ScriptSteps.Add("pixelcolor 1 2 sampled");
        var mixed = PlaybackRequirements.Analyze(macro);

        Assert.True(relativeOnly.RequiresAbsoluteDevice);
        Assert.True(relativeOnly.AllowsCooperativeLogicalRelativeMovement);
        Assert.False(relativeOnly.RequiresInitialReanchor);
        Assert.False(mixed.AllowsCooperativeLogicalRelativeMovement);
        Assert.True(mixed.RequiresInitialReanchor);
        Assert.False(relativeOnly.HasRuntimeSteps);
    }

    [Fact]
    public void Analyze_MixedStaticAndRuntimeScript_IsNotRuntimeOnly()
    {
        var macro = new MacroSequence { ScriptSteps = { "pixelcolor 1 2 sampled", "click left", " " } };

        var requirements = PlaybackRequirements.Analyze(macro);

        Assert.True(requirements.HasRuntimeSteps);
        Assert.False(requirements.HasOnlyRuntimeSteps);
        Assert.True(requirements.RequiresRuntimeInput);
    }
}
