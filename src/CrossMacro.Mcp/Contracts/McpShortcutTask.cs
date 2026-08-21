namespace CrossMacro.Mcp.Contracts;

public sealed class McpShortcutTask
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string Hotkey { get; init; } = string.Empty;
    public string MacroFilePath { get; init; } = string.Empty;
    public double PlaybackSpeed { get; init; }
    public bool LoopEnabled { get; init; }
    public bool RunWhileHeld { get; init; }
    public int RepeatCount { get; init; }
    public int RepeatDelayMs { get; init; }
    public bool RandomRepeatDelay { get; init; }
    public int? RepeatDelayMinMs { get; init; }
    public int? RepeatDelayMaxMs { get; init; }
    public IReadOnlyList<McpShortcutWindowRule> WindowRules { get; init; } = [];
    public DateTime? LastTriggeredTime { get; init; }
    public string? LastStatus { get; init; }
}
