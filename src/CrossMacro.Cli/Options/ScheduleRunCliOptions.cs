namespace CrossMacro.Cli.Options;

public sealed record ScheduleRunCliOptions(string TaskId, bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedCliOptions(JsonOutput, LogLevel);
