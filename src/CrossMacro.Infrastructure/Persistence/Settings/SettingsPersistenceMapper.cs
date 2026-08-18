namespace CrossMacro.Infrastructure.Persistence.Settings;

/// <summary>
/// Maps the application settings aggregate to Infrastructure-owned JSON documents.
/// The public Core <see cref="Core.Models.SettingsMapper"/> remains available as a
/// source-compatibility facade for callers that still use the legacy DTO types.
/// </summary>
internal static class SettingsPersistenceMapper
{
    public static PersistedGlobalSettings ToGlobal(AppSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new PersistedGlobalSettings
        {
            EnableTrayIcon = source.EnableTrayIcon,
            StartMinimized = source.StartMinimized,
            SuppressFastLoopWarning = source.SuppressFastLoopWarning,
            LogLevel = source.LogLevel,
            Theme = source.Theme,
            Language = source.Language,
        };
    }

    public static PersistedProfileSettings ToProfile(AppSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var profile = new PersistedProfileSettings
        {
            PlaybackSpeed = source.PlaybackSpeed,
            IsLooping = source.IsLooping,
            LoopCount = source.LoopCount,
            LoopDelayMs = source.LoopDelayMs,
            UseRandomLoopDelay = source.UseRandomLoopDelay,
            LoopDelayMinMs = source.LoopDelayMinMs,
            LoopDelayMaxMs = source.LoopDelayMaxMs,
            MotionMode = source.MotionMode,
            StrictSpeedMotionEventsPerSecond = source.StrictSpeedMotionEventsPerSecond,
            PrecisionMotionEventsPerSecond = source.PrecisionMotionEventsPerSecond,
            MaximumMotionErrorPixels = source.MaximumMotionErrorPixels,
            CountdownSeconds = source.CountdownSeconds,
            IsMouseRecordingEnabled = source.IsMouseRecordingEnabled,
            IsKeyboardRecordingEnabled = source.IsKeyboardRecordingEnabled,
            ForceRelativeCoordinates = source.ForceRelativeCoordinates,
            SkipInitialZeroZero = source.SkipInitialZeroZero,
            EnableTextExpansion = source.EnableTextExpansion,
            CheckForUpdates = source.CheckForUpdates,
        };
        Normalize(profile);
        return profile;
    }

    public static AppSettings Combine(PersistedGlobalSettings global, PersistedProfileSettings profile)
    {
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(profile);

        var settings = new AppSettings
        {
            EnableTrayIcon = global.EnableTrayIcon,
            StartMinimized = global.StartMinimized,
            SuppressFastLoopWarning = global.SuppressFastLoopWarning,
            LogLevel = global.LogLevel,
            Theme = global.Theme,
            Language = global.Language,
            PlaybackSpeed = profile.PlaybackSpeed,
            IsLooping = profile.IsLooping,
            LoopCount = profile.LoopCount,
            LoopDelayMs = profile.LoopDelayMs,
            UseRandomLoopDelay = profile.UseRandomLoopDelay,
            LoopDelayMinMs = profile.LoopDelayMinMs,
            LoopDelayMaxMs = profile.LoopDelayMaxMs,
            MotionMode = profile.MotionMode,
            StrictSpeedMotionEventsPerSecond = profile.StrictSpeedMotionEventsPerSecond,
            PrecisionMotionEventsPerSecond = profile.PrecisionMotionEventsPerSecond,
            MaximumMotionErrorPixels = profile.MaximumMotionErrorPixels,
            CountdownSeconds = profile.CountdownSeconds,
            IsMouseRecordingEnabled = profile.IsMouseRecordingEnabled,
            IsKeyboardRecordingEnabled = profile.IsKeyboardRecordingEnabled,
            ForceRelativeCoordinates = profile.ForceRelativeCoordinates,
            SkipInitialZeroZero = profile.SkipInitialZeroZero,
            EnableTextExpansion = profile.EnableTextExpansion,
            CheckForUpdates = profile.CheckForUpdates,
        };
        settings.Normalize();
        return settings;
    }

    public static void ApplyGlobal(AppSettings target, PersistedGlobalSettings global)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(global);

        target.EnableTrayIcon = global.EnableTrayIcon;
        target.StartMinimized = global.StartMinimized;
        target.SuppressFastLoopWarning = global.SuppressFastLoopWarning;
        target.LogLevel = global.LogLevel;
        target.Theme = global.Theme;
        target.Language = global.Language;
    }

    public static void ApplyProfile(AppSettings target, PersistedProfileSettings profile)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(profile);

        target.PlaybackSpeed = profile.PlaybackSpeed;
        target.IsLooping = profile.IsLooping;
        target.LoopCount = profile.LoopCount;
        target.LoopDelayMs = profile.LoopDelayMs;
        target.UseRandomLoopDelay = profile.UseRandomLoopDelay;
        target.LoopDelayMinMs = profile.LoopDelayMinMs;
        target.LoopDelayMaxMs = profile.LoopDelayMaxMs;
        target.MotionMode = profile.MotionMode;
        target.StrictSpeedMotionEventsPerSecond = profile.StrictSpeedMotionEventsPerSecond;
        target.PrecisionMotionEventsPerSecond = profile.PrecisionMotionEventsPerSecond;
        target.MaximumMotionErrorPixels = profile.MaximumMotionErrorPixels;
        target.CountdownSeconds = profile.CountdownSeconds;
        target.IsMouseRecordingEnabled = profile.IsMouseRecordingEnabled;
        target.IsKeyboardRecordingEnabled = profile.IsKeyboardRecordingEnabled;
        target.ForceRelativeCoordinates = profile.ForceRelativeCoordinates;
        target.SkipInitialZeroZero = profile.SkipInitialZeroZero;
        target.EnableTextExpansion = profile.EnableTextExpansion;
        target.CheckForUpdates = profile.CheckForUpdates;
        target.Normalize();
    }

    private static void Normalize(PersistedProfileSettings profile)
    {
        profile.PlaybackSpeed = PlaybackOptions.NormalizeSpeedMultiplier(profile.PlaybackSpeed);
        profile.LoopDelayMs = PlaybackOptions.NormalizeDelayMs(profile.LoopDelayMs);
        (profile.LoopDelayMinMs, profile.LoopDelayMaxMs) = PlaybackOptions.NormalizeDelayRange(
            profile.LoopDelayMinMs,
            profile.LoopDelayMaxMs);
        profile.MotionMode = Enum.IsDefined(profile.MotionMode)
            ? profile.MotionMode
            : MotionPlaybackMode.Precision;
        profile.StrictSpeedMotionEventsPerSecond = PlaybackOptions.NormalizeStrictSpeedMotionEventsPerSecond(
            profile.StrictSpeedMotionEventsPerSecond);
        profile.PrecisionMotionEventsPerSecond = PlaybackOptions.NormalizePrecisionMotionEventsPerSecond(
            profile.PrecisionMotionEventsPerSecond);
        profile.MaximumMotionErrorPixels = PlaybackOptions.NormalizeMaximumMotionErrorPixels(
            profile.MaximumMotionErrorPixels);
    }
}
