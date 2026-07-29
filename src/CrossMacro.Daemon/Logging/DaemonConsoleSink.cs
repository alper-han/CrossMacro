namespace CrossMacro.Daemon.Logging;

internal sealed class DaemonConsoleSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        var timestamp = logEvent.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        var line = $"[{timestamp} {ShortLevel(logEvent.Level)}] {logEvent.RenderMessage(CultureInfo.InvariantCulture)}";
        if (logEvent.Exception is not null)
        {
            line += Environment.NewLine + logEvent.Exception;
        }

        try
        {
            Console.WriteLine(line);
        }
        catch (IOException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }
    }

    private static string ShortLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "VRB",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        LogEventLevel.Fatal => "FTL",
        _ => "???",
    };
}
