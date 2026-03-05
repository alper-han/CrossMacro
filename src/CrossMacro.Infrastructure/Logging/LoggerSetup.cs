using System;
using System.IO;
using CrossMacro.Core;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CrossMacro.Infrastructure.Logging;

/// <summary>
/// Centralized logger configuration for CrossMacro.
/// </summary>
public static class LoggerSetup
{
    private static LoggingLevelSwitch? _levelSwitch;

    /// <summary>
    /// Gets the logging level switch for runtime level changes.
    /// </summary>
    public static LoggingLevelSwitch? LevelSwitch => _levelSwitch;

    /// <summary>
    /// Initialize Serilog with cross-platform log directory support.
    /// </summary>
    /// <param name="logLevel">Initial log level (Debug, Information, Warning, Error).</param>
    public static void Initialize(string logLevel = "Information")
    {
        _levelSwitch = new LoggingLevelSwitch(ParseLogLevel(logLevel));

        var config = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.Console();

        try
        {
            var logDir = GetLogDirectory();
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "log-.txt");
            config = config.WriteTo.Async(a => a.File(logPath, rollingInterval: RollingInterval.Day));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LoggerSetup] File logging disabled: {ex.Message}");
        }

        Log.Logger = config.CreateLogger();
        CrossMacro.Core.Logging.Log.Configure(new SerilogCoreLogger());
        Log.Debug("Logger initialized. Level: {Level}", logLevel);
    }

    /// <summary>
    /// Change log level at runtime.
    /// </summary>
    /// <param name="logLevel">New log level (Debug, Information, Warning, Error).</param>
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

    private static string GetLogDirectory()
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
