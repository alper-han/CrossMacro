namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSNativeClipboardServiceTests
{
    [Fact]
    public async Task TextAndPngOperations_UseTheNativeBackend()
    {
        var backend = new FakeClipboardBackend();
        using var service = new MacOSNativeClipboardService(backend, isMacOS: () => true);
        byte[] png = [137, 80, 78, 71];

        await service.SetTextAsync("Merhaba", CancellationToken.None);
        await service.SetPngAsync(png, CancellationToken.None);

        Assert.Equal("Merhaba", await service.GetTextAsync(CancellationToken.None));
        Assert.Equal(png, await service.GetPngAsync(1024, CancellationToken.None));
        Assert.Equal(1, backend.TextWrites);
        Assert.Equal(1, backend.PngWrites);
    }

    [Fact]
    public async Task SetPngAsync_WithEmptyPayload_DoesNotWrite()
    {
        var backend = new FakeClipboardBackend();
        using var service = new MacOSNativeClipboardService(backend, isMacOS: () => true);

        await service.SetPngAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.Equal(0, backend.PngWrites);
    }

    [Fact]
    public async Task GetPngAsync_WithInvalidMaximum_ThrowsBeforeUsingBackend()
    {
        var backend = new FakeClipboardBackend();
        using var service = new MacOSNativeClipboardService(backend, isMacOS: () => true);

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetPngAsync(0, CancellationToken.None));
    }

    [Fact]
    public async Task Operations_WhenNativePasteboardIsUnavailable_ReportImageClipboardUnavailability()
    {
        var backend = new FakeClipboardBackend { IsAvailable = false };
        using var service = new MacOSNativeClipboardService(backend, isMacOS: () => true);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetTextAsync("text", CancellationToken.None));
        _ = await Assert.ThrowsAsync<ImageClipboardUnavailableException>(() => service.GetPngAsync(1024, CancellationToken.None));
    }

    [Fact]
    public async Task SetTextAsync_WhenPasteboardWriteFails_Throws()
    {
        var backend = new FakeClipboardBackend { TextWriteSucceeds = false };
        using var service = new MacOSNativeClipboardService(backend, isMacOS: () => true);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetTextAsync("text", CancellationToken.None));
    }

    [Fact]
    public async Task SetPngAsync_WhenEmptyAndAlreadyCanceled_PropagatesCancellation()
    {
        var backend = new FakeClipboardBackend();
        using var service = new MacOSNativeClipboardService(backend, isMacOS: () => true);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SetPngAsync(ReadOnlyMemory<byte>.Empty, cancellation.Token));
    }

    [Fact]
    public async Task Dispose_IsIdempotentAndOperationsReportDisposedState()
    {
        var service = new MacOSNativeClipboardService(new FakeClipboardBackend(), isMacOS: () => true);

        service.Dispose();
        service.Dispose();

        Assert.False(service.IsSupported);
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(
            () => service.GetTextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Dispose_WhenOperationAndWaiterAreActive_AllStartedOperationsComplete()
    {
        using var firstEntered = new ManualResetEventSlim(initialState: false);
        using var releaseFirst = new ManualResetEventSlim(initialState: false);
        var backend = new FakeClipboardBackend
        {
            OnGetText = () =>
            {
                firstEntered.Set();
                _ = releaseFirst.Wait(TimeSpan.FromSeconds(2), CancellationToken.None);
            },
        };
        var service = new MacOSNativeClipboardService(backend, isMacOS: () => true);
        Task<string?> first = service.GetTextAsync(CancellationToken.None);
        Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(1), CancellationToken.None));
        Task second = service.SetTextAsync("queued", CancellationToken.None);

        Task dispose = Task.Run(service.Dispose, CancellationToken.None);
        await dispose.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);

        releaseFirst.Set();
        await Task.WhenAll(first, second).WaitAsync(
            TimeSpan.FromSeconds(2),
            TimeProvider.System,
            CancellationToken.None);
        Assert.Equal(1, backend.TextWrites);
    }

    private sealed class FakeClipboardBackend : IMacOSClipboardBackend
    {
        public bool IsAvailable { get; set; } = true;
        public bool TextWriteSucceeds { get; set; } = true;
        public bool PngWriteSucceeds { get; set; } = true;
        public int TextWrites { get; private set; }
        public int PngWrites { get; private set; }
        public Action? OnGetText { get; init; }
        private string? Text { get; set; }
        private byte[]? Png { get; set; }

        public bool TrySetText(string text)
        {
            TextWrites++;
            Text = text;
            return TextWriteSucceeds;
        }

        public string? GetText()
        {
            OnGetText?.Invoke();
            return Text;
        }

        public bool TrySetPng(byte[] pngBytes)
        {
            PngWrites++;
            Png = pngBytes;
            return PngWriteSucceeds;
        }

        public byte[]? GetPng(int maximumBytes) => Png;
    }
}
