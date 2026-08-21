namespace CrossMacro.Mcp.Services;

public sealed class McpPathPolicy(ISettingsService settingsService) : IMcpPathPolicy
{
    private readonly ISettingsService _settingsService = settingsService;

    public bool TryAuthorize(
        string path,
        McpPathKind kind,
        bool requireExisting,
        out string normalizedPath,
        out McpToolOutcome failure)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            failure = McpToolOutcomeMapper.InvalidArguments("Path must be an absolute path.");
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
            if (requireExisting && !File.Exists(normalizedPath) && !Directory.Exists(normalizedPath))
            {
                failure = McpToolOutcomeMapper.FileError("The requested file or directory was not found.");
                return false;
            }

            if (ContainsReparsePoint(normalizedPath))
            {
                failure = McpToolOutcomeMapper.PathNotAllowed();
                return false;
            }

            var (roots, hasConfiguredRoots) = GetRoots(kind);
            var candidatePath = normalizedPath;
            if (hasConfiguredRoots && (roots.Count is 0 || !roots.Any(root => IsWithinRoot(candidatePath, root))))
            {
                failure = McpToolOutcomeMapper.PathNotAllowed();
                return false;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            normalizedPath = string.Empty;
            failure = McpToolOutcomeMapper.InvalidArguments("Path is invalid.");
            return false;
        }

        failure = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private (IReadOnlyList<string> Roots, bool HasConfiguredRoots) GetRoots(McpPathKind kind)
    {
        var paths = _settingsService.Current.McpSecurity?.Paths ?? new McpPathSettings();
        var setting = kind switch
        {
            McpPathKind.MacroRead => McpPathSetting.MacroRead,
            McpPathKind.MacroWrite => McpPathSetting.MacroWrite,
            McpPathKind.ImageRead => McpPathSetting.ImageRead,
            McpPathKind.ImageWrite => McpPathSetting.ImageWrite,
            McpPathKind.FileRead => McpPathSetting.FileRead,
            McpPathKind.FileWrite => McpPathSetting.FileWrite,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown MCP path kind."),
        };

        var configuredRoots = paths.GetRoots(setting);
        return (configuredRoots
            .Select(TryNormalizeRoot)
            .Where(static root => root is not null)
            .Cast<string>()
            .ToArray(), configuredRoots.Count > 0);
    }

    private static string? TryNormalizeRoot(string root)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            {
                return null;
            }

            var normalized = Path.GetFullPath(root);
            return !Directory.Exists(normalized) || ContainsReparsePoint(normalized)
                ? null
                : TrimTrailingSeparators(normalized);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static bool IsWithinRoot(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return !Path.IsPathFullyQualified(relative)
            && !string.Equals(relative, "..", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static bool ContainsReparsePoint(string path)
    {
        var current = path;
        while (!string.IsNullOrEmpty(current))
        {
            if (HasLinkTarget(current)
                || ((File.Exists(current) || Directory.Exists(current))
                && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
            )
            {
                return true;
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent ?? string.Empty;
        }

        return false;
    }

    private static bool HasLinkTarget(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget is not null
                || new DirectoryInfo(path).LinkTarget is not null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string TrimTrailingSeparators(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        return path.Length > root.Length
            ? path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : path;
    }
}
