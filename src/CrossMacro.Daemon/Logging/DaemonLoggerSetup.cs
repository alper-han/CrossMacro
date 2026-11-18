namespace CrossMacro.Daemon.Logging;

internal static class DaemonLoggerSetup
{
    private static LoggingLevelSwitch? _levelSwitch;

    public static void Initialize(string logLevel = "Information")
    {
        var levelSwitch = new LoggingLevelSwitch(ParseLogLevel(logLevel));
        _levelSwitch = levelSwitch;
        SerilogLog.Logger = new Serilog.LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.Sink(new DaemonConsoleSink())
            .CreateLogger();
        CrossMacro.Core.Logging.Log.Configure(new DaemonSerilogCoreLogger());
        CrossMacro.Core.Logging.Log.Debug("Logger initialized. Level: {Level}", logLevel);
    }

    /// <summary>Switches between Debug and Information; returns the new level name, or null before initialization.</summary>
    public static string? ToggleDebugLevel()
    {
        var levelSwitch = _levelSwitch;
        if (levelSwitch is null)
        {
            return null;
        }

        var next = levelSwitch.MinimumLevel is LogEventLevel.Debug ? LogEventLevel.Information : LogEventLevel.Debug;
        levelSwitch.MinimumLevel = next;
        return next.ToString();
    }

    private static LogEventLevel ParseLogLevel(string level) => level?.ToUpperInvariant() switch
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
