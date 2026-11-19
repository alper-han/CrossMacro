namespace CrossMacro.Commands.Options;

public sealed record ScheduleListOptions(bool JsonOutput = false, string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
