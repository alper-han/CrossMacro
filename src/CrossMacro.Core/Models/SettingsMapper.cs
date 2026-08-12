namespace CrossMacro.Core.Models;

/// <summary>
/// Provides mapping between the in-memory AppSettings aggregate and
/// the persistence DTOs (GlobalSettings and ProfileSettings).
/// </summary>
public static class SettingsMapper
{
    /// <summary>
    /// Extracts global fields from an AppSettings instance.
    /// </summary>
    public static GlobalSettings ToGlobal(AppSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new GlobalSettings
        {
            EnableTrayIcon = source.EnableTrayIcon,
            StartMinimized = source.StartMinimized,
            LogLevel = source.LogLevel,
            Theme = source.Theme,
            Language = source.Language,
        };
    }

    /// <summary>
    /// Extracts profile-specific fields from an AppSettings instance.
    /// </summary>
    public static ProfileSettings ToProfile(AppSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var profile = new ProfileSettings
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
        profile.Normalize();
        return profile;
    }

    /// <summary>
    /// Combines global and profile settings into a single AppSettings aggregate.
    /// </summary>
    public static AppSettings Combine(GlobalSettings global, ProfileSettings profile)
    {
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(profile);

        var settings = new AppSettings
        {
            // Global fields
            EnableTrayIcon = global.EnableTrayIcon,
            StartMinimized = global.StartMinimized,
            LogLevel = global.LogLevel,
            Theme = global.Theme,
            Language = global.Language,
            // Profile fields
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

    /// <summary>
    /// Applies global settings onto an existing AppSettings instance.
    /// </summary>
    public static void ApplyGlobal(AppSettings target, GlobalSettings global)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(global);

        target.EnableTrayIcon = global.EnableTrayIcon;
        target.StartMinimized = global.StartMinimized;
        target.LogLevel = global.LogLevel;
        target.Theme = global.Theme;
        target.Language = global.Language;
    }

    /// <summary>
    /// Applies profile settings onto an existing AppSettings instance.
    /// </summary>
    public static void ApplyProfile(AppSettings target, ProfileSettings profile)
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
}
