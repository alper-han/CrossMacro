namespace CrossMacro.UI.Tests.Cli;

internal static class ConsoleTestLock
{
    private static readonly TimeSpan AcquireTimeout = TimeSpan.FromSeconds(5);

    internal static readonly object Gate = new();

    private static readonly SemaphoreSlim AsyncGate = new(1, 1);

    internal static async Task<IDisposable> AcquireAsync()
    {
        if (!await AsyncGate.WaitAsync(AcquireTimeout))
        {
            throw new TimeoutException("Timed out waiting for the UI CLI console test lock.");
        }

        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        public void Dispose()
        {
            AsyncGate.Release();
        }
    }
}
