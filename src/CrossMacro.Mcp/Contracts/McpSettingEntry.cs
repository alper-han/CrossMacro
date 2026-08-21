namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A settings value exposed through MCP. Sensitive values are represented only
/// by their key and redaction marker.
/// </summary>
public sealed record McpSettingEntry(
    string Key,
    string? Value,
    bool Redacted);
