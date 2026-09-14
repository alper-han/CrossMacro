namespace CrossMacro.Cli.Services.Settings;

internal sealed record SettingsDescriptor(
    string Key,
    Func<AppSettings, object?> Read,
    Func<AppSettings, string, (bool Success, string Error)> Write,
    Action<AppSettings, AppSettings> Reset);
