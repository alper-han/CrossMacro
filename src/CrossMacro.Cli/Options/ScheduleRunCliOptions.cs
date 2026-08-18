namespace CrossMacro.Cli.Options;

public sealed record ScheduleRunCliOptions(string TaskId, bool JsonOutput = false, string? LogLevel = null)
    : CliCommandOptions(JsonOutput, LogLevel);
