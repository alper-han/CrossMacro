using System;
using System.Collections.Generic;
using CrossMacro.Core.Models;
using CrossMacro.UI.Models;

namespace CrossMacro.UI.Services;

internal static class PlaybackExecutionPlanner
{
    public static PlaybackExecutionPlan CreatePlan(ILoadedMacroSession loadedMacroSession, MacroSequence? fallbackMacro)
    {
        ArgumentNullException.ThrowIfNull(loadedMacroSession);

        var mode = GetEffectivePlaybackMode(loadedMacroSession);
        if (mode != LoadedMacroPlaybackMode.SequentialCycle)
        {
            return CreateSingleMacroPlan(mode, loadedMacroSession.SelectedMacro ?? fallbackMacro);
        }

        var sequenceSnapshot = loadedMacroSession.CreateSequentialCycleSnapshot();
        if (sequenceSnapshot.Count == 0)
        {
            return CreateSingleMacroPlan(LoadedMacroPlaybackMode.SelectedOnly, loadedMacroSession.SelectedMacro ?? fallbackMacro);
        }

        if (!TryValidateSequenceSnapshot(sequenceSnapshot, out var validationError))
        {
            return new PlaybackExecutionPlan(mode, null, sequenceSnapshot, validationError);
        }

        return new PlaybackExecutionPlan(mode, sequenceSnapshot[0].Macro, sequenceSnapshot, null);
    }

    public static MacroSequence? GetPreviewMacro(ILoadedMacroSession loadedMacroSession, MacroSequence? fallbackMacro)
    {
        ArgumentNullException.ThrowIfNull(loadedMacroSession);

        if (GetEffectivePlaybackMode(loadedMacroSession) != LoadedMacroPlaybackMode.SequentialCycle)
        {
            return loadedMacroSession.SelectedMacro ?? fallbackMacro;
        }

        if (loadedMacroSession.SelectedMacro != null)
        {
            return loadedMacroSession.SelectedMacro;
        }

        return loadedMacroSession.Count > 0
            ? loadedMacroSession.LoadedMacros[0].Macro
            : fallbackMacro;
    }

    public static bool HasPlayableEvents(MacroSequence? macro)
    {
        return (macro?.Events?.Count ?? 0) > 0;
    }

    private static PlaybackExecutionPlan CreateSingleMacroPlan(LoadedMacroPlaybackMode mode, MacroSequence? activeMacro)
    {
        return new PlaybackExecutionPlan(mode, activeMacro, Array.Empty<LoadedMacroListItem>(), null);
    }

    private static LoadedMacroPlaybackMode GetEffectivePlaybackMode(ILoadedMacroSession loadedMacroSession)
    {
        return loadedMacroSession.Count == 0
            ? LoadedMacroPlaybackMode.SelectedOnly
            : loadedMacroSession.PlaybackMode;
    }

    private static bool TryValidateSequenceSnapshot(
        IReadOnlyList<LoadedMacroListItem> sequenceSnapshot,
        out string validationError)
    {
        foreach (var item in sequenceSnapshot)
        {
            if (HasPlayableEvents(item.Macro))
            {
                continue;
            }

            var itemName = string.IsNullOrWhiteSpace(item.Name) ? MacroNameDefaults.UnnamedMacroName : item.Name;
            validationError = $"Playback error: loaded macro '{itemName}' has no events";
            return false;
        }

        validationError = string.Empty;
        return true;
    }
}
