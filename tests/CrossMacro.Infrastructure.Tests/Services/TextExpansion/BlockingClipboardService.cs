namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

/// <summary>
/// The write never finishes on its own; it only ends when the executor cancels it. Cancelling before the
/// write is entered skips it entirely, so tests wait for <see cref="WriteStarted"/> before timing it out.
/// </summary>
internal sealed class BlockingClipboardService : IClipboardService
{
    private readonly TaskCompletionSource _writeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsSupported => true;

    public Task WriteStarted => _writeStarted.Task;

    public Task CancellationObserved => _cancellationObserved.Task;

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        _ = _writeStarted.TrySetResult();
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
