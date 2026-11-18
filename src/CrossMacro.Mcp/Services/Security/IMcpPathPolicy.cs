namespace CrossMacro.Mcp.Services.Security;

public interface IMcpPathPolicy
{
    /// <summary>
    /// Authorizes and normalizes a path before it is passed to an existing
    /// pathname-based CrossMacro service. A path is accepted only when a root
    /// is configured for its path class and the path lies inside one. This
    /// boundary rejects traversal, unconfigured roots, and static reparse
    /// points. It does not provide an operating-system file handle and therefore
    /// cannot eliminate a same-user time-of-check/time-of-use race after this
    /// method returns.
    /// </summary>
    public bool TryAuthorize(
        string path,
        McpPathSetting setting,
        bool requireExisting,
        out string normalizedPath,
        out McpToolOutcome failure);

    public bool HasConfiguredRoots(McpPathSetting setting);
}
