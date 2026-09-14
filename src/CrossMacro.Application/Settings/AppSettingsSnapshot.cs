namespace CrossMacro.Application.Settings;

/// <summary>Copies settings without sharing mutable security state or using reflection.</summary>
public static class AppSettingsSnapshot
{
    internal static IReadOnlyList<SettingsField> Fields { get; } = CreateFields();

    public static AppSettings Copy(AppSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new AppSettings();
        foreach (var descriptor in Fields) { descriptor.Copy(source, result); }
        return result;
    }

    private static IReadOnlyList<SettingsField> CreateFields()
    {
        var fields = new List<SettingsField>
        {
            new TypedSettingsField<bool>(nameof(AppSettings.EnableTrayIcon), settings => settings.EnableTrayIcon, (settings, value) => settings.EnableTrayIcon = value),
            new TypedSettingsField<bool>(nameof(AppSettings.StartMinimized), settings => settings.StartMinimized, (settings, value) => settings.StartMinimized = value),
            new TypedSettingsField<bool>(nameof(AppSettings.HideToTrayOnPlayback), settings => settings.HideToTrayOnPlayback, (settings, value) => settings.HideToTrayOnPlayback = value),
            new TypedSettingsField<bool>(nameof(AppSettings.HideToTrayOnRecording), settings => settings.HideToTrayOnRecording, (settings, value) => settings.HideToTrayOnRecording = value),
            new TypedSettingsField<bool>(nameof(AppSettings.SuppressFastLoopWarning), settings => settings.SuppressFastLoopWarning, (settings, value) => settings.SuppressFastLoopWarning = value),
            new TypedSettingsField<bool>(nameof(AppSettings.MacOSScreenRecordingOnboardingCompleted), settings => settings.MacOSScreenRecordingOnboardingCompleted, (settings, value) => settings.MacOSScreenRecordingOnboardingCompleted = value),
            new TypedSettingsField<double>(nameof(AppSettings.PlaybackSpeed), settings => settings.PlaybackSpeed, (settings, value) => settings.PlaybackSpeed = value),
            new TypedSettingsField<bool>(nameof(AppSettings.IsLooping), settings => settings.IsLooping, (settings, value) => settings.IsLooping = value),
            new TypedSettingsField<int>(nameof(AppSettings.LoopCount), settings => settings.LoopCount, (settings, value) => settings.LoopCount = value),
            new TypedSettingsField<int>(nameof(AppSettings.LoopDelayMs), settings => settings.LoopDelayMs, (settings, value) => settings.LoopDelayMs = value),
            new TypedSettingsField<bool>(nameof(AppSettings.UseRandomLoopDelay), settings => settings.UseRandomLoopDelay, (settings, value) => settings.UseRandomLoopDelay = value),
            new TypedSettingsField<int>(nameof(AppSettings.LoopDelayMinMs), settings => settings.LoopDelayMinMs, (settings, value) => settings.LoopDelayMinMs = value),
            new TypedSettingsField<int>(nameof(AppSettings.LoopDelayMaxMs), settings => settings.LoopDelayMaxMs, (settings, value) => settings.LoopDelayMaxMs = value),
            new TypedSettingsField<MotionPlaybackMode>(nameof(AppSettings.MotionMode), settings => settings.MotionMode, (settings, value) => settings.MotionMode = value),
            new TypedSettingsField<int>(nameof(AppSettings.StrictSpeedMotionEventsPerSecond), settings => settings.StrictSpeedMotionEventsPerSecond, (settings, value) => settings.StrictSpeedMotionEventsPerSecond = value),
            new TypedSettingsField<int>(nameof(AppSettings.PrecisionMotionEventsPerSecond), settings => settings.PrecisionMotionEventsPerSecond, (settings, value) => settings.PrecisionMotionEventsPerSecond = value),
            new TypedSettingsField<double>(nameof(AppSettings.MaximumMotionErrorPixels), settings => settings.MaximumMotionErrorPixels, (settings, value) => settings.MaximumMotionErrorPixels = value),
            new TypedSettingsField<int>(nameof(AppSettings.CountdownSeconds), settings => settings.CountdownSeconds, (settings, value) => settings.CountdownSeconds = value),
            new TypedSettingsField<bool>(nameof(AppSettings.IsMouseRecordingEnabled), settings => settings.IsMouseRecordingEnabled, (settings, value) => settings.IsMouseRecordingEnabled = value),
            new TypedSettingsField<bool>(nameof(AppSettings.IsKeyboardRecordingEnabled), settings => settings.IsKeyboardRecordingEnabled, (settings, value) => settings.IsKeyboardRecordingEnabled = value),
            new TypedSettingsField<bool>(nameof(AppSettings.ForceRelativeCoordinates), settings => settings.ForceRelativeCoordinates, (settings, value) => settings.ForceRelativeCoordinates = value),
            new TypedSettingsField<bool>(nameof(AppSettings.UseLogicalRelativeCoordinates), settings => settings.UseLogicalRelativeCoordinates, (settings, value) => settings.UseLogicalRelativeCoordinates = value),
            new TypedSettingsField<bool>(nameof(AppSettings.SkipInitialZeroZero), settings => settings.SkipInitialZeroZero, (settings, value) => settings.SkipInitialZeroZero = value),
            new TypedSettingsField<bool>(nameof(AppSettings.EnableTextExpansion), settings => settings.EnableTextExpansion, (settings, value) => settings.EnableTextExpansion = value),
            new TypedSettingsField<bool>(nameof(AppSettings.CheckForUpdates), settings => settings.CheckForUpdates, (settings, value) => settings.CheckForUpdates = value),
            new TypedSettingsField<string>(nameof(AppSettings.LogLevel), settings => settings.LogLevel, (settings, value) => settings.LogLevel = value),
            new TypedSettingsField<string>(nameof(AppSettings.Theme), settings => settings.Theme, (settings, value) => settings.Theme = value),
            new TypedSettingsField<string>(nameof(AppSettings.Language), settings => settings.Language, (settings, value) => settings.Language = value),
            new TypedSettingsField<int>("McpSecurity.ApprovalTimeoutSeconds", settings => settings.McpSecurity.ApprovalTimeoutSeconds, (settings, value) => settings.McpSecurity.ApprovalTimeoutSeconds = value),
        };
        foreach (var setting in Enum.GetValues<McpSecuritySetting>())
        {
            fields.Add(new TypedSettingsField<bool>($"McpSecurity.{setting}", settings => settings.McpSecurity.IsAllowed(setting), (settings, value) => settings.McpSecurity.Set(setting, value)));
        }
        foreach (var setting in Enum.GetValues<McpPathSetting>())
        {
            fields.Add(new TypedSettingsField<IReadOnlyList<string>>($"McpSecurity.Paths.{setting}", settings => settings.McpSecurity.Paths.GetRoots(setting),
                (settings, value) => settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(setting, value),
                (left, right) => left.SequenceEqual(right, StringComparer.Ordinal)));
        }
        return fields.AsReadOnly();
    }
}
