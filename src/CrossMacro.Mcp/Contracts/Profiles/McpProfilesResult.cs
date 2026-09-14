namespace CrossMacro.Mcp.Contracts.Profiles;

public sealed record McpProfilesResult(
    string Action,
    McpToolOutcome Outcome,
    IReadOnlyList<McpProfile> Profiles,
    string? ActiveProfileId);
