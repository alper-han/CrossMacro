namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A redacted readiness result derived from a CrossMacro doctor check.
/// </summary>
public sealed record McpCapabilityStatus(string Name, string Status, string Message);
