namespace CrossMacro.Mcp.Contracts.Profiles;

public sealed record McpProfile(
    string Id,
    string Name,
    DateTime CreatedAt,
    bool IsActive);
