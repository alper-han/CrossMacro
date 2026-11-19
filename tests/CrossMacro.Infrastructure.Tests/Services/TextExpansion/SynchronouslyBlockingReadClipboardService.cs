namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

/// <summary>Blocks the calling thread inside <see cref="GetTextAsync"/> before any Task is returned.</summary>
internal sealed class SynchronouslyBlockingReadClipboardService : IClipboardService
{
    private static readonly TimeSpan BlockedThreadUpperBound = TimeSpan.FromSeconds(30);

    private readonly AsyncSignal _readStarted = new();

    public bool IsSupported => true;

    public Task WaitForReadStartedAsync(TimeSpan timeout) => _readStarted.WaitAsync(timeout);

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        _readStarted.Signal();
        _ = cancellationToken.WaitHandle.WaitOne(BlockedThreadUpperBound);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>("replacement");
    }
}
