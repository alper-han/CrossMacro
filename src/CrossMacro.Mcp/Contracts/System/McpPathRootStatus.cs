namespace CrossMacro.Mcp.Contracts.System;

/// <summary>
/// Whether file access of one path class is possible. Access is denied until
/// <see cref="Setting"/> lists at least one root.
/// </summary>
public sealed record McpPathRootStatus(string Setting, bool Configured);
