namespace CrossMacro.Platform.Windows.Tests.Services;

[SupportedOSPlatform("windows")]
[Collection(nameof(WindowsClipboardSerialization))]
public sealed class WindowsNativeClipboardServiceTests
{
    [WindowsFact]
    public async Task SetTextAsync_WhenClipboardIsLocked_ReportsFailure()
    {
        using var serviceThread = new StaMessageThread("CrossMacro_TestNativeClipboardService");
        var service = new WindowsNativeClipboardService(new Lazy<StaMessageThread>(() => serviceThread));
        await using var clipboardLock = await ClipboardLock.AcquireAsync(CancellationToken.None);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetTextAsync(string.Empty, CancellationToken.None));
    }

    [WindowsFact]
    public async Task SetTextAsync_WithUnicodeText_WritesNormalizedTextAndClearRemovesIt()
    {
        using var thread = new StaMessageThread("CrossMacro_TestNativeClipboardRoundTrip");
        var service = new WindowsNativeClipboardService(new Lazy<StaMessageThread>(() => thread));
        var originalText = await service.GetTextAsync(CancellationToken.None);

        try
        {
            await service.SetTextAsync("Merhaba, \u0130stanbul \ud83d\udc4b\nSecond line", CancellationToken.None);

            Assert.Equal("Merhaba, \u0130stanbul \ud83d\udc4b\r\nSecond line", await service.GetTextAsync(CancellationToken.None));

            await service.SetTextAsync(string.Empty, CancellationToken.None);

            Assert.Null(await service.GetTextAsync(CancellationToken.None));
        }
        finally
        {
            await service.SetTextAsync(originalText ?? string.Empty, CancellationToken.None);
        }
    }

    [WindowsFact]
    public async Task ClipboardOperations_WhenAlreadyCanceled_DoNotInitializeStaThread()
    {
        var thread = new Lazy<StaMessageThread>(() => throw new InvalidOperationException("STA thread should not be initialized."));
        var service = new WindowsNativeClipboardService(thread);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SetTextAsync("text", cancellation.Token));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetTextAsync(cancellation.Token));

        Assert.False(thread.IsValueCreated);
    }

    private sealed class ClipboardLock : IAsyncDisposable
    {
        private readonly StaMessageThread _thread = new("CrossMacro_TestNativeClipboardLock");
        private readonly TaskCompletionSource _clipboardOpened = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseClipboard = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task? _holdClipboardTask;

        public static async Task<ClipboardLock> AcquireAsync(CancellationToken cancellationToken = default)
        {
            var clipboardLock = new ClipboardLock();
            try
            {
                clipboardLock._holdClipboardTask = clipboardLock._thread.InvokeAsync(() =>
                {
                    if (!User32.OpenClipboard(clipboardLock._thread.MessageWindowHandle))
                    {
                        throw new InvalidOperationException("Failed to acquire the test clipboard lock.");
                    }

                    _ = clipboardLock._clipboardOpened.TrySetResult();
                    var clipboardClosed = false;
                    try
                    {
                        clipboardLock._releaseClipboard.Task.GetAwaiter().GetResult();
                    }
                    finally
                    {
                        clipboardClosed = User32.CloseClipboard();
                    }

                    if (!clipboardClosed)
                    {
                        throw new InvalidOperationException("Failed to release the test clipboard lock.");
                    }
                }, cancellationToken);

                var completedTask = await Task.WhenAny(clipboardLock._clipboardOpened.Task, clipboardLock._holdClipboardTask);
                if (completedTask == clipboardLock._holdClipboardTask)
                {
                    await clipboardLock._holdClipboardTask;
                }

                await clipboardLock._clipboardOpened.Task;
                return clipboardLock;
            }
            catch
            {
                await clipboardLock.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _ = _releaseClipboard.TrySetResult();
            try
            {
                if (_holdClipboardTask is not null)
                {
                    await _holdClipboardTask;
                }
            }
            finally
            {
                _thread.Dispose();
            }
        }
    }
}
