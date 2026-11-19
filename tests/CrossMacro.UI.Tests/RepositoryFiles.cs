namespace CrossMacro.UI.Tests;

/// <summary>Locates checked-in source files of the UI project for tests that validate them.</summary>
internal static class RepositoryFiles
{
    private static readonly Lazy<string> RootDirectory = new(FindRoot);

    public static string UiProject => Path.Combine(RootDirectory.Value, "src", "CrossMacro.UI");

    public static string UiPath(params string[] segments) => Path.Combine([UiProject, .. segments]);

    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "CrossMacro.UI", "CrossMacro.UI.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test base directory.");
    }
}
