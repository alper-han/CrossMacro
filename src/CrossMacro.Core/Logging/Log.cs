
namespace CrossMacro.Core.Logging;

public static class Log
{
    private static readonly ICoreLogger BootstrapLogger = new BootstrapCoreLogger();
    private static ICoreLogger _logger = BootstrapLogger;

    public static IDisposable PushLogger(ICoreLogger logger)
    {
        var previousLogger = _logger;
        Configure(logger);
        return new LoggerRestoreScope(previousLogger);
    }

    public static void Configure(ICoreLogger logger)
    {
        _logger = logger ?? BootstrapLogger;
    }

    public static bool IsEnabled(CoreLogLevel level)
    {
        return _logger.IsEnabled(level);
    }

    public static void Verbose(string messageTemplate, params object?[] propertyValues)
    {
        _logger.Verbose(messageTemplate, propertyValues);
    }

    public static void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        _logger.Verbose(exception, messageTemplate, propertyValues);
    }

    public static void Debug(string messageTemplate, params object?[] propertyValues)
    {
        _logger.Debug(messageTemplate, propertyValues);
    }

    public static void Debug(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        _logger.Debug(exception, messageTemplate, propertyValues);
    }

    public static void Information(string messageTemplate, params object?[] propertyValues)
    {
        _logger.Information(messageTemplate, propertyValues);
    }

    public static void Information(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        _logger.Information(exception, messageTemplate, propertyValues);
    }

    public static void Warning(string messageTemplate, params object?[] propertyValues)
    {
        _logger.Warning(messageTemplate, propertyValues);
    }

    public static void Warning(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        _logger.Warning(exception, messageTemplate, propertyValues);
    }

    public static void LogError(string messageTemplate, params object?[] propertyValues)
    {
        _logger.LogError(messageTemplate, propertyValues);
    }

    public static void LogError(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        _logger.LogError(exception, messageTemplate, propertyValues);
    }

    public static void Fatal(string messageTemplate, params object?[] propertyValues)
    {
        _logger.Fatal(messageTemplate, propertyValues);
    }

    public static void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues)
    {
        _logger.Fatal(exception, messageTemplate, propertyValues);
    }

    private sealed class BootstrapCoreLogger : ICoreLogger
    {
        private const string BootstrapWarning =
            "[CrossMacro] Core logger is using bootstrap fallback. Call LoggerSetup.Initialize early for structured logging.";
        private int _bootstrapWarningEmitted;

        bool ICoreLogger.IsEnabled(CoreLogLevel level)
        {
            return level >= CoreLogLevel.Warning;
        }

        void ICoreLogger.Verbose(string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Verbose, exception: null, messageTemplate, propertyValues);

        void ICoreLogger.Verbose(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Verbose, exception, messageTemplate, propertyValues);

        void ICoreLogger.Debug(string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Debug, exception: null, messageTemplate, propertyValues);

        void ICoreLogger.Debug(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Debug, exception, messageTemplate, propertyValues);

        void ICoreLogger.Information(string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Information, exception: null, messageTemplate, propertyValues);

        void ICoreLogger.Information(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Information, exception, messageTemplate, propertyValues);

        void ICoreLogger.Warning(string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Warning, exception: null, messageTemplate, propertyValues);

        void ICoreLogger.Warning(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Warning, exception, messageTemplate, propertyValues);

        void ICoreLogger.LogError(string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Error, exception: null, messageTemplate, propertyValues);

        void ICoreLogger.LogError(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Error, exception, messageTemplate, propertyValues);

        void ICoreLogger.Fatal(string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Fatal, exception: null, messageTemplate, propertyValues);

        void ICoreLogger.Fatal(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Write(CoreLogLevel.Fatal, exception, messageTemplate, propertyValues);

        private void Write(
            CoreLogLevel level,
            Exception? exception,
            string messageTemplate,
            object?[] propertyValues)
        {
            EmitBootstrapWarning();
            if (level < CoreLogLevel.Warning)
            {
                return;
            }

            var builder = new StringBuilder(256);
            builder.Append('[').Append(DateTimeOffset.UtcNow.ToString("O")).Append("] ");
            builder.Append("[CrossMacro][").Append(level).Append("] ");
            builder.Append(FormatMessage(messageTemplate, propertyValues));

            if (exception is not null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            TryWriteLine(builder.ToString());
        }

        private void EmitBootstrapWarning()
        {
            if (Interlocked.Exchange(ref _bootstrapWarningEmitted, 1) is 1)
            {
                return;
            }

            TryWriteLine(BootstrapWarning);
        }

        private static string FormatMessage(string template, object?[] propertyValues)
        {
            if (propertyValues.Length is 0)
            {
                return template;
            }

            var builder = new StringBuilder(template.Length + 32);
            builder.Append(template);
            builder.Append(" | ");
            for (var i = 0; i < propertyValues.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(Convert.ToString(propertyValues[i], System.Globalization.CultureInfo.InvariantCulture) ?? "null");
            }

            return builder.ToString();
        }

        private static void TryWriteLine(string line)
        {
            try
            {
                Console.Error.WriteLine(line);
            }
            catch (IOException)
            {
                // Console is already closed or unavailable, ignore
            }
            catch (ObjectDisposedException)
            {
                // Console stream is disposed, ignore
            }
        }
    }

    private sealed class LoggerRestoreScope(ICoreLogger previousLogger) : IDisposable
    {
        private ICoreLogger? _previousLogger = previousLogger;

        public void Dispose()
        {
            var prev = Interlocked.Exchange(ref _previousLogger, value: null);
            if (prev is not null)
            {
                Configure(prev);
            }
        }
    }
}
