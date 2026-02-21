using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Platform.Linux.Extensions;
using CrossMacro.TestInfrastructure;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Extensions;

public class LoggingExtensionsTests
{
    private static readonly object LoggerSync = new();

    private class TestSink : ILogEventSink
    {
        public ConcurrentBag<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    [LinuxFact]
    public void LogOnce_ShouldLogOnlyOnce_WhenCalledMultipleTimesWithSameKey()
    {
        lock (LoggerSync)
        {
            // Arrange
            var originalLogger = Log.Logger;
            var sink = new TestSink();
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Sink(sink)
                    .CreateLogger();

                var key = Guid.NewGuid().ToString();
                var message = $"Test message {Guid.NewGuid():N} {{0}}";
                var arg = "Arg";

                // Act
                LoggingExtensions.LogOnce(key, message, arg);
                LoggingExtensions.LogOnce(key, message, arg);
                LoggingExtensions.LogOnce(key, message, arg);

                // Assert
                Assert.Single(sink.Events, e => e.MessageTemplate.Text == message);
            }
            finally
            {
                Log.Logger = originalLogger;
            }
        }
    }

    [LinuxFact]
    public void LogOnce_ShouldLogMultipleTimes_WhenCalledWithDifferentKeys()
    {
        lock (LoggerSync)
        {
            // Arrange
            var originalLogger = Log.Logger;
            var sink = new TestSink();
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Sink(sink)
                    .CreateLogger();

                var key1 = Guid.NewGuid().ToString();
                var key2 = Guid.NewGuid().ToString();
                var message = $"Test message {Guid.NewGuid():N} {{0}}";
                var arg = "Arg";

                // Act
                LoggingExtensions.LogOnce(key1, message, arg);
                LoggingExtensions.LogOnce(key2, message, arg);

                // Assert
                Assert.Equal(2, sink.Events.Count(e => e.MessageTemplate.Text == message));
            }
            finally
            {
                Log.Logger = originalLogger;
            }
        }
    }
}
