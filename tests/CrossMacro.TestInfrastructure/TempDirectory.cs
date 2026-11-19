namespace CrossMacro.TestInfrastructure;

/// <summary>
/// A uniquely named directory under the physical system temp path that is deleted on dispose.
/// macOS reports a symlinked temp path, which path-policy code rejects, so its real location is used.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory(bool create = true)
    {
        var root = OperatingSystem.IsMacOS() ? "/private/tmp" : System.IO.Path.GetTempPath();
        Path = System.IO.Path.Combine(root, $"crossmacro-tests-{Guid.NewGuid():N}");
        if (create)
        {
            _ = Directory.CreateDirectory(Path);
        }
    }

    public string Path { get; }

    public string Combine(string fileName) => System.IO.Path.Combine(Path, fileName);

    public string WriteFile(string fileName, string content = "fixture")
    {
        var path = Combine(fileName);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
