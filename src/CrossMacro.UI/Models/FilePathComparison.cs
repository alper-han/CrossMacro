namespace CrossMacro.UI.Models;

/// <summary>Path comparison matching the case sensitivity of the host file system.</summary>
internal static class FilePathComparison
{
    public static StringComparison Current { get; } = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
