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

        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        public void Dispose()
        {
            Gate.Release();
        }
    }
}
