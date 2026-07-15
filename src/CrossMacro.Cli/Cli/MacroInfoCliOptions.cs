namespace CrossMacro.Cli;

public sealed record MacroInfoCliOptions(string MacroFilePath, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
