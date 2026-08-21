namespace CrossMacro.Mcp.Contracts;

public sealed record McpProfilesResult(
    string Action,
    McpToolOutcome Outcome,
    IReadOnlyList<McpProfile> Profiles,
    string? ActiveProfileId);
