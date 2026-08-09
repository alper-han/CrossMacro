
namespace CrossMacro.Cli.Services;

public sealed class SettingsCliService(ISettingsService settingsService) : ISettingsCliService
{
    private static readonly string[] AllowedLogLevels = ["Debug", "Information", "Warning", "Error"];

    private readonly ISettingsService _settingsService = settingsService;

    public async Task<SettingsCommandResult> GetAsync(string? key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var settings = await _settingsService.LoadAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(key))
        {
            return new SettingsCommandResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Settings loaded.",
                Data = BuildSettingsDictionary(settings),
            };
        }

        if (!TryGetValue(settings, key, out var value))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Unknown settings key.",
                Errors = [$"Unknown key: {key}", $"Available keys: {string.Join(", ", SupportedKeys)}"],
            };
        }

        return new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = $"{key}={value}",
            Data = new SettingsValueData(key, value),
        };
    }

    public async Task<SettingsCommandResult> SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Missing settings key.",
            };
        }

        if (value is null)
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Missing settings value.",
            };
        }

        var settings = await _settingsService.LoadAsync().ConfigureAwait(false);

        if (!TryGetValue(settings, key, out var beforeValue))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Unknown settings key.",
                Errors = [$"Unknown key: {key}", $"Available keys: {string.Join(", ", SupportedKeys)}"],
            };
        }

        if (!TrySetValue(settings, key, value, out var errorMessage))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Invalid settings value.",
                Errors = [errorMessage],
            };
        }

        try
        {
            await _settingsService.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Failed to save settings.",
                Errors = [ex.Message],
            };
        }

        _ = TryGetValue(settings, key, out var afterValue);

        return new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = $"{key} updated.",
            Data = new SettingsMutationData(key, beforeValue, afterValue),
        };
    }

    public Task<SettingsCommandResult> ListKeysAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = "Supported settings keys loaded.",
            Data = SupportedKeys.ToList(),
        });
    }

    public async Task<SettingsCommandResult> ResetAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Missing settings key.",
            };
        }

        var settings = await _settingsService.LoadAsync().ConfigureAwait(false);
        if (!TryGetValue(settings, key, out var beforeValue))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Unknown settings key.",
                Errors = [$"Unknown key: {key}", $"Available keys: {string.Join(", ", SupportedKeys)}"],
            };
        }

        var defaults = new AppSettings();
        if (!TryResetValue(settings, defaults, key, out var errorMessage))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Settings key cannot be reset.",
                Errors = [errorMessage],
            };
        }

        try
        {
            await _settingsService.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Failed to save settings.",
                Errors = [ex.Message],
            };
        }

        _ = TryGetValue(settings, key, out var afterValue);

        return new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = $"{key} reset.",
            Data = new SettingsMutationData(key, beforeValue, afterValue),
        };
    }

    public static IReadOnlyList<string> SupportedKeys { get; } =
    [
            "playback.speed",
            "playback.loop",
            "playback.loopCount",
            "playback.loopDelayMs",
            "playback.motionMode",
            "playback.strictSpeedMotionEventsPerSecond",
            "playback.precisionMotionEventsPerSecond",
            "playback.maximumMotionErrorPixels",
            "playback.countdownSeconds",
            "logging.level",
            "recording.mouse",
            "recording.keyboard",
            "recording.forceRelative",
            "recording.skipInitialZeroZero",
            "textExpansion.enabled",
            "ui.theme",
            "ui.language",
            "ui.trayIcon",
            "ui.startMinimized",
            "updates.checkForUpdates",
            "screen.portalRestoreToken",
    ];

    private static Dictionary<string, object?> BuildSettingsDictionary(AppSettings settings)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["playback.speed"] = settings.PlaybackSpeed,
            ["playback.loop"] = settings.IsLooping,
            ["playback.loopCount"] = settings.LoopCount,
            ["playback.loopDelayMs"] = settings.LoopDelayMs,
            ["playback.motionMode"] = settings.MotionMode.ToString(),
            ["playback.strictSpeedMotionEventsPerSecond"] = settings.StrictSpeedMotionEventsPerSecond,
            ["playback.precisionMotionEventsPerSecond"] = settings.PrecisionMotionEventsPerSecond,
            ["playback.maximumMotionErrorPixels"] = settings.MaximumMotionErrorPixels,
            ["playback.countdownSeconds"] = settings.CountdownSeconds,
            ["logging.level"] = settings.LogLevel,
            ["recording.mouse"] = settings.IsMouseRecordingEnabled,
            ["recording.keyboard"] = settings.IsKeyboardRecordingEnabled,
            ["recording.forceRelative"] = settings.ForceRelativeCoordinates,
            ["recording.skipInitialZeroZero"] = settings.SkipInitialZeroZero,
            ["textExpansion.enabled"] = settings.EnableTextExpansion,
            ["ui.theme"] = settings.Theme,
            ["ui.language"] = settings.Language,
            ["ui.trayIcon"] = settings.EnableTrayIcon,
            ["ui.startMinimized"] = settings.StartMinimized,
            ["updates.checkForUpdates"] = settings.CheckForUpdates,
            ["screen.portalRestoreToken"] = string.IsNullOrEmpty(settings.PortalScreenCastRestoreToken) ? "empty" : "set",
        };
    }

    private static bool TryGetValue(AppSettings settings, string key, out object? value)
    {
        switch (key)
        {
            case "playback.speed":
                value = settings.PlaybackSpeed;
                return true;
            case "playback.loop":
                value = settings.IsLooping;
                return true;
            case "playback.loopCount":
                value = settings.LoopCount;
                return true;
            case "playback.loopDelayMs":
                value = settings.LoopDelayMs;
                return true;
            case "playback.motionMode":
                value = settings.MotionMode.ToString();
                return true;
            case "playback.strictSpeedMotionEventsPerSecond":
                value = settings.StrictSpeedMotionEventsPerSecond;
                return true;
            case "playback.precisionMotionEventsPerSecond":
                value = settings.PrecisionMotionEventsPerSecond;
                return true;
            case "playback.maximumMotionErrorPixels":
                value = settings.MaximumMotionErrorPixels;
                return true;
            case "playback.countdownSeconds":
                value = settings.CountdownSeconds;
                return true;
            case "logging.level":
                value = settings.LogLevel;
                return true;
            case "recording.mouse":
                value = settings.IsMouseRecordingEnabled;
                return true;
            case "recording.keyboard":
                value = settings.IsKeyboardRecordingEnabled;
                return true;
            case "recording.forceRelative":
                value = settings.ForceRelativeCoordinates;
                return true;
            case "recording.skipInitialZeroZero":
                value = settings.SkipInitialZeroZero;
                return true;
            case "textExpansion.enabled":
                value = settings.EnableTextExpansion;
                return true;
            case "ui.theme":
                value = settings.Theme;
                return true;
            case "ui.language":
                value = settings.Language;
                return true;
            case "ui.trayIcon":
                value = settings.EnableTrayIcon;
                return true;
            case "ui.startMinimized":
                value = settings.StartMinimized;
                return true;
            case "updates.checkForUpdates":
                value = settings.CheckForUpdates;
                return true;
            case "screen.portalRestoreToken":
                value = string.IsNullOrEmpty(settings.PortalScreenCastRestoreToken) ? "empty" : "set";
                return true;
            default:
                value = null;
                return false;
        }
    }

    private static bool TrySetValue(AppSettings settings, string key, string rawValue, out string errorMessage)
    {
        switch (key)
        {
            case "playback.speed":
                if (!double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var speed))
                {
                    errorMessage = $"Invalid numeric value for {key}: {rawValue}";
                    return false;
                }
                settings.PlaybackSpeed = speed;
                errorMessage = string.Empty;
                return true;

            case "playback.loop":
                if (!TryParseBool(rawValue, out var loop))
                {
                    errorMessage = $"Invalid boolean value for {key}: {rawValue}";
                    return false;
                }
                settings.IsLooping = loop;
                errorMessage = string.Empty;
                return true;

            case "playback.loopCount":
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var loopCount) || loopCount < 0)
                {
                    errorMessage = $"Invalid integer value for {key}: {rawValue}. Expected >= 0.";
                    return false;
                }
                settings.LoopCount = loopCount;
                errorMessage = string.Empty;
                return true;

            case "playback.loopDelayMs":
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var loopDelay) || loopDelay < 0)
                {
                    errorMessage = $"Invalid integer value for {key}: {rawValue}. Expected >= 0.";
                    return false;
                }
                settings.LoopDelayMs = loopDelay;
                errorMessage = string.Empty;
                return true;

            case "playback.motionMode":
                settings.MotionMode = rawValue.Trim().ToLowerInvariant() switch
                {
                    "precision" => MotionPlaybackMode.Precision,
                    "strict-speed" or "strictspeed" or "strict" => MotionPlaybackMode.StrictSpeed,
                    _ => (MotionPlaybackMode)(-1),
                };
                if (!Enum.IsDefined(settings.MotionMode))
                {
                    errorMessage = $"Invalid value for {key}: {rawValue}. Allowed: precision, strict-speed.";
                    return false;
                }
                errorMessage = string.Empty;
                return true;

            case "playback.strictSpeedMotionEventsPerSecond":
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var motionRate)
                    || motionRate is < PlaybackOptions.MinStrictSpeedMotionEventsPerSecond or > PlaybackOptions.MaxStrictSpeedMotionEventsPerSecond)
                {
                    errorMessage = $"Invalid integer value for {key}: {rawValue}. Expected {PlaybackOptions.MinStrictSpeedMotionEventsPerSecond}..{PlaybackOptions.MaxStrictSpeedMotionEventsPerSecond}.";
                    return false;
                }
                settings.StrictSpeedMotionEventsPerSecond = motionRate;
                errorMessage = string.Empty;
                return true;

            case "playback.precisionMotionEventsPerSecond":
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var precisionMotionRate)
                    || precisionMotionRate is < PlaybackOptions.MinPrecisionMotionEventsPerSecond or > PlaybackOptions.MaxPrecisionMotionEventsPerSecond)
                {
                    errorMessage = $"Invalid integer value for {key}: {rawValue}. Expected {PlaybackOptions.MinPrecisionMotionEventsPerSecond}..{PlaybackOptions.MaxPrecisionMotionEventsPerSecond}.";
                    return false;
                }
                settings.PrecisionMotionEventsPerSecond = precisionMotionRate;
                errorMessage = string.Empty;
                return true;

            case "playback.maximumMotionErrorPixels":
                if (!double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var maximumMotionErrorPixels)
                    || !double.IsFinite(maximumMotionErrorPixels)
                    || maximumMotionErrorPixels is < PlaybackOptions.MinMaximumMotionErrorPixels or > PlaybackOptions.MaxMaximumMotionErrorPixels)
                {
                    errorMessage = $"Invalid numeric value for {key}: {rawValue}. Expected {PlaybackOptions.MinMaximumMotionErrorPixels.ToString(CultureInfo.InvariantCulture)}..{PlaybackOptions.MaxMaximumMotionErrorPixels.ToString(CultureInfo.InvariantCulture)}.";
                    return false;
                }
                settings.MaximumMotionErrorPixels = maximumMotionErrorPixels;
                errorMessage = string.Empty;
                return true;

            case "playback.countdownSeconds":
                if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var countdown) || countdown < 0)
                {
                    errorMessage = $"Invalid integer value for {key}: {rawValue}. Expected >= 0.";
                    return false;
                }
                settings.CountdownSeconds = countdown;
                errorMessage = string.Empty;
                return true;

            case "logging.level":
                {
                    var normalized = AllowedLogLevels.FirstOrDefault(x => string.Equals(x, rawValue, StringComparison.OrdinalIgnoreCase));
                    if (normalized is null)
                    {
                        errorMessage = $"Invalid value for {key}: {rawValue}. Allowed: {string.Join(", ", AllowedLogLevels)}.";
                        return false;
                    }
                    settings.LogLevel = normalized;
                    errorMessage = string.Empty;
                    return true;
                }

            case "recording.mouse":
                if (!TryParseBool(rawValue, out var recordMouse))
                {
                    errorMessage = $"Invalid boolean value for {key}: {rawValue}";
                    return false;
                }
                settings.IsMouseRecordingEnabled = recordMouse;
                errorMessage = string.Empty;
                return true;

            case "recording.keyboard":
                if (!TryParseBool(rawValue, out var recordKeyboard))
                {
                    errorMessage = $"Invalid boolean value for {key}: {rawValue}";
                    return false;
                }
                settings.IsKeyboardRecordingEnabled = recordKeyboard;
                errorMessage = string.Empty;
                return true;

            case "recording.forceRelative":
                if (!TryParseBool(rawValue, out var forceRelative))
                {
                    errorMessage = $"Invalid boolean value for {key}: {rawValue}";
                    return false;
                }
                settings.ForceRelativeCoordinates = forceRelative;
                errorMessage = string.Empty;
                return true;

            case "recording.skipInitialZeroZero":
                if (!TryParseBool(rawValue, out var skipZero))
                {
                    errorMessage = $"Invalid boolean value for {key}: {rawValue}";
                    return false;
                }
                settings.SkipInitialZeroZero = skipZero;
                errorMessage = string.Empty;
                return true;

            case "textExpansion.enabled":
                if (!TryParseBool(rawValue, out var textExpansionEnabled))
                {
                    errorMessage = $"Invalid boolean value for {key}: {rawValue}";
                    return false;
                }
                settings.EnableTextExpansion = textExpansionEnabled;
                errorMessage = string.Empty;
                return true;

            case "ui.theme":
                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    errorMessage = $"Invalid value for {key}: value cannot be empty.";
                    return false;
                }
                settings.Theme = rawValue;
                errorMessage = string.Empty;
                return true;

            case "ui.language":
                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    errorMessage = $"Invalid value for {key}: value cannot be empty.";
                    return false;
                }
                settings.Language = rawValue;
                errorMessage = string.Empty;
                return true;

            case "ui.trayIcon":
                if (!TryParseBool(rawValue, out var trayIcon))
                {
                    errorMessage = $"Invalid boolean value for {key}: {rawValue}";
                    return false;
                }
                settings.EnableTrayIcon = trayIcon;
                errorMessage = string.Empty;
                return true;

            case "ui.startMinimized":
                if (!TryParseBool(rawValue, out var startMinimized))
                {
                    errorMessage = $"Invalid boolean value for {key}: {rawValue}";
                    return false;
                }
                settings.StartMinimized = startMinimized;
                errorMessage = string.Empty;
                return true;

            case "updates.checkForUpdates":
                if (!TryParseBool(rawValue, out var checkForUpdates))
                {
                    errorMessage = $"Invalid boolean value for {key}: {rawValue}";
                    return false;
                }
                settings.CheckForUpdates = checkForUpdates;
                errorMessage = string.Empty;
                return true;

            case "screen.portalRestoreToken":
                errorMessage = "screen.portalRestoreToken is status-only; use settings reset screen.portalRestoreToken to clear it.";
                return false;

            default:
                errorMessage = $"Unknown key: {key}";
                return false;
        }
    }

    private static bool TryResetValue(AppSettings settings, AppSettings defaults, string key, out string errorMessage)
    {
        switch (key)
        {
            case "playback.speed":
                settings.PlaybackSpeed = defaults.PlaybackSpeed;
                break;
            case "playback.loop":
                settings.IsLooping = defaults.IsLooping;
                break;
            case "playback.loopCount":
                settings.LoopCount = defaults.LoopCount;
                break;
            case "playback.loopDelayMs":
                settings.LoopDelayMs = defaults.LoopDelayMs;
                break;
            case "playback.motionMode":
                settings.MotionMode = defaults.MotionMode;
                break;
            case "playback.strictSpeedMotionEventsPerSecond":
                settings.StrictSpeedMotionEventsPerSecond = defaults.StrictSpeedMotionEventsPerSecond;
                break;
            case "playback.precisionMotionEventsPerSecond":
                settings.PrecisionMotionEventsPerSecond = defaults.PrecisionMotionEventsPerSecond;
                break;
            case "playback.maximumMotionErrorPixels":
                settings.MaximumMotionErrorPixels = defaults.MaximumMotionErrorPixels;
                break;
            case "playback.countdownSeconds":
                settings.CountdownSeconds = defaults.CountdownSeconds;
                break;
            case "logging.level":
                settings.LogLevel = defaults.LogLevel;
                break;
            case "recording.mouse":
                settings.IsMouseRecordingEnabled = defaults.IsMouseRecordingEnabled;
                break;
            case "recording.keyboard":
                settings.IsKeyboardRecordingEnabled = defaults.IsKeyboardRecordingEnabled;
                break;
            case "recording.forceRelative":
                settings.ForceRelativeCoordinates = defaults.ForceRelativeCoordinates;
                break;
            case "recording.skipInitialZeroZero":
                settings.SkipInitialZeroZero = defaults.SkipInitialZeroZero;
                break;
            case "textExpansion.enabled":
                settings.EnableTextExpansion = defaults.EnableTextExpansion;
                break;
            case "ui.theme":
                settings.Theme = defaults.Theme;
                break;
            case "ui.language":
                settings.Language = defaults.Language;
                break;
            case "ui.trayIcon":
                settings.EnableTrayIcon = defaults.EnableTrayIcon;
                break;
            case "ui.startMinimized":
                settings.StartMinimized = defaults.StartMinimized;
                break;
            case "updates.checkForUpdates":
                settings.CheckForUpdates = defaults.CheckForUpdates;
                break;
            case "screen.portalRestoreToken":
                settings.PortalScreenCastRestoreToken = null;
                break;
            default:
                errorMessage = $"Unknown key: {key}";
                return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryParseBool(string value, out bool result)
    {
        if (bool.TryParse(value, out result))
        {
            return true;
        }

        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }
}
