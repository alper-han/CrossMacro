namespace CrossMacro.Commands.Options;

public sealed record QuickSetupOptions(bool JsonOutput = false, string? LogLevel = null)
    : CommandOptions(JsonOutput, LogLevel);
