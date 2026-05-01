using System;
using System.Collections.Concurrent;
using System.Linq;
using CoreLogging = CrossMacro.Core.Logging;
using CrossMacro.Platform.Linux.Extensions;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Extensions;

public class LoggingExtensionsTests
{
    private static readonly object LoggerSync = new();

    private sealed class TestCoreLogger : CoreLogging.ICoreLogger
    {
        public ConcurrentBag<TestCoreLogEntry> Entries { get; } = new();

        public bool IsEnabled(CoreLogging.CoreLogLevel level) => true;

        public void Verbose(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Debug(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Debug(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Information(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Information(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Warning(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Warning(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Error(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Error(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Fatal(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(null, messageTemplate, propertyValues));

        public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));
    }

    private sealed record TestCoreLogEntry(Exception? Exception, string MessageTemplate, object?[] PropertyValues);

    [LinuxFact]
    public void LogOnce_ShouldLogOnlyOnce_WhenCalledMultipleTimesWithSameKey()
    {
        lock (LoggerSync)
        {
            var logger = new TestCoreLogger();
            using var _ = CoreLogging.Log.PushLogger(logger);

            var key = Guid.NewGuid().ToString();
            var message = $"Test message {Guid.NewGuid():N} {{0}}";
            var arg = "Arg";

            LoggingExtensions.LogOnce(key, message, arg);
            LoggingExtensions.LogOnce(key, message, arg);
            LoggingExtensions.LogOnce(key, message, arg);

            Assert.Single(logger.Entries, e => e.MessageTemplate == message);
        }
    }

    [LinuxFact]
    public void LogOnce_ShouldLogMultipleTimes_WhenCalledWithDifferentKeys()
    {
        lock (LoggerSync)
        {
            var logger = new TestCoreLogger();
            using var _ = CoreLogging.Log.PushLogger(logger);

            var key1 = Guid.NewGuid().ToString();
            var key2 = Guid.NewGuid().ToString();
            var message = $"Test message {Guid.NewGuid():N} {{0}}";
            var arg = "Arg";

            LoggingExtensions.LogOnce(key1, message, arg);
            LoggingExtensions.LogOnce(key2, message, arg);

            Assert.Equal(2, logger.Entries.Count(e => e.MessageTemplate == message));
        }
    }
}
