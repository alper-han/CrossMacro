
namespace CrossMacro.Platform.Linux.Tests.Extensions;

public sealed class LoggingExtensionsTests
{
    private sealed class TestCoreLogger : CoreLogging.ICoreLogger
    {
        public ConcurrentBag<TestCoreLogEntry> Entries { get; } = new();

        public bool IsEnabled(CoreLogging.CoreLogLevel level) => true;

        public void Verbose(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(Exception: null, messageTemplate, propertyValues));

        public void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Debug(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(Exception: null, messageTemplate, propertyValues));

        public void Debug(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Information(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(Exception: null, messageTemplate, propertyValues));

        public void Information(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Warning(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(Exception: null, messageTemplate, propertyValues));

        public void Warning(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void LogError(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(Exception: null, messageTemplate, propertyValues));

        public void LogError(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));

        public void Fatal(string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(Exception: null, messageTemplate, propertyValues));

        public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues) =>
            Entries.Add(new TestCoreLogEntry(exception, messageTemplate, propertyValues));
    }

    private sealed record TestCoreLogEntry(Exception? Exception, string MessageTemplate, object?[] PropertyValues);

    [Fact]
    public void LogOnce_ShouldLogOnlyOnce_WhenCalledMultipleTimesWithSameKey()
    {
        var logger = new TestCoreLogger();
        using var loggingScope = CoreLogging.Log.PushLogger(logger);
        var key = Guid.NewGuid().ToString();
        var message = $"Test message {Guid.NewGuid():N} {{0}}";
        const string arg = "Arg";

        LoggingExtensions.LogOnce(key, message, arg);
        LoggingExtensions.LogOnce(key, message, arg);
        LoggingExtensions.LogOnce(key, message, arg);

        _ = Assert.Single(logger.Entries, e => string.Equals(e.MessageTemplate, message, StringComparison.Ordinal));
    }

    [Fact]
    public void LogOnce_ShouldLogMultipleTimes_WhenCalledWithDifferentKeys()
    {
        var logger = new TestCoreLogger();
        using var loggingScope = CoreLogging.Log.PushLogger(logger);
        var key1 = Guid.NewGuid().ToString();
        var key2 = Guid.NewGuid().ToString();
        var message = $"Test message {Guid.NewGuid():N} {{0}}";
        const string arg = "Arg";

        LoggingExtensions.LogOnce(key1, message, arg);
        LoggingExtensions.LogOnce(key2, message, arg);

        Assert.Equal(2, logger.Entries.Count(e => string.Equals(e.MessageTemplate, message, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task LogOnce_ShouldLogOnlyOnce_WhenCalledConcurrentlyWithSameKey()
    {
        var logger = new TestCoreLogger();
        using var loggingScope = CoreLogging.Log.PushLogger(logger);
        const int callerCount = 32;
        var key = Guid.NewGuid().ToString();
        const string message = "Concurrent log message";
        using var start = new ManualResetEventSlim(initialState: false);
        var callers = Enumerable.Range(0, callerCount)
            .Select(_ => Task.Run(() =>
            {
                start.Wait(TestContext.Current.CancellationToken);
                LoggingExtensions.LogOnce(key, message);
            }, TestContext.Current.CancellationToken))
            .ToArray();

        start.Set();
        await Task.WhenAll(callers);

        _ = Assert.Single(logger.Entries, e => string.Equals(e.MessageTemplate, message, StringComparison.Ordinal));
    }
}
