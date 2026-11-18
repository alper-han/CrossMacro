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

    /// <summary>The directory for rolling log files, following each platform's convention.</summary>
    public static string CaptureLogDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppConstants.AppIdentifier, "logs");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Logs", AppConstants.AppIdentifier);
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrEmpty(xdgDataHome))
        {
            return Path.Combine(xdgDataHome, AppConstants.AppIdentifier, "logs");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", AppConstants.AppIdentifier, "logs");
    }
}
