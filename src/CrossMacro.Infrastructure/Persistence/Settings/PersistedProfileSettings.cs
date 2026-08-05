using System.ComponentModel;

namespace CrossMacro.Infrastructure.Persistence.Settings;

/// <summary>
/// Infrastructure-owned representation of profiles/{id}/settings.json.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PersistedProfileSettings
{
    public double PlaybackSpeed { get; set; } = PlaybackOptions.DefaultSpeedMultiplier;

    public bool IsLooping { get; set; }

    public int LoopCount { get; set; } = 1;

    public int LoopDelayMs { get; set; } = PlaybackOptions.DefaultDelayMs;

    public bool UseRandomLoopDelay { get; set; }

    public int LoopDelayMinMs { get; set; } = PlaybackOptions.DefaultDelayMs;

    public int LoopDelayMaxMs { get; set; } = PlaybackOptions.DefaultDelayMs;

    public int CountdownSeconds { get; set; }

    public bool IsMouseRecordingEnabled { get; set; } = true;

    public bool IsKeyboardRecordingEnabled { get; set; } = true;

    public bool ForceRelativeCoordinates { get; set; }

    public bool SkipInitialZeroZero { get; set; }

    public bool EnableTextExpansion { get; set; }

    public bool CheckForUpdates { get; set; }
}
