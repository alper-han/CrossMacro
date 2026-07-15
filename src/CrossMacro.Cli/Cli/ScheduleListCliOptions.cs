namespace CrossMacro.Cli;

public sealed record ScheduleListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
