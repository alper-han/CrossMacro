namespace CrossMacro.Daemon.Logging;

internal static class DaemonLoggerSetup
{
    public static LoggingLevelSwitch? LevelSwitch { get; private set; }

    public static void Initialize(string logLevel = "Information")
    {
        LevelSwitch = new LoggingLevelSwitch(ParseLogLevel(logLevel));
        SerilogLog.Logger = new Serilog.LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch)
            .WriteTo.Sink(new DaemonConsoleSink())
            .CreateLogger();
        CrossMacro.Core.Logging.Log.Configure(new DaemonSerilogCoreLogger());
        SerilogLog.Debug("Logger initialized. Level: {Level}", logLevel);
    }

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
