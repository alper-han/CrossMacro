using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class SettingsCliService : ISettingsCliService
{
    private static readonly string[] AllowedLogLevels = ["Debug", "Information", "Warning", "Error"];

    private readonly ISettingsService _settingsService;

    public SettingsCliService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public Task<SettingsCommandResult> GetAsync(string? key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var settings = _settingsService.Load();

        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(new SettingsCommandResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Settings loaded.",
                Data = BuildSettingsDictionary(settings)
            });
        }

        if (!TryGetValue(settings, key, out var value))
        {
            return Task.FromResult(new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Unknown settings key.",
                Errors = [$"Unknown key: {key}", $"Available keys: {string.Join(", ", GetSupportedKeys())}"]
            });
        }

        return Task.FromResult(new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = $"{key}={value}",
            Data = new { key, value }
        });
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
                Message = "Missing settings key."
            };
        }

        if (value == null)
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Missing settings value."
            };
        }

        var settings = _settingsService.Load();

        if (!TryGetValue(settings, key, out var beforeValue))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Unknown settings key.",
                Errors = [$"Unknown key: {key}", $"Available keys: {string.Join(", ", GetSupportedKeys())}"]
            };
        }

        if (!TrySetValue(settings, key, value, out var errorMessage))
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Invalid settings value.",
                Errors = [errorMessage]
            };
        }

        try
        {
            await _settingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            return new SettingsCommandResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Failed to save settings.",
                Errors = [ex.Message]
            };
        }

        TryGetValue(settings, key, out var afterValue);

        return new SettingsCommandResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = $"{key} updated.",
            Data = new { key, oldValue = beforeValue, newValue = afterValue }
        };
    }

    public static IReadOnlyList<string> GetSupportedKeys()
    {
        return
        [
            "playback.speed",
            "playback.loop",
            "playback.loopCount",
            "playback.loopDelayMs",
            "playback.countdownSeconds",
            "logging.level",
            "recording.mouse",
            "recording.keyboard",
            "recording.forceRelative",
            "recording.skipInitialZeroZero",
            "textExpansion.enabled"
        ];
    }

    private static Dictionary<string, object?> BuildSettingsDictionary(AppSettings settings)
    {
        return new Dictionary<string, object?>
        {
            ["playback.speed"] = settings.PlaybackSpeed,
            ["playback.loop"] = settings.IsLooping,
            ["playback.loopCount"] = settings.LoopCount,
            ["playback.loopDelayMs"] = settings.LoopDelayMs,
            ["playback.countdownSeconds"] = settings.CountdownSeconds,
            ["logging.level"] = settings.LogLevel,
            ["recording.mouse"] = settings.IsMouseRecordingEnabled,
            ["recording.keyboard"] = settings.IsKeyboardRecordingEnabled,
            ["recording.forceRelative"] = settings.ForceRelativeCoordinates,
            ["recording.skipInitialZeroZero"] = settings.SkipInitialZeroZero,
            ["textExpansion.enabled"] = settings.EnableTextExpansion
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
                if (normalized == null)
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

            default:
                errorMessage = $"Unknown key: {key}";
                return false;
        }
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
