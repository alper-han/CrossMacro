namespace CrossMacro.TestInfrastructure;

/// <summary>
/// Produces short unique Unix domain socket paths. sun_path is limited to ~108 bytes;
/// long TMPDIRs (e.g. nix develop shells) push GetTempPath-based paths over the limit.
/// </summary>
internal static class TestSocketPaths
{
    private const int MaxSafeSocketPathBytes = 100;

    public static string CreateShort(string prefix)
    {
        var name = $"{prefix}-{Guid.NewGuid():N}.sock";
        var candidate = Path.Combine(Path.GetTempPath(), name);
        return System.Text.Encoding.UTF8.GetByteCount(candidate) <= MaxSafeSocketPathBytes
            ? candidate
            : Path.Combine("/tmp", name);
    }
}
