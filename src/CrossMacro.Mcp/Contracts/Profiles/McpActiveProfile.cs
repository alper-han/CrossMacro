namespace CrossMacro.Mcp.Contracts.Profiles;

/// <summary>
/// The active CrossMacro profile without its filesystem location.
/// </summary>
public sealed record McpActiveProfile(string Id, string Name);
