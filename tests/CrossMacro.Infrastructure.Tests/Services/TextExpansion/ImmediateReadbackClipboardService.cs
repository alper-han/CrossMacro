namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

internal sealed class ImmediateReadbackClipboardService :
    IClipboardService,
    IClipboardWriteReadbackCapability
{
    private int _readCount;

    public bool IsSupported => true;

    public bool GuaranteesImmediateReadback => true;

    public int ReadCount => Volatile.Read(ref _readCount);

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        var readCount = Interlocked.Increment(ref _readCount);
        return Task.FromResult<string?>(readCount is 1 ? "old-value" : "replacement");
    }
}
