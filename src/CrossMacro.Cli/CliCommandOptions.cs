namespace CrossMacro.Cli;

public abstract record CliCommandOptions(bool JsonOutput, string? LogLevel = null);
