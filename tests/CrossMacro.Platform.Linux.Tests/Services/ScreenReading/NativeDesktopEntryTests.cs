namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

/// <summary>KWin only serves ScreenShot2 to desktop entries that declare the restricted interface.</summary>
public sealed class NativeDesktopEntryTests
{
    [Fact]
    public void NativeDesktopEntry_DeclaresTheKWinScreenShotPermission()
    {
        var entries = File.ReadAllLines(Path.Combine(FindRepoRoot(), "scripts/assets/CrossMacro.desktop"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && line[0] is not '#' and not '[')
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length is 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

        Assert.Equal("org.kde.KWin.ScreenShot2", entries["X-KDE-DBUS-Restricted-Interfaces"]);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CrossMacro.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
