namespace CrossMacro.Platform.Windows.Services.Clipboard;

/// <summary>Serial STA callers retain every allocation until its individual format is published.</summary>
internal sealed class WindowsClipboardWriter(IWindowsClipboardNative native)
{
    private readonly IWindowsClipboardNative _native = native ?? throw new ArgumentNullException(nameof(native));

    internal void WriteText(string text, IntPtr owner)
    {
        Write(owner, () =>
        {
            var byteCount = checked((nuint)(checked(text.Length + 1) * sizeof(char)));
            using var memory = WindowsClipboardMemory.TryAllocate(_native, byteCount, destination =>
            {
                for (var index = 0; index < text.Length; index++)
                {
                    Marshal.WriteInt16(destination, index * sizeof(char), (short)text[index]);
                }
                Marshal.WriteInt16(destination, text.Length * sizeof(char), 0);
            }) ?? throw new InvalidOperationException("Failed to allocate or lock global memory for clipboard data.");

            if (!memory.TryPublish(User32.CF_UNICODETEXT))
            {
                throw new InvalidOperationException("Failed to set clipboard data.");
            }
        });
    }

    internal void WritePng(byte[] bytes, uint pngFormat, uint imagePngFormat, IntPtr owner, Func<byte[], IntPtr> createDib)
    {
        using var dib = WindowsClipboardMemory.Adopt(_native, createDib(bytes));
        using var png = AllocatePng(bytes);
        using var imagePng = AllocatePng(bytes);
        Write(owner, () =>
        {
            // Formats remain independent: an unavailable DIB or one failed format
            // must not reclaim another format's successfully transferred memory.
            _ = png?.TryPublish(pngFormat);
            _ = imagePng?.TryPublish(imagePngFormat);
            _ = dib?.TryPublish(User32.CF_DIB);
        });
    }

    private WindowsClipboardMemory? AllocatePng(byte[] bytes)
        => WindowsClipboardMemory.TryAllocate(_native, (nuint)bytes.Length,
            destination => Marshal.Copy(bytes, 0, destination, bytes.Length));

    private void Write(IntPtr owner, Action publish)
    {
        if (!_native.Open(owner))
        {
            throw new InvalidOperationException("Failed to open Windows clipboard.");
        }

        try
        {
            if (!_native.Empty())
            {
                throw new InvalidOperationException("Failed to empty Windows clipboard.");
            }

            publish();
        }
        finally
        {
            _ = _native.Close();
        }
    }
}
