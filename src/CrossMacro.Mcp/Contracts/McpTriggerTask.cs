namespace CrossMacro.Mcp.Contracts;

public sealed class McpTriggerTask
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string Field { get; init; } = string.Empty;
    public string MatchMode { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? TargetProfileId { get; init; }
    public string? MacroFilePath { get; init; }
    public string FireMode { get; init; } = string.Empty;
    public int? CooldownMs { get; init; }
    public int? DebounceMs { get; init; }
    public DateTime? LastTriggeredTime { get; init; }
    public string? LastStatus { get; init; }
}
