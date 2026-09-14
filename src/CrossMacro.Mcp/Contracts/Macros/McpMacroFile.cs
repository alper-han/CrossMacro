namespace CrossMacro.Mcp.Contracts.Macros;

/// <summary>
/// Non-content metadata for one regular macro file.
/// </summary>
public sealed record McpMacroFile(
    string MacroPath,
    string FileName,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc);
