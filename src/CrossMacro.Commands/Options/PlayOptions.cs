using CrossMacro.Core.Models.Playback;

namespace CrossMacro.Commands.Options;

public sealed record PlayOptions(
    string MacroFilePath,
    double SpeedMultiplier = 1.0,
    bool Loop = false,
    int RepeatCount = 1,
    int RepeatDelayMs = 0,
    MotionPlaybackMode MotionMode = MotionPlaybackMode.Precision,
    int StrictSpeedMotionEventsPerSecond = PlaybackOptions.DefaultStrictSpeedMotionEventsPerSecond,
    int PrecisionMotionEventsPerSecond = PlaybackOptions.DefaultPrecisionMotionEventsPerSecond,
    double MaximumMotionErrorPixels = PlaybackOptions.DefaultMaximumMotionErrorPixels,
    int CountdownSeconds = 0,
    int TimeoutSeconds = 0,
    bool DryRun = false,
    bool JsonOutput = false,
    string? LogLevel = null)
    : CommandOptions(JsonOutput, LogLevel);
