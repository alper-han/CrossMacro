using System;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CrossMacro.Core.Logging;

/// <summary>
/// Centralized logger configuration for CrossMacro
/// </summary>
public static class LoggerSetup
{
    private static LoggingLevelSwitch? _levelSwitch;
    
    /// <summary>
    /// Gets the logging level switch for runtime level changes
    /// </summary>
    public static LoggingLevelSwitch? LevelSwitch => _levelSwitch;
    
    /// <summary>
    /// Initialize Serilog with cross-platform log directory support
    /// </summary>
    /// <param name="logLevel">Initial log level (Debug, Information, Warning, Error)</param>
    public static void Initialize(string logLevel = "Information")
    {
        var logDir = GetLogDirectory();
        Directory.CreateDirectory(logDir);
        
        var logPath = Path.Combine(logDir, "log-.txt");
        
        _levelSwitch = new LoggingLevelSwitch(ParseLogLevel(logLevel));
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.Console()
            .WriteTo.Async(a => a.File(logPath, rollingInterval: RollingInterval.Day))
            .CreateLogger();
        
        Log.Information("Logger initialized. Log directory: {LogDirectory}, Level: {Level}", logDir, logLevel);
    }
    
    /// <summary>
    /// Change log level at runtime
    /// </summary>
    /// <param name="logLevel">New log level (Debug, Information, Warning, Error)</param>
    public static void SetLogLevel(string logLevel)
    {
        if (_levelSwitch == null)
            return;
            
        var newLevel = ParseLogLevel(logLevel);
        if (_levelSwitch.MinimumLevel != newLevel)
        {
            _levelSwitch.MinimumLevel = newLevel;
            Log.Information("Log level changed to {Level}", logLevel);
        }
    }
    
    /// <summary>
    /// Parse string to LogEventLevel with fallback to Information
    /// </summary>
    private static LogEventLevel ParseLogLevel(string level)
    {
        return level?.ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
    
    /// <summary>
    /// Get platform-specific log directory following platform conventions:
    /// - Windows: %LOCALAPPDATA%\crossmacro\logs
    /// - Linux: XDG_DATA_HOME/crossmacro/logs or ~/.local/share/crossmacro/logs
    /// - macOS: ~/Library/Logs/crossmacro
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
        else if (OperatingSystem.IsMacOS())
        {
            // macOS: ~/Library/Logs/crossmacro (Apple standard log location)
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Logs", AppConstants.AppIdentifier);
        }
        else
        {
            // Linux and others: XDG_DATA_HOME/crossmacro/logs or ~/.local/share/crossmacro/logs
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
