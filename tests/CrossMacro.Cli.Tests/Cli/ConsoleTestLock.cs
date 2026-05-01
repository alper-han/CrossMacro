namespace CrossMacro.Cli.Tests;

internal static class ConsoleTestLock
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static async Task<IDisposable> AcquireAsync()
    {
        await Gate.WaitAsync();
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
