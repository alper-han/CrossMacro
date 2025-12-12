using System;
using System.IO;
using Serilog;

namespace CrossMacro.Core.Logging;

/// <summary>
/// Centralized logger configuration for CrossMacro
/// </summary>
public static class LoggerSetup
{
    /// <summary>
    /// Initialize Serilog with cross-platform log directory support
    /// </summary>
    public static void Initialize()
    {
        var logDir = GetLogDirectory();
        Directory.CreateDirectory(logDir);
        
        var logPath = Path.Combine(logDir, "log-.txt");
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.Async(a => a.File(logPath, rollingInterval: RollingInterval.Day))
            .CreateLogger();
        
        Log.Information("Logger initialized. Log directory: {LogDirectory}", logDir);
    }
    
    /// <summary>
    /// Get platform-specific log directory
    /// </summary>
    private static string GetLogDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows: %LOCALAPPDATA%\crossmacro\logs
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppConstants.AppIdentifier, "logs");
        }
        else
        {
            // Linux: XDG_DATA_HOME/crossmacro/logs or ~/.local/share/crossmacro/logs
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            
            if (!string.IsNullOrEmpty(xdgDataHome))
            {
                return Path.Combine(xdgDataHome, AppConstants.AppIdentifier, "logs");
            }
            
            // Fallback to XDG default
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", AppConstants.AppIdentifier, "logs");
        }
    }
}
