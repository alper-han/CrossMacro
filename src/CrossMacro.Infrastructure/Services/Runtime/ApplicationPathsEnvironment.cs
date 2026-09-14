namespace CrossMacro.Infrastructure.Services.Runtime;

/// <summary>Captures platform environment at the composition boundary; consumers receive explicit paths.</summary>
public static class ApplicationPathsEnvironment
{
    public static ApplicationPaths CaptureCurrent()
    {
        string configBase;

        if (OperatingSystem.IsMacOS())
        {
            // macOS: ~/Library/Application Support/crossmacro
            // This is Apple's standard location for app config and data
            configBase = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support");
        }
        else if (OperatingSystem.IsWindows())
        {
            // Windows: %APPDATA%\crossmacro (Roaming)
            configBase = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else
        {
            // Linux and others: Follow XDG Base Directory specification
            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            configBase = string.IsNullOrWhiteSpace(xdgConfigHome)
                || !Path.IsPathRooted(xdgConfigHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
                : xdgConfigHome;
        }

        return new ApplicationPaths(Path.Combine(configBase, AppConstants.AppIdentifier));
    }

}
