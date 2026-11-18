namespace CrossMacro.Daemon.Tests.Logging;

public sealed class DaemonLoggingCompositionTests
{
    [Fact]
    public void CoreConsoleLoggerRendersPropertiesAndExceptionsAndHonorsRuntimeLevel()
    {
        var originalOut = Console.Out;
        var originalLogger = Serilog.Log.Logger;
        using var coreLoggerScope = CrossMacro.Core.Logging.Log.PushLogger(new NoOpCoreLogger());
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        Console.SetOut(output);

        try
        {
            DaemonLoggerSetup.Initialize("Debug");
            CrossMacro.Core.Logging.Log.Information("daemon event {Value}", 42);
            CrossMacro.Core.Logging.Log.LogError(new InvalidOperationException("boom"), "daemon failure");
            Assert.Equal("Information", DaemonLoggerSetup.ToggleDebugLevel());
            CrossMacro.Core.Logging.Log.Debug("hidden debug event");
            CrossMacro.Core.Logging.Log.Information("visible information event");
            Assert.Equal("Debug", DaemonLoggerSetup.ToggleDebugLevel());
            CrossMacro.Core.Logging.Log.Debug("debug event after toggling back");

            var rendered = output.ToString();
            Assert.Contains("daemon event 42", rendered, StringComparison.Ordinal);
            Assert.Contains("daemon failure", rendered, StringComparison.Ordinal);
            Assert.Contains("InvalidOperationException: boom", rendered, StringComparison.Ordinal);
            Assert.DoesNotContain("hidden debug event", rendered, StringComparison.Ordinal);
            Assert.Contains("visible information event", rendered, StringComparison.Ordinal);
            Assert.Contains("debug event after toggling back", rendered, StringComparison.Ordinal);
        }
        finally
        {
            Serilog.Log.CloseAndFlush();
            Serilog.Log.Logger = originalLogger;
            Console.SetOut(originalOut);
        }
    }

    private sealed class NoOpCoreLogger : CrossMacro.Core.Logging.ICoreLogger
    {
        public bool IsEnabled(CrossMacro.Core.Logging.CoreLogLevel level) => true;

        public void Verbose(string messageTemplate, params object?[] propertyValues)
        {
        }

        public void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues)
        {
        }

        public void Debug(string messageTemplate, params object?[] propertyValues)
        {
        }

        public void Debug(Exception exception, string messageTemplate, params object?[] propertyValues)
        {
        }

        public void Information(string messageTemplate, params object?[] propertyValues)
        {
        }

        public void Information(Exception exception, string messageTemplate, params object?[] propertyValues)
        {
        }

        public void Warning(string messageTemplate, params object?[] propertyValues)
        {
        }

        public void Warning(Exception exception, string messageTemplate, params object?[] propertyValues)
        {
        }

        public void LogError(string messageTemplate, params object?[] propertyValues)
        {
        }

        public void LogError(Exception exception, string messageTemplate, params object?[] propertyValues)
        {
        }

        public void Fatal(string messageTemplate, params object?[] propertyValues)
        {
        }

        public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues)
        {
        }
    }
}
