namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>Immutable resource and coordinate requirements captured once for a playback run.</summary>
internal sealed record PlaybackRequirements(
    bool HasRuntimeSteps,
    bool HasOnlyRuntimeSteps,
    bool HasScreenReadingSteps,
    bool RequiresRuntimeInput,
    bool RequiresAbsoluteCoordinates,
    bool RequiresAbsoluteDevice,
    bool AllowsCooperativeLogicalRelativeMovement)
{
    public static PlaybackRequirements Empty { get; } = new(HasRuntimeSteps: false, HasOnlyRuntimeSteps: false,
        HasScreenReadingSteps: false, RequiresRuntimeInput: false, RequiresAbsoluteCoordinates: false,
        RequiresAbsoluteDevice: false, AllowsCooperativeLogicalRelativeMovement: false);
    public bool RequiresInitialReanchor => RequiresAbsoluteCoordinates && !AllowsCooperativeLogicalRelativeMovement;

    public static PlaybackRequirements Analyze(MacroSequence macro)
    {
        var hasRuntime = false;
        var onlyRuntime = macro.ScriptSteps.Count > 0;
        var screenReading = false;
        var input = false;
        var logicalMove = false;
        var imageClick = false;
        foreach (var step in macro.ScriptSteps)
        {
            var requirements = RunScriptRuntimeStepClassifier.GetRequirements(step);
            hasRuntime |= requirements.IsRuntime;
            if (!string.IsNullOrWhiteSpace(step))
            {
                onlyRuntime &= requirements.IsRuntime;
            }
            screenReading |= requirements.ReadsScreen;
            input |= requirements.RequiresInput;
            logicalMove |= requirements.MovesInLogicalDesktop;
            imageClick |= requirements.ClicksImage;
        }

        var logicalEvents = MacroPositionSemantics.HasAnyLogicalDesktopCoordinateEvents(macro);
        var cooperative = !hasRuntime && macro.Events.Count > 0 && macro.Events.All(item =>
            item.Type is EventType.None || (item.Type is EventType.MouseMove
                && MacroPositionSemantics.ResolveCoordinateMode(item, macro.IsAbsoluteCoordinates) is MouseCoordinateMode.Relative
                && MacroPositionSemantics.ResolveCoordinateSpace(item, macro.IsAbsoluteCoordinates) is MouseCoordinateSpace.LogicalDesktop));
        var absolute = logicalEvents || logicalMove;
        return new PlaybackRequirements(
            HasRuntimeSteps: hasRuntime,
            HasOnlyRuntimeSteps: onlyRuntime,
            HasScreenReadingSteps: screenReading,
            RequiresRuntimeInput: input,
            RequiresAbsoluteCoordinates: absolute,
            RequiresAbsoluteDevice: absolute || imageClick || MacroPositionSemantics.RequiresInitialCornerReset(macro),
            AllowsCooperativeLogicalRelativeMovement: cooperative);
    }
}
