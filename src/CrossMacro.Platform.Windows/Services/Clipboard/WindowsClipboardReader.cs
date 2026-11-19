namespace CrossMacro.Platform.Windows.Services.Clipboard;

internal sealed class WindowsClipboardReader(IWindowsClipboardNative native)
{
    private readonly IWindowsClipboardNative _native = native ?? throw new ArgumentNullException(nameof(native));

    /// <summary>Reads the first available PNG flavour, or null when the clipboard holds none.</summary>
    internal byte[]? ReadPng(int maximumBytes, uint pngFormat, uint imagePngFormat, IntPtr owner)
    {
        if (!_native.IsFormatAvailable(pngFormat) && !_native.IsFormatAvailable(imagePngFormat))
        {
            return null;
        }

        if (!_native.Open(owner))
        {
            throw new InvalidOperationException("Failed to open Windows clipboard.");
        }

        try
        {
            foreach (var format in (ReadOnlySpan<uint>)[pngFormat, imagePngFormat])
            {
                if (!_native.IsFormatAvailable(format))
                {
                    continue;
                }

                var clipboardData = _native.GetData(format);
                if (clipboardData == IntPtr.Zero)
                {
                    continue;
                }

                var byteCount = (ulong)_native.Size(clipboardData);
                if (byteCount > (ulong)maximumBytes)
                {
                    throw new InvalidDataException("Clipboard PNG exceeds the maximum allowed size.");
                }

                if (byteCount is 0)
                {
                    return [];
                }

                var source = _native.Lock(clipboardData);
                if (source == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    var pngBytes = new byte[(int)byteCount];
                    Marshal.Copy(source, pngBytes, 0, pngBytes.Length);
                    return pngBytes;
                }
                finally
                {
                    _ = _native.Unlock(clipboardData);
                }
            }

            return null;
        }
        finally
        {
            _ = _native.Close();
        }
    }
}
