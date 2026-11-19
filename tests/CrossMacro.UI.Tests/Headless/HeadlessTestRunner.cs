using Avalonia.Headless;

namespace CrossMacro.UI.Tests.Headless;

internal static class HeadlessTestRunner
{
    /// <summary>Runs <paramref name="action"/> on the dispatcher of a fresh headless Avalonia session.</summary>
    public static async Task RunAsync(Func<CancellationToken, Task> action)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TestTimeouts.Default);
        var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessStartupApplication));
        try
        {
            await session.Dispatch(async () =>
            {
                await action(timeout.Token);
                return true;
            }, timeout.Token).WaitAsync(timeout.Token);
        }
        finally
        {
            // Dispatch completion may resume inline on the UI thread; never join that thread from itself.
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    public static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
    {
        while (!condition())
        {
            await Task.Delay(1, token);
        }
    }
}
