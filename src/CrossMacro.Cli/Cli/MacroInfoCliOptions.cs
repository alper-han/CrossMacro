namespace CrossMacro.Cli;

public sealed record class MacroInfoCliOptions(string MacroFilePath, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
