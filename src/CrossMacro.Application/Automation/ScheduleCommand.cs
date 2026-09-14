namespace CrossMacro.Application.Automation;

public sealed record ScheduleCommand(
    ScheduleCommandAction Action,
    string? TaskId = null,
    string? Name = null,
    string? MacroFilePath = null,
    string? Interval = null,
    string? At = null,
    string? Weekly = null,
    string? Time = null,
    double? Speed = null,
    bool? Enabled = null);
