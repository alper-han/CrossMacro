
namespace CrossMacro.Cli.Serialization;

public sealed record ScheduleTaskRunData(
    Guid Id,
    string Name,
    bool Enabled,
    string MacroFilePath,
    DateTime? LastRunTime,
    string? LastStatus
);
