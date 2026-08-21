namespace CrossMacro.Mcp.Contracts;

public sealed class McpShortcutTaskRun
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string Hotkey { get; init; } = string.Empty;
    public string MacroFilePath { get; init; } = string.Empty;
    public DateTime? LastTriggeredTime { get; init; }
    public string? LastStatus { get; init; }
}
