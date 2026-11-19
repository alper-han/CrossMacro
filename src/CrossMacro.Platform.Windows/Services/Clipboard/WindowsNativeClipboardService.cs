using CrossMacro.Core.Services.Clipboard;

namespace CrossMacro.Platform.Windows.Services.Clipboard;

[SupportedOSPlatform("windows")]
internal sealed class WindowsNativeClipboardService(Lazy<StaMessageThread> staThread) :
    IClipboardService,
    IClipboardWriteReadbackCapability
{
    private readonly Lazy<StaMessageThread> _staThread = staThread;

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool GuaranteesImmediateReadback => true;

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(text))
        {
            return ClearAsync(cancellationToken);
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        var thread = _staThread.Value;
        return thread.InvokeAsync(() => SetTextInternal(normalized, thread.MessageWindowHandle), cancellationToken);
    }

    private static void SetTextInternal(string text, IntPtr hwndOwner)
        => new WindowsClipboardWriter(WindowsClipboardNative.Instance).WriteText(text, hwndOwner);

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var thread = _staThread.Value;
        return thread.InvokeAsync(() =>
        {
            // Windows synthesizes CF_UNICODETEXT from CF_TEXT and CF_OEMTEXT, so no ANSI path is needed.
            if (!User32.IsClipboardFormatAvailable(User32.CF_UNICODETEXT) || !User32.OpenClipboard(thread.MessageWindowHandle))
            {
                return null;
            }

            try
            {
                return GetUnicodeTextFromClipboard();
            }
            finally
            {
                _ = User32.CloseClipboard();
            }
        }, cancellationToken);
    }

    private static string? GetUnicodeTextFromClipboard()
    {
        IntPtr hGlobal = User32.GetClipboardData(User32.CF_UNICODETEXT);
        if (hGlobal == IntPtr.Zero)
        {
            return null;
        }

        IntPtr source = Kernel32.GlobalLock(hGlobal);
        if (source == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(source);
        }
        finally
        {
            _ = Kernel32.GlobalUnlock(hGlobal);
        }
    }

    private Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var thread = _staThread.Value;
        return thread.InvokeAsync(
            () => new WindowsClipboardWriter(WindowsClipboardNative.Instance).Clear(thread.MessageWindowHandle),
            cancellationToken);
    }
}
