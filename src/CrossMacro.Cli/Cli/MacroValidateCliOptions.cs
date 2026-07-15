namespace CrossMacro.Cli;

public sealed record class MacroValidateCliOptions(string MacroFilePath, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
