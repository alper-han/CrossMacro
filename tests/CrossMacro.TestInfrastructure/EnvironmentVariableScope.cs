namespace CrossMacro.TestInfrastructure;

/// <summary>
/// Sets process environment variables for the lifetime of the scope and restores every touched variable on disposal.
/// Owns a unique temporary root directory so tests can redirect XDG/HOME style locations away from the real profile.
/// Environment variables are process-wide: tests using this type must run in a non-parallel collection.
/// </summary>
public sealed class EnvironmentVariableScope : IDisposable
{
    private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

    public string Root { get; } = Path.Combine(Path.GetTempPath(), "crossmacro-tests", Guid.NewGuid().ToString("N"));

    public string CreateDirectory(string name)
    {
        var path = Path.Combine(Root, name);
        _ = Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Sets <paramref name="name"/> to <paramref name="value"/>, or removes it when the value is <see langword="null"/>.</summary>
    public void Set(string name, string? value)
    {
        _ = _originalValues.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        foreach (var (name, value) in _originalValues)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup of a unique temp directory; a lingering handle must not fail an otherwise passing test.
        }
    }
}
