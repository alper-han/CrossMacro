namespace CrossMacro.Cli.Services.Settings;

public sealed record SettingsMutationData(string Key, object? OldValue, object? NewValue);
