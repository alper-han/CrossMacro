namespace CrossMacro.Cli;

public sealed record MacroValidateCliOptions(string MacroFilePath, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
