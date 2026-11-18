namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

/// <summary>The write never finishes on its own; it only ends when the executor cancels it.</summary>
internal sealed class BlockingClipboardService : IClipboardService
{
    private readonly TaskCompletionSource _cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsSupported => true;

    public Task CancellationObserved => _cancellationObserved.Task;

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _ = _cancellationObserved.TrySetResult();
            throw;
        }
    }
}
