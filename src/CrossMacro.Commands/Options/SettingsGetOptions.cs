namespace CrossMacro.Commands.Options;

public sealed record SettingsGetOptions(string? Key = null, bool JsonOutput = false, string? LogLevel = null, bool All = false)
    : ProfileBackedOptions(JsonOutput, LogLevel);
