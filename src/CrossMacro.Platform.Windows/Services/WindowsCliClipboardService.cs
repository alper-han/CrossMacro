
namespace CrossMacro.Platform.Windows.Services;

[SupportedOSPlatform("windows")]
internal sealed class WindowsCliClipboardService(StaMessageThread staThread) : IClipboardService
{
    private readonly StaMessageThread _staThread = staThread;

    public bool IsSupported => OperatingSystem.IsWindows();

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return ClearAsync(cancellationToken);
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
        return _staThread.InvokeAsync(() => SetTextInternal(normalized, _staThread.MessageWindowHandle));
    }

    private static void SetTextInternal(string text, IntPtr hwndOwner)
    {
        if (!User32.OpenClipboard(hwndOwner))
        {
            throw new InvalidOperationException("Failed to open Windows clipboard.");
        }

        try
        {
            if (!User32.EmptyClipboard())
            {
                throw new InvalidOperationException("Failed to empty Windows clipboard.");
            }

            WriteUnicodeTextToClipboard(text);
        }
        finally
        {
            _ = User32.CloseClipboard();
        }
    }

    private static void WriteUnicodeTextToClipboard(string text)
    {
        int byteCount = (text.Length + 1) * 2;
        IntPtr hGlobal = Kernel32.GlobalAlloc(Kernel32.GHND, (UIntPtr)byteCount);

        if (hGlobal == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to allocate global memory for clipboard data.");
        }

        bool isOwnedBySystem = false;
        try
        {
            IntPtr target = Kernel32.GlobalLock(hGlobal);
            if (target == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to lock global memory for clipboard data.");
            }

            try
            {
                for (int i = 0; i < text.Length; i++)
                {
                    Marshal.WriteInt16(target, i * 2, (short)text[i]);
                }
            }
            finally
            {
                _ = Kernel32.GlobalUnlock(hGlobal);
            }

            IntPtr result = User32.SetClipboardData(User32.CF_UNICODETEXT, hGlobal);
            if (result == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to set clipboard data.");
            }

            isOwnedBySystem = true;
        }
        finally
        {
            if (!isOwnedBySystem)
            {
                _ = Kernel32.GlobalFree(hGlobal);
            }
        }
    }

    public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        return _staThread.InvokeAsync(() =>
        {
            if (!User32.IsClipboardFormatAvailable(User32.CF_UNICODETEXT) &&
                !User32.IsClipboardFormatAvailable(User32.CF_TEXT))
            {
                return null;
            }

            if (!User32.OpenClipboard(_staThread.MessageWindowHandle))
            {
                return null;
            }

            try
            {
                if (User32.IsClipboardFormatAvailable(User32.CF_UNICODETEXT))
                {
                    return GetUnicodeTextFromClipboard();
                }
                if (User32.IsClipboardFormatAvailable(User32.CF_TEXT))
                {
                    return GetAnsiTextFromClipboard();
                }

                return null;
            }
            finally
            {
                _ = User32.CloseClipboard();
            }
        });
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

    private static string? GetAnsiTextFromClipboard()
    {
        IntPtr hGlobal = User32.GetClipboardData(User32.CF_TEXT);
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
            return Marshal.PtrToStringAnsi(source);
        }
        finally
        {
            _ = Kernel32.GlobalUnlock(hGlobal);
        }
    }

    private async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _staThread.InvokeAsync(() =>
        {
            if (User32.OpenClipboard(_staThread.MessageWindowHandle))
            {
                try
                {
                    _ = User32.EmptyClipboard();
                }
                finally
                {
                    _ = User32.CloseClipboard();
                }
            }
        }).ConfigureAwait(false);

        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
    }
}
