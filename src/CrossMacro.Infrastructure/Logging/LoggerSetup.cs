
namespace CrossMacro.Infrastructure.Logging;

/// <summary>
/// Centralized logger configuration for CrossMacro.
/// </summary>
public static class LoggerSetup
{
    /// <summary>
    /// Gets the logging level switch for runtime level changes.
    /// </summary>
    public static LoggingLevelSwitch? LevelSwitch { get; private set; }

    /// <summary>
    /// Initialize Serilog with cross-platform log directory support.
    /// </summary>
    /// <param name="logLevel">Initial log level (Debug, Information, Warning, Error).</param>
    public static void Initialize(
        string logLevel = "Information",
        bool enableFileLogging = true,
        bool enableConsoleLogging = true)
    {
        LevelSwitch = new LoggingLevelSwitch(ParseLogLevel(logLevel));

        var config = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch);

        if (enableConsoleLogging)
        {
            config = config.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
        }

        if (enableFileLogging)
        {
            try
            {
                var logDir = GetLogDirectory();
                _ = Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "log-.txt");
                config = config.WriteTo.Async(a => a.File(logPath, rollingInterval: RollingInterval.Day, formatProvider: CultureInfo.InvariantCulture));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                WriteDiagnosticToStderr($"[LoggerSetup] File logging disabled: {ex.Message}");
            }
        }

        SerilogLog.Logger = config.CreateLogger();
        CrossMacro.Core.Logging.Log.Configure(new SerilogCoreLogger());
        SerilogLog.Debug("Logger initialized. Level: {Level}", logLevel);
    }

    /// <summary>
    /// Change log level at runtime.
    /// </summary>
    /// <param name="logLevel">New log level (Debug, Information, Warning, Error).</param>
    public static void SetLogLevel(string logLevel)
    {
        if (LevelSwitch is null)
        {
            return;
        }

        var newLevel = ParseLogLevel(logLevel);
        if (LevelSwitch.MinimumLevel != newLevel)
        {
            LevelSwitch.MinimumLevel = newLevel;
            SerilogLog.Information("Log level changed to {Level}", logLevel);
        }
    }

    private static LogEventLevel ParseLogLevel(string level)
    {
        return level?.ToUpperInvariant() switch
        {
            "VERBOSE" => LogEventLevel.Verbose,
            "DEBUG" => LogEventLevel.Debug,
            "INFORMATION" => LogEventLevel.Information,
            "WARNING" => LogEventLevel.Warning,
            "ERROR" => LogEventLevel.Error,
            "FATAL" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
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

    private static void WriteDiagnosticToStderr(string message)
    {
        try
        {
            Console.Error.WriteLine(message);
        }
        catch (IOException) { /* Empty */ }
        catch (ObjectDisposedException) { /* Empty */ }
    }
}
