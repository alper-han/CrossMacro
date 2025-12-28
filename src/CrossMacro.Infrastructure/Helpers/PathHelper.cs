using System;
using System.IO;
using CrossMacro.Core;

namespace CrossMacro.Infrastructure.Helpers;

/// <summary>
/// Helper for resolving application paths following platform-specific conventions:
/// - Windows: %APPDATA% (Roaming Application Data)
/// - Linux: XDG Base Directory specification (~/.config)
/// - macOS: ~/Library/Application Support (Apple standard)
/// </summary>
public static class PathHelper
{
    public static string GetConfigDirectory()
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
            configBase = string.IsNullOrEmpty(xdgConfigHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
                : xdgConfigHome;
        }

        return Path.Combine(configBase, AppConstants.AppIdentifier);
    }

    public static string GetConfigFilePath(string fileName)
    {
        return Path.Combine(GetConfigDirectory(), fileName);
    }
}
