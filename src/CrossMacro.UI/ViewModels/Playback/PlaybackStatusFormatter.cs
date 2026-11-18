namespace CrossMacro.UI.ViewModels.Playback;

/// <summary>Renders the localized "what is playing right now" status line from player and sequence state.</summary>
internal sealed class PlaybackStatusFormatter(ILocalizationService localizationService)
{
    /// <param name="loopDelayWaitText">The text for the wait between loops or cycles; only used while waiting.</param>
    internal string Format(IMacroPlayer player, SequencePlaybackProgress sequence, string loopDelayWaitText)
    {
        return sequence.IsActive
            ? FormatSequence(player, sequence, loopDelayWaitText)
            : FormatSingleMacro(player, loopDelayWaitText);
    }

    private string FormatSequence(IMacroPlayer player, SequencePlaybackProgress sequence, string loopDelayWaitText)
    {
        if (sequence.IsWaitingBetweenCycles)
        {
            return string.Format(localizationService.CurrentCulture, localizationService["Playback_StatusWaitingNextSequence"], loopDelayWaitText);
        }

        var macroName = string.IsNullOrWhiteSpace(sequence.MacroName)
            ? localizationService["Playback_UnnamedMacro"]
            : sequence.MacroName;
        var macroIndex = Math.Max(1, sequence.MacroIndex);
        var macroCount = Math.Max(1, sequence.MacroCount);
        var cycleText = sequence.TotalCycles is 0
            ? string.Format(
                localizationService.CurrentCulture,
                localizationService["Playback_SequenceCycleInfinite"],
                Math.Max(1, sequence.Cycle))
            : $"{Math.Max(1, sequence.Cycle).ToString(CultureInfo.InvariantCulture)}/{Math.Max(1, sequence.TotalCycles).ToString(CultureInfo.InvariantCulture)}";
        var repeatCount = Math.Max(1, sequence.MacroRepeatCount);
        var repeatText = string.Empty;

        if (repeatCount > 1)
        {
            var currentRepeat = player.TotalLoops == repeatCount
                ? Math.Clamp(Math.Max(1, player.CurrentLoop), 1, repeatCount)
                : 1;
            repeatText = string.Format(
                localizationService.CurrentCulture,
                localizationService["Playback_SequenceRepeatProgress"],
                currentRepeat,
                repeatCount);
        }

        return string.Format(localizationService.CurrentCulture, localizationService["Playback_StatusSequencePlaying"], macroName, macroIndex, macroCount, repeatText, cycleText);
    }

    private string FormatSingleMacro(IMacroPlayer player, string loopDelayWaitText)
    {
        var currentLoop = player.CurrentLoop;
        var totalLoops = player.TotalLoops;

        if (player.IsWaitingBetweenLoops)
        {
            return string.Format(localizationService.CurrentCulture, localizationService["Playback_StatusWaitingNextLoop"], loopDelayWaitText);
        }

        if (totalLoops is 0)
        {
            return string.Format(localizationService.CurrentCulture, localizationService["Playback_StatusLoopInfinite"], currentLoop);
        }

        if (totalLoops > 1)
        {
            return string.Format(localizationService.CurrentCulture, localizationService["Playback_StatusLoopProgress"], currentLoop, totalLoops);
        }

        return localizationService["Playback_StatusPlaying"];
    }
}
