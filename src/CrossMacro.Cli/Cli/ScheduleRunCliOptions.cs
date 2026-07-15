namespace CrossMacro.Cli;

public sealed record ScheduleRunCliOptions(string TaskId, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
