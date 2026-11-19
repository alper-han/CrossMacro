namespace CrossMacro.Commands.Settings;

public sealed record SettingsMutationData(string Key, object? OldValue, object? NewValue);
