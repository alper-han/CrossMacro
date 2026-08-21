namespace CrossMacro.Mcp.Contracts;

public sealed class McpScheduleTask
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string Type { get; init; } = string.Empty;
    public string MacroFilePath { get; init; } = string.Empty;
    public double PlaybackSpeed { get; init; }
    public int? IntervalValue { get; init; }
    public string? IntervalUnit { get; init; }
    public DateTime? ScheduledDateTime { get; init; }
    public string? WeeklyDays { get; init; }
    public string? WeeklyTime { get; init; }
    public DateTime? NextRunTime { get; init; }
    public DateTime? LastRunTime { get; init; }
    public string? LastStatus { get; init; }
}
