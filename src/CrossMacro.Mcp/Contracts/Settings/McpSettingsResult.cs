namespace CrossMacro.Mcp.Contracts.Settings;

/// <summary>
/// Structured result for settings read, mutation, and key-list operations.
/// </summary>
public sealed record McpSettingsResult(
    string Action,
    McpToolOutcome Outcome,
    IReadOnlyList<McpSettingEntry> Settings,
    IReadOnlyList<string> Keys);
