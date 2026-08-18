namespace CrossMacro.Cli.Services;

public sealed record SettingsMutationData(string Key, object? OldValue, object? NewValue);
