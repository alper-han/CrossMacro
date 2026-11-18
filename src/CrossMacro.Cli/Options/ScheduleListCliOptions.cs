namespace CrossMacro.Cli.Options;

public sealed record ScheduleListCliOptions(bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedCliOptions(JsonOutput, LogLevel);
