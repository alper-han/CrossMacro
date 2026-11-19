namespace CrossMacro.Commands;

public abstract record CommandOptions(bool JsonOutput, string? LogLevel = null);
