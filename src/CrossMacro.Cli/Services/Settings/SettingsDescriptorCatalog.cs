namespace CrossMacro.Cli.Services.Settings;

/// <summary>The CLI settings vocabulary and its read, parse, write and reset behavior.</summary>
internal static class SettingsDescriptorCatalog
{
    private static readonly string[] AllowedLogLevels = ["Debug", "Information", "Warning", "Error"];
    public const string PortalRestoreStateKey = "screen.portalRestoreToken";

    public static IReadOnlyList<SettingsDescriptor> Values { get; } = CreateDescriptors();
    public static IReadOnlyList<string> Keys { get; } =
        Array.AsReadOnly(Values.Where(value => !value.Key.StartsWith("mcp.", StringComparison.Ordinal)).Select(value => value.Key)
            .Append(PortalRestoreStateKey).Concat(McpSettingsDescriptorCatalog.Keys).ToArray());

    public static bool TryGet(string key, out SettingsDescriptor descriptor)
    {
        descriptor = Values.FirstOrDefault(value => string.Equals(value.Key, key, StringComparison.Ordinal))!;
        return descriptor is not null;
    }

    private static IReadOnlyList<SettingsDescriptor> CreateDescriptors()
    {
        var descriptors = new List<SettingsDescriptor>
        {
            Number("playback.speed", settings => settings.PlaybackSpeed, (settings, value) => settings.PlaybackSpeed = value),
            Boolean("playback.loop", settings => settings.IsLooping, (settings, value) => settings.IsLooping = value),
            Integer("playback.loopCount", settings => settings.LoopCount, (settings, value) => settings.LoopCount = value),
            Integer("playback.loopDelayMs", settings => settings.LoopDelayMs, (settings, value) => settings.LoopDelayMs = value),
            MotionMode(),
            Integer("playback.strictSpeedMotionEventsPerSecond", settings => settings.StrictSpeedMotionEventsPerSecond, (settings, value) => settings.StrictSpeedMotionEventsPerSecond = value, PlaybackOptions.MinStrictSpeedMotionEventsPerSecond, PlaybackOptions.MaxStrictSpeedMotionEventsPerSecond),
            Integer("playback.precisionMotionEventsPerSecond", settings => settings.PrecisionMotionEventsPerSecond, (settings, value) => settings.PrecisionMotionEventsPerSecond = value, PlaybackOptions.MinPrecisionMotionEventsPerSecond, PlaybackOptions.MaxPrecisionMotionEventsPerSecond),
            Number("playback.maximumMotionErrorPixels", settings => settings.MaximumMotionErrorPixels, (settings, value) => settings.MaximumMotionErrorPixels = value, PlaybackOptions.MinMaximumMotionErrorPixels, PlaybackOptions.MaxMaximumMotionErrorPixels),
            Integer("playback.countdownSeconds", settings => settings.CountdownSeconds, (settings, value) => settings.CountdownSeconds = value),
            LogLevel(),
            Boolean("recording.mouse", settings => settings.IsMouseRecordingEnabled, (settings, value) => settings.IsMouseRecordingEnabled = value),
            Boolean("recording.keyboard", settings => settings.IsKeyboardRecordingEnabled, (settings, value) => settings.IsKeyboardRecordingEnabled = value),
            Boolean("recording.forceRelative", settings => settings.ForceRelativeCoordinates, (settings, value) => settings.ForceRelativeCoordinates = value),
            Boolean("recording.logicalRelative", settings => settings.UseLogicalRelativeCoordinates, (settings, value) => settings.UseLogicalRelativeCoordinates = value),
            Boolean("recording.skipInitialZeroZero", settings => settings.SkipInitialZeroZero, (settings, value) => settings.SkipInitialZeroZero = value),
            Boolean("textExpansion.enabled", settings => settings.EnableTextExpansion, (settings, value) => settings.EnableTextExpansion = value),
            Text("ui.theme", settings => settings.Theme, (settings, value) => settings.Theme = value),
            Text("ui.language", settings => settings.Language, (settings, value) => settings.Language = value),
            Boolean("ui.trayIcon", settings => settings.EnableTrayIcon, (settings, value) => settings.EnableTrayIcon = value),
            Boolean("ui.startMinimized", settings => settings.StartMinimized, (settings, value) => settings.StartMinimized = value),
            Boolean("updates.checkForUpdates", settings => settings.CheckForUpdates, (settings, value) => settings.CheckForUpdates = value),
        };
        descriptors.AddRange(McpSettingsDescriptorCatalog.Values.Select(descriptor => new SettingsDescriptor(
            descriptor.Key,
            settings => descriptor.GetValue(settings.McpSecurity),
            (settings, rawValue) =>
            {
                var success = descriptor.TrySetValue(settings.McpSecurity, rawValue, out var error);
                return (success, error);
            },
            (settings, defaults) => descriptor.ResetValue(settings.McpSecurity, defaults.McpSecurity))));
        return descriptors.AsReadOnly();
    }

    private static SettingsDescriptor Boolean(string key, Func<AppSettings, bool> read, Action<AppSettings, bool> write) =>
        new(key, settings => read(settings), (settings, rawValue) =>
        {
            bool value;
            if (!bool.TryParse(rawValue, out value))
            {
                switch (rawValue.ToLowerInvariant())
                {
                    case "1" or "yes" or "on": value = true; break;
                    case "0" or "no" or "off": value = false; break;
                    default: return (false, $"Invalid boolean value for {key}: {rawValue}");
                }
            }
            write(settings, value);
            return (true, string.Empty);
        }, (settings, defaults) => write(settings, read(defaults)));

    private static SettingsDescriptor Integer(string key, Func<AppSettings, int> read, Action<AppSettings, int> write, int minimum = 0, int? maximum = null) =>
        new(key, settings => read(settings), (settings, rawValue) =>
        {
            if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                || value < minimum || (maximum is int limit && value > limit))
            {
                var range = maximum is int upper ? string.Create(CultureInfo.InvariantCulture, $"{minimum}..{upper}") : string.Create(CultureInfo.InvariantCulture, $">= {minimum}");
                return (false, $"Invalid integer value for {key}: {rawValue}. Expected {range}.");
            }
            write(settings, value);
            return (true, string.Empty);
        }, (settings, defaults) => write(settings, read(defaults)));

    private static SettingsDescriptor Number(string key, Func<AppSettings, double> read, Action<AppSettings, double> write, double? minimum = null, double? maximum = null) =>
        new(key, settings => read(settings), (settings, rawValue) =>
        {
            if (!double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
                || (minimum is not null && (!double.IsFinite(value) || value < minimum || value > maximum)))
            {
                var range = minimum is null ? string.Empty : $" Expected {minimum.Value.ToString(CultureInfo.InvariantCulture)}..{maximum!.Value.ToString(CultureInfo.InvariantCulture)}.";
                return (false, $"Invalid numeric value for {key}: {rawValue}" + (range.Length > 0 ? "." + range : string.Empty));
            }
            write(settings, value);
            return (true, string.Empty);
        }, (settings, defaults) => write(settings, read(defaults)));

    private static SettingsDescriptor Text(string key, Func<AppSettings, string> read, Action<AppSettings, string> write) =>
        new(key, settings => read(settings), (settings, rawValue) =>
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return (false, $"Invalid value for {key}: value cannot be empty.");
            }
            write(settings, rawValue);
            return (true, string.Empty);
        }, (settings, defaults) => write(settings, read(defaults)));

    private static SettingsDescriptor MotionMode() =>
        new("playback.motionMode", settings => settings.MotionMode.ToString(), (settings, rawValue) =>
        {
            var mode = rawValue.Trim().ToLowerInvariant() switch
            {
                "precision" => MotionPlaybackMode.Precision,
                "strict-speed" or "strictspeed" or "strict" => MotionPlaybackMode.StrictSpeed,
                _ => (MotionPlaybackMode)(-1),
            };
            if (!Enum.IsDefined(mode))
            {
                return (false, $"Invalid value for playback.motionMode: {rawValue}. Allowed: precision, strict-speed.");
            }
            settings.MotionMode = mode;
            return (true, string.Empty);
        }, (settings, defaults) => settings.MotionMode = defaults.MotionMode);

    private static SettingsDescriptor LogLevel() =>
        new("logging.level", settings => settings.LogLevel, (settings, rawValue) =>
        {
            var normalized = AllowedLogLevels.FirstOrDefault(value => string.Equals(value, rawValue, StringComparison.OrdinalIgnoreCase));
            if (normalized is null)
            {
                return (false, $"Invalid value for logging.level: {rawValue}. Allowed: {string.Join(", ", AllowedLogLevels)}.");
            }
            settings.LogLevel = normalized;
            return (true, string.Empty);
        }, (settings, defaults) => settings.LogLevel = defaults.LogLevel);
}
