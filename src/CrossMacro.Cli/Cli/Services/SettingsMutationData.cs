namespace CrossMacro.Cli.Services;

public sealed record class SettingsMutationData(string Key, object? OldValue, object? NewValue);
