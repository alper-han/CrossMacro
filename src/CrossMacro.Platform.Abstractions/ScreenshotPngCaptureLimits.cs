namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Shared safety budgets for encoded PNG capture data crossing platform
/// abstraction boundaries. MCP-specific response limits belong to MCP.
/// </summary>
public static class ScreenshotPngCaptureLimits
{
    public const int MaximumEncodedBytes = 48 * 1024 * 1024;
}
