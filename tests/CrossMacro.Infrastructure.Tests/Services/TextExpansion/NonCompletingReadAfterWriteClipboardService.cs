namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

/// <summary>The backup read succeeds; every later read never completes on its own.</summary>
internal sealed class NonCompletingReadAfterWriteClipboardService : IClipboardService
{
    private int _readCount;

    public bool IsSupported => true;

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _readCount) is 1)
        {
            return "old-value";
        }

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return "replacement";
    }
}
