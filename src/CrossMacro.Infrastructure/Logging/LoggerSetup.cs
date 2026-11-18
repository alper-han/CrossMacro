
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
        bool enableConsoleLogging = true,
        bool writeConsoleToStandardError = false)
    {
        LevelSwitch = new LoggingLevelSwitch(ParseLogLevel(logLevel));

        var config = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch);

        if (enableConsoleLogging)
        {
            config = config.WriteTo.Console(
                formatProvider: CultureInfo.InvariantCulture,
                standardErrorFromLevel: writeConsoleToStandardError ? LogEventLevel.Verbose : null);
        }

        if (enableFileLogging)
        {
            try
            {
                var logDir = ApplicationPathsEnvironment.CaptureLogDirectory();
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

    private static void WriteDiagnosticToStderr(string message)
    {
        try
        {
            Console.Error.WriteLine(message);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // stderr itself is unusable, so the trace channel is the only place left to report it.
            Trace.TraceError("[LoggerSetup] Could not write a diagnostic to stderr: {0}", ex.Message);
        }
    }
}
