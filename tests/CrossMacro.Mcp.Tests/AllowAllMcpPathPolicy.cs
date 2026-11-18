namespace CrossMacro.Mcp.Tests;

internal sealed class AllowAllMcpPathPolicy : IMcpPathPolicy
{
    public bool TryAuthorize(
        string path,
        McpPathSetting setting,
        bool requireExisting,
        out string normalizedPath,
        out McpToolOutcome failure)
    {
        normalizedPath = Path.GetFullPath(path);
        failure = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    public bool HasConfiguredRoots(McpPathSetting setting) => true;
}
