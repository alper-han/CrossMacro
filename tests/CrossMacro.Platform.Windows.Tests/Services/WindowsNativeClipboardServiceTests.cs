namespace CrossMacro.Platform.Windows.Tests.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsNativeClipboardServiceTests
{
    [WindowsFact]
    public async Task SetTextAsync_WhenClipboardIsLocked_ReportsFailure()
    {
        using var thread = new StaMessageThread("CrossMacro_TestNativeClipboard");
        var service = new WindowsNativeClipboardService(new Lazy<StaMessageThread>(() => thread));

        Assert.True(User32.OpenClipboard(IntPtr.Zero));
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.SetTextAsync(string.Empty, CancellationToken.None));
            Assert.NotNull(exception);
        }
        finally
        {
            Assert.True(User32.CloseClipboard());
        }
    }
}
