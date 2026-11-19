namespace CrossMacro.Infrastructure.Tests.TestDoubles;

/// <summary>A throw-away configuration root on the physical temp path, so symlink checks see the real location.</summary>
internal sealed class TempConfigDirectory : IDisposable
{
    public TempConfigDirectory(string prefix)
    {
        Path = System.IO.Path.Combine(GetPhysicalTempPath(), prefix + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }

    private static string GetPhysicalTempPath()
    {
        var tempPath = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
        var tempDirectory = new DirectoryInfo(tempPath);
        for (var current = tempDirectory; current is not null; current = current.Parent)
        {
            if (!current.Exists || !current.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            if (current.ResolveLinkTarget(returnFinalTarget: true) is not { FullName: var targetPath })
            {
                break;
            }

            var suffix = System.IO.Path.GetRelativePath(current.FullName, tempPath);
            return string.Equals(suffix, ".", StringComparison.Ordinal) ? targetPath : System.IO.Path.Combine(targetPath, suffix);
        }

        return tempPath;
    }
}
