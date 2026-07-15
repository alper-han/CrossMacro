using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Windows.Native;

namespace CrossMacro.Platform.Windows.Services;

[SupportedOSPlatform("windows")]
internal sealed class WindowsCliClipboardService : IClipboardService
{
    private readonly StaMessageThread _staThread;

    public WindowsCliClipboardService(StaMessageThread staThread)
    {
        _staThread = staThread;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return ClearAsync();
        }

        return _staThread.InvokeAsync(() =>
        {
            text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");

            if (!User32.OpenClipboard(_staThread.MessageWindowHandle))
            {
                throw new InvalidOperationException("Failed to open Windows clipboard.");
            }

            try
            {
                if (!User32.EmptyClipboard())
                {
                    throw new InvalidOperationException("Failed to empty Windows clipboard.");
                }

                int byteCount = (text.Length + 1) * 2;
                IntPtr hGlobal = Kernel32.GlobalAlloc(Kernel32.GHND, (UIntPtr)byteCount);

                if (hGlobal == IntPtr.Zero)
                {
                    throw new OutOfMemoryException("Failed to allocate global memory for clipboard data.");
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
                        Kernel32.GlobalUnlock(hGlobal);
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
                        Kernel32.GlobalFree(hGlobal);
                    }
                }
            }
            finally
            {
                User32.CloseClipboard();
            }
        });
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
                    IntPtr hGlobal = User32.GetClipboardData(User32.CF_UNICODETEXT);
                    if (hGlobal != IntPtr.Zero)
                    {
                        IntPtr source = Kernel32.GlobalLock(hGlobal);
                        if (source != IntPtr.Zero)
                        {
                            try
                            {
                                return Marshal.PtrToStringUni(source);
                            }
                            finally
                            {
                                Kernel32.GlobalUnlock(hGlobal);
                            }
                        }
                    }
                }
                else if (User32.IsClipboardFormatAvailable(User32.CF_TEXT))
                {
                    IntPtr hGlobal = User32.GetClipboardData(User32.CF_TEXT);
                    if (hGlobal != IntPtr.Zero)
                    {
                        IntPtr source = Kernel32.GlobalLock(hGlobal);
                        if (source != IntPtr.Zero)
                        {
                            try
                            {
                                return Marshal.PtrToStringAnsi(source);
                            }
                            finally
                            {
                                Kernel32.GlobalUnlock(hGlobal);
                            }
                        }
                    }
                }

                return null;
            }
            finally
            {
                User32.CloseClipboard();
            }
        });
    }

    private Task ClearAsync()
    {
        return _staThread.InvokeAsync(() =>
        {
            if (User32.OpenClipboard(_staThread.MessageWindowHandle))
            {
                try
                {
                    User32.EmptyClipboard();
                }
                finally
                {
                    User32.CloseClipboard();
                    Thread.Sleep(100);
                }
            }
        });
    }
}