
namespace CrossMacro.Cli.Services;

internal static class CommandTimeoutRunner
{
    public static async Task<TResult> RunAsync<TResult>(
        int timeoutSeconds,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<TResult>> action)
    {
        if (timeoutSeconds > 0)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            return await action(timeoutCts.Token).ConfigureAwait(false);
        }

        return await action(cancellationToken).ConfigureAwait(false);
    }
}
