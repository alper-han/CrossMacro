namespace CrossMacro.UI.Tests.Cli;

internal static class ConsoleTestLock
{
    internal static readonly object Gate = new();

    private static readonly SemaphoreSlim AsyncGate = new(1, 1);

    internal static async Task<IDisposable> AcquireAsync()
    {
        await AsyncGate.WaitAsync();
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
