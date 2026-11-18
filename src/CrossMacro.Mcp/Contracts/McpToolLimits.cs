namespace CrossMacro.Mcp.Contracts;

/// <summary>Numeric limits shared by the MCP tools.</summary>
internal static class McpToolLimits
{
    /// <summary>Timeout applied to screen and window waits when the caller supplies none.</summary>
    public const int DefaultWaitTimeoutMs = 5_000;

    /// <summary>Largest wait timeout a screen or window tool accepts.</summary>
    public const int MaximumWaitTimeoutMs = 30_000;

    /// <summary>Largest PNG that may be returned inline as MCP image content.</summary>
    public const int MaximumInlineImageBytes = 8 * 1024 * 1024;
}
