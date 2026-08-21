namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// A read-only image search result that never echoes the submitted file path.
/// </summary>
public sealed record McpScreenImageSearchResult(
    McpToolOutcome Outcome,
    bool? Found,
    McpScreenPoint? Point,
    double? Score,
    McpScreenRegion? Region,
    double? Similarity,
    string? MatchMode,
    string? ProviderName);
