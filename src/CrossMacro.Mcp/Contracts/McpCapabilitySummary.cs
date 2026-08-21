namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Summarizes CrossMacro platform readiness without exposing Doctor detail
/// payloads, which can include local paths and provider-specific diagnostics.
/// </summary>
public sealed record McpCapabilitySummary(
    bool HasFailures,
    bool HasWarnings,
    IReadOnlyList<McpCapabilityStatus> Checks);
