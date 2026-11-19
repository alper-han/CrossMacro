namespace CrossMacro.UI.ViewModels.Playback;

/// <summary>Where a sequential (multi-macro) playback currently is; read to render the status line.</summary>
internal sealed class SequencePlaybackProgress
{
    internal bool IsActive { get; set; }
    internal bool IsWaitingBetweenCycles { get; set; }
    internal int MacroIndex { get; set; }
    internal int MacroCount { get; set; }
    internal int Cycle { get; set; }
    internal int TotalCycles { get; set; }
    internal string MacroName { get; set; } = string.Empty;
    internal int MacroRepeatCount { get; set; } = 1;

    internal void Reset()
    {
        IsActive = false;
        IsWaitingBetweenCycles = false;
        MacroIndex = 0;
        MacroCount = 0;
        Cycle = 0;
        TotalCycles = 0;
        MacroName = string.Empty;
        MacroRepeatCount = 1;
    }
}
