namespace CrossMacro.Cli;

public abstract record class CliCommandOptions(bool JsonOutput, string? LogLevel = null);
