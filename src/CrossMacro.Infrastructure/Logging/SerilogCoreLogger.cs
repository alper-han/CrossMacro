using System;
using CrossMacro.Core.Logging;
using Serilog.Events;
using SerilogLog = Serilog.Log;

namespace CrossMacro.Infrastructure.Logging;

public sealed class SerilogCoreLogger : ICoreLogger
{
    public bool IsEnabled(CoreLogLevel level)
    {
        return SerilogLog.IsEnabled(MapLevel(level));
    }

    public void Verbose(string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Verbose(messageTemplate, propertyValues);
    }

    public void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Verbose(exception, messageTemplate, propertyValues);
    }

    public void Debug(string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Debug(messageTemplate, propertyValues);
    }

    public void Debug(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Debug(exception, messageTemplate, propertyValues);
    }

    public void Information(string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Information(messageTemplate, propertyValues);
    }

    public void Information(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Information(exception, messageTemplate, propertyValues);
    }

    public void Warning(string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Warning(messageTemplate, propertyValues);
    }

    public void Warning(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Warning(exception, messageTemplate, propertyValues);
    }

    public void Error(string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Error(messageTemplate, propertyValues);
    }

    public void Error(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Error(exception, messageTemplate, propertyValues);
    }

    public void Fatal(string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Fatal(messageTemplate, propertyValues);
    }

    public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        SerilogLog.Fatal(exception, messageTemplate, propertyValues);
    }

    private static LogEventLevel MapLevel(CoreLogLevel level)
    {
        return level switch
        {
            CoreLogLevel.Verbose => LogEventLevel.Verbose,
            CoreLogLevel.Debug => LogEventLevel.Debug,
            CoreLogLevel.Information => LogEventLevel.Information,
            CoreLogLevel.Warning => LogEventLevel.Warning,
            CoreLogLevel.Error => LogEventLevel.Error,
            CoreLogLevel.Fatal => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
