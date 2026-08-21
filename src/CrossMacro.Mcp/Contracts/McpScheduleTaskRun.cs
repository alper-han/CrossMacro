namespace CrossMacro.Mcp.Contracts;

public sealed class McpScheduleTaskRun
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string MacroFilePath { get; init; } = string.Empty;
    public DateTime? LastRunTime { get; init; }
    public string? LastStatus { get; init; }
}
