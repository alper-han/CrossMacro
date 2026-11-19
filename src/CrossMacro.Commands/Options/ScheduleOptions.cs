namespace CrossMacro.Commands.Options;

public sealed record ScheduleOptions(
    ScheduleAction Action,
    string? TaskId = null,
    string? Name = null,
    string? MacroFilePath = null,
    string? Interval = null,
    string? At = null,
    string? Weekly = null,
    string? Time = null,
    double? Speed = null,
    bool? Enabled = null,
    bool JsonOutput = false,
    string? LogLevel = null)
    : ProfileBackedOptions(JsonOutput, LogLevel);
