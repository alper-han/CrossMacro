
namespace CrossMacro.UI.Services.Playback;

internal static class PlaybackExecutionPlanner
{
    public static PlaybackExecutionPlan CreatePlan(ILoadedMacroSession loadedMacroSession)
    {
        ArgumentNullException.ThrowIfNull(loadedMacroSession);

        var mode = GetEffectivePlaybackMode(loadedMacroSession);
        if (mode is not LoadedMacroPlaybackMode.SequentialCycle)
        {
            return CreateSingleMacroPlan(mode, loadedMacroSession.SelectedMacro);
        }

        var sequenceSnapshot = loadedMacroSession.CreateSequentialCycleSnapshot();
        if (sequenceSnapshot.Count is 0)
        {
            return CreateSingleMacroPlan(LoadedMacroPlaybackMode.SelectedOnly, loadedMacroSession.SelectedMacro);
        }

        var macroWithoutEventsName = FindMacroWithoutEvents(sequenceSnapshot);
        if (macroWithoutEventsName is not null)
        {
            return new PlaybackExecutionPlan(mode, ActiveMacro: null, sequenceSnapshot, macroWithoutEventsName);
        }

        return new PlaybackExecutionPlan(mode, sequenceSnapshot[0].Macro, sequenceSnapshot, MacroWithoutEventsName: null);
    }

    public static MacroSequence? GetPreviewMacro(ILoadedMacroSession loadedMacroSession)
    {
        ArgumentNullException.ThrowIfNull(loadedMacroSession);

        if (GetEffectivePlaybackMode(loadedMacroSession) is not LoadedMacroPlaybackMode.SequentialCycle)
        {
            return loadedMacroSession.SelectedMacro;
        }

        if (loadedMacroSession.SelectedMacro is not null)
        {
            return loadedMacroSession.SelectedMacro;
        }

        return loadedMacroSession.Count > 0
            ? loadedMacroSession.LoadedMacros[0].Macro
            : null;
    }

    public static bool HasPlayableEvents(MacroSequence? macro)
    {
        return MacroPlayableActionCounter.HasPlayableActions(macro);
    }

    private static PlaybackExecutionPlan CreateSingleMacroPlan(LoadedMacroPlaybackMode mode, MacroSequence? activeMacro)
    {
        return new PlaybackExecutionPlan(mode, activeMacro, [], MacroWithoutEventsName: null);
    }

    private static LoadedMacroPlaybackMode GetEffectivePlaybackMode(ILoadedMacroSession loadedMacroSession)
    {
        return loadedMacroSession.Count is 0
            ? LoadedMacroPlaybackMode.SelectedOnly
            : loadedMacroSession.PlaybackMode;
    }

    private static string? FindMacroWithoutEvents(IReadOnlyList<LoadedMacroListItem> sequenceSnapshot)
    {
        var item = sequenceSnapshot.FirstOrDefault(candidate => !HasPlayableEvents(candidate.Macro));
        if (item is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(item.Name) ? MacroNameDefaults.UnnamedMacroName : item.Name;
    }
}
