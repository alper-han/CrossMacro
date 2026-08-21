namespace CrossMacro.Mcp.Services;

public interface IMcpPathPolicy
{
    /// <summary>
    /// Authorizes and normalizes a path before it is passed to an existing
    /// pathname-based CrossMacro service. This boundary rejects traversal,
    /// unconfigured roots, and static reparse points. It does not provide an
    /// operating-system file handle and therefore cannot eliminate a same-user
    /// time-of-check/time-of-use race after this method returns.
    /// </summary>
    public bool TryAuthorize(
        string path,
        McpPathKind kind,
        bool requireExisting,
        out string normalizedPath,
        out McpToolOutcome failure);
}
