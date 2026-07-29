namespace CrossMacro.Daemon.Logging;

internal sealed class DaemonSerilogCoreLogger : ICoreLogger
{
    public bool IsEnabled(CoreLogLevel level) => SerilogLog.IsEnabled(MapLevel(level));
    public void Verbose(string messageTemplate, params object?[] propertyValues) => SerilogLog.Verbose(messageTemplate, propertyValues);
    public void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues) => SerilogLog.Verbose(exception, messageTemplate, propertyValues);
    public void Debug(string messageTemplate, params object?[] propertyValues) => SerilogLog.Debug(messageTemplate, propertyValues);
    public void Debug(Exception exception, string messageTemplate, params object?[] propertyValues) => SerilogLog.Debug(exception, messageTemplate, propertyValues);
    public void Information(string messageTemplate, params object?[] propertyValues) => SerilogLog.Information(messageTemplate, propertyValues);
    public void Information(Exception exception, string messageTemplate, params object?[] propertyValues) => SerilogLog.Information(exception, messageTemplate, propertyValues);
    public void Warning(string messageTemplate, params object?[] propertyValues) => SerilogLog.Warning(messageTemplate, propertyValues);
    public void Warning(Exception exception, string messageTemplate, params object?[] propertyValues) => SerilogLog.Warning(exception, messageTemplate, propertyValues);
    public void LogError(string messageTemplate, params object?[] propertyValues) => SerilogLog.Error(messageTemplate, propertyValues);
    public void LogError(Exception exception, string messageTemplate, params object?[] propertyValues) => SerilogLog.Error(exception, messageTemplate, propertyValues);
    public void Fatal(string messageTemplate, params object?[] propertyValues) => SerilogLog.Fatal(messageTemplate, propertyValues);
    public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues) => SerilogLog.Fatal(exception, messageTemplate, propertyValues);

    private static LogEventLevel MapLevel(CoreLogLevel level) => level switch
    {
        CoreLogLevel.Verbose => LogEventLevel.Verbose,
        CoreLogLevel.Debug => LogEventLevel.Debug,
        CoreLogLevel.Information => LogEventLevel.Information,
        CoreLogLevel.Warning => LogEventLevel.Warning,
        CoreLogLevel.Error => LogEventLevel.Error,
        CoreLogLevel.Fatal => LogEventLevel.Fatal,
        _ => LogEventLevel.Information,
    };
}
