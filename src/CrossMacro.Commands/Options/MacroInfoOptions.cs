namespace CrossMacro.Commands.Options;

public sealed record MacroInfoOptions(string MacroFilePath, bool JsonOutput = false, string? LogLevel = null)
    : CommandOptions(JsonOutput, LogLevel);
