namespace CrossMacro.Mcp.Contracts;

public sealed record McpProfile(
    string Id,
    string Name,
    DateTime CreatedAt,
    bool IsActive);
