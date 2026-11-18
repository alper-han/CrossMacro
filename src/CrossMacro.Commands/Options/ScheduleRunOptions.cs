namespace CrossMacro.Commands.Options;

public sealed record ScheduleRunOptions(string TaskId, bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
