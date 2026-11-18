using CrossMacro.Core.Logging;

namespace CrossMacro.Infrastructure.Tests.TestDoubles;

/// <summary>
/// Captures everything written through <see cref="Log"/>. Installing it with <c>Log.PushLogger</c> swaps a
/// process-global field, so every test that does so belongs to <see cref="GlobalCoreLoggerGroup"/>.
/// </summary>
internal sealed class RecordingCoreLogger : ICoreLogger
{
    private readonly ConcurrentQueue<RecordedLogEntry> _entries = new();

    public IReadOnlyCollection<RecordedLogEntry> Entries => _entries;

    public bool IsEnabled(CoreLogLevel level) => true;

    public void Verbose(string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Verbose, exception: null, messageTemplate, propertyValues);

    public void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Verbose, exception, messageTemplate, propertyValues);

    public void Debug(string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Debug, exception: null, messageTemplate, propertyValues);

    public void Debug(Exception exception, string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Debug, exception, messageTemplate, propertyValues);

    public void Information(string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Information, exception: null, messageTemplate, propertyValues);

    public void Information(Exception exception, string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Information, exception, messageTemplate, propertyValues);

    public void Warning(string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Warning, exception: null, messageTemplate, propertyValues);

    public void Warning(Exception exception, string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Warning, exception, messageTemplate, propertyValues);

    public void LogError(string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Error, exception: null, messageTemplate, propertyValues);

    public void LogError(Exception exception, string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Error, exception, messageTemplate, propertyValues);

    public void Fatal(string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Fatal, exception: null, messageTemplate, propertyValues);

    public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues) =>
        Record(CoreLogLevel.Fatal, exception, messageTemplate, propertyValues);

    private void Record(CoreLogLevel level, Exception? exception, string messageTemplate, object?[] propertyValues) =>
        _entries.Enqueue(new RecordedLogEntry(level, messageTemplate, propertyValues, exception));
}
