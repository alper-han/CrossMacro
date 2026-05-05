using CoreLogging = CrossMacro.Core.Logging;

namespace CrossMacro.Cli.Tests;

internal static class ConsoleTestLock
{
    private static readonly TimeSpan AcquireTimeout = TimeSpan.FromSeconds(5);

    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static async Task<IDisposable> AcquireAsync()
    {
        if (!await Gate.WaitAsync(AcquireTimeout))
        {
            throw new TimeoutException("Timed out waiting for the CLI console test lock.");
        }

        return new Releaser(CoreLogging.Log.PushLogger(new NoOpCoreLogger()));
    }

    private sealed class Releaser : IDisposable
    {
        private IDisposable? _loggerScope;

        public Releaser(IDisposable loggerScope)
        {
            _loggerScope = loggerScope;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _loggerScope, null)?.Dispose();
            Gate.Release();
        }
    }

    private sealed class NoOpCoreLogger : CoreLogging.ICoreLogger
    {
        public bool IsEnabled(CoreLogging.CoreLogLevel level) => false;

        public void Verbose(string messageTemplate, params object?[] propertyValues) { }
        public void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues) { }
        public void Debug(string messageTemplate, params object?[] propertyValues) { }
        public void Debug(Exception exception, string messageTemplate, params object?[] propertyValues) { }
        public void Information(string messageTemplate, params object?[] propertyValues) { }
        public void Information(Exception exception, string messageTemplate, params object?[] propertyValues) { }
        public void Warning(string messageTemplate, params object?[] propertyValues) { }
        public void Warning(Exception exception, string messageTemplate, params object?[] propertyValues) { }
        public void Error(string messageTemplate, params object?[] propertyValues) { }
        public void Error(Exception exception, string messageTemplate, params object?[] propertyValues) { }
        public void Fatal(string messageTemplate, params object?[] propertyValues) { }
        public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues) { }
    }
}
