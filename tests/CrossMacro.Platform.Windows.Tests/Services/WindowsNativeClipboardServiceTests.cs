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

    [WindowsFact]
    public async Task GetPngAsync_AfterSetPngAsync_ReturnsTheNativePngClipboardFormat()
    {
        using var thread = new StaMessageThread("CrossMacro_TestNativeImageClipboardRoundTrip");
        var service = new WindowsNativeImageClipboardService(new Lazy<StaMessageThread>(() => thread));
        byte[] pngBytes = [137, 80, 78, 71, 13, 10, 26, 10];

        await service.SetPngAsync(pngBytes, CancellationToken.None);
        var result = await service.GetPngAsync(1024, CancellationToken.None);

        Assert.Equal(pngBytes, result);
    }

    [WindowsFact]
    public async Task GetPngAsync_WhenAlreadyCanceled_DoesNotInitializeStaThread()
    {
        var thread = new Lazy<StaMessageThread>(() => throw new InvalidOperationException("STA thread should not be initialized."));
        var service = new WindowsNativeImageClipboardService(thread);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetPngAsync(1024, cancellation.Token));

        Assert.False(thread.IsValueCreated);
    }

    [Fact]
    public void ReadPngFromClipboard_UsesImagePngFallbackAndAlwaysUnlocksAndCloses()
    {
        var pngBytes = new byte[] { 137, 80, 78, 71 };
        var handle = Marshal.AllocHGlobal(pngBytes.Length);
        var unlockCount = 0;
        var closeCount = 0;
        try
        {
            Marshal.Copy(pngBytes, 0, handle, pngBytes.Length);
            var result = WindowsNativeImageClipboardService.ReadPngFromClipboard(
                maximumBytes: 1024,
                pngFormat: 1,
                imagePngFormat: 2,
                hwndOwner: IntPtr.Zero,
                isClipboardFormatAvailable: format => format is 2,
                openClipboard: _ => true,
                getClipboardData: _ => handle,
                globalSize: _ => (UIntPtr)pngBytes.Length,
                globalLock: _ => handle,
                globalUnlock: _ =>
                {
                    unlockCount++;
                    return true;
                },
                closeClipboard: () =>
                {
                    closeCount++;
                    return true;
                });

            Assert.Equal(pngBytes, result);
            Assert.Equal(1, unlockCount);
            Assert.Equal(1, closeCount);
        }
        finally
        {
            Marshal.FreeHGlobal(handle);
        }
    }

    [Fact]
    public void ReadPngFromClipboard_WhenDataExceedsLimit_ClosesClipboardBeforeThrowing()
    {
        var closeCount = 0;

        _ = Assert.Throws<InvalidDataException>(() => WindowsNativeImageClipboardService.ReadPngFromClipboard(
            maximumBytes: 3,
            pngFormat: 1,
            imagePngFormat: 2,
            hwndOwner: IntPtr.Zero,
            isClipboardFormatAvailable: _ => true,
            openClipboard: _ => true,
            getClipboardData: _ => new IntPtr(1),
            globalSize: _ => (UIntPtr)4,
            globalLock: _ => IntPtr.Zero,
            globalUnlock: _ => true,
             closeClipboard: () =>
            {
                closeCount++;
                return true;
            }));

        Assert.Equal(1, closeCount);
    }

    [Fact]
    public void ReadPngFromClipboard_WhenClipboardCannotOpen_ThrowsWithoutClosing()
    {
        var closeCount = 0;

        _ = Assert.Throws<InvalidOperationException>(() => WindowsNativeImageClipboardService.ReadPngFromClipboard(
            maximumBytes: 1024,
            pngFormat: 1,
            imagePngFormat: 2,
            hwndOwner: IntPtr.Zero,
            isClipboardFormatAvailable: _ => true,
            openClipboard: _ => false,
            getClipboardData: _ => IntPtr.Zero,
            globalSize: _ => UIntPtr.Zero,
            globalLock: _ => IntPtr.Zero,
            globalUnlock: _ => true,
            closeClipboard: () =>
            {
                closeCount++;
                return true;
            }));

        Assert.Equal(0, closeCount);
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
