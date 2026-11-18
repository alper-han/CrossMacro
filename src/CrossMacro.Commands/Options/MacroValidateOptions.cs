namespace CrossMacro.Commands.Options;

public sealed record MacroValidateOptions(string MacroFilePath, bool JsonOutput = false, string? LogLevel = null)
    : CommandOptions(JsonOutput, LogLevel);
