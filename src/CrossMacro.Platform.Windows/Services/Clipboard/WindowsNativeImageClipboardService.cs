
namespace CrossMacro.Platform.Windows.Services.Clipboard;

[SupportedOSPlatform("windows")]
internal sealed partial class WindowsNativeImageClipboardService(Lazy<StaMessageThread> staThread) : IImageClipboardService, IImageClipboardReader
{
    private readonly Lazy<StaMessageThread> _staThread = staThread;
    private static readonly Lazy<uint> _pngFormatId = new(() => User32.RegisterClipboardFormat("PNG"));
    private static readonly Lazy<uint> _imagePngFormatId = new(() => User32.RegisterClipboardFormat("image/png"));

    public bool IsSupported => OperatingSystem.IsWindows();

    public async Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (pngBytes.IsEmpty)
        {
            return;
        }

        byte[] pngArray = pngBytes.ToArray();

        var thread = _staThread.Value;
        await thread.InvokeAsync(() =>
        {
            uint pngFormat = _pngFormatId.Value;
            uint imagePngFormat = _imagePngFormatId.Value;

            if (pngFormat is 0 || imagePngFormat is 0)
            {
                throw new InvalidOperationException("Failed to register PNG clipboard formats.");
            }

            IntPtr hwndOwner = Kernel32.GetConsoleWindow();
            if (hwndOwner == IntPtr.Zero)
            {
                hwndOwner = thread.MessageWindowHandle;
            }

            SetPngInternal(pngArray, pngFormat, imagePngFormat, hwndOwner);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]?> GetPngAsync(int maximumBytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes), "Maximum PNG bytes must be positive.");
        }

        var thread = _staThread.Value;
        return await thread.InvokeAsync(() =>
        {
            uint pngFormat = _pngFormatId.Value;
            uint imagePngFormat = _imagePngFormatId.Value;
            if (pngFormat is 0 || imagePngFormat is 0)
            {
                throw new ImageClipboardUnavailableException("Failed to register PNG clipboard formats.");
            }

            IntPtr hwndOwner = Kernel32.GetConsoleWindow();
            if (hwndOwner == IntPtr.Zero)
            {
                hwndOwner = thread.MessageWindowHandle;
            }

            return GetPngInternal(maximumBytes, pngFormat, imagePngFormat, hwndOwner);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void SetPngInternal(byte[] pngArray, uint pngFormat, uint imagePngFormat, IntPtr hwndOwner)
        => new WindowsClipboardWriter(WindowsClipboardNative.Instance)
            .WritePng(pngArray, pngFormat, imagePngFormat, hwndOwner, CreateDibFromPng);

    private static byte[]? GetPngInternal(int maximumBytes, uint pngFormat, uint imagePngFormat, IntPtr hwndOwner)
    {
        return ReadPngFromClipboard(
            maximumBytes,
            pngFormat,
            imagePngFormat,
            hwndOwner,
            User32.IsClipboardFormatAvailable,
            User32.OpenClipboard,
            User32.GetClipboardData,
            Kernel32.GlobalSize,
            Kernel32.GlobalLock,
            Kernel32.GlobalUnlock,
            User32.CloseClipboard);
    }

    internal static byte[]? ReadPngFromClipboard(
        int maximumBytes,
        uint pngFormat,
        uint imagePngFormat,
        IntPtr hwndOwner,
        Func<uint, bool> isClipboardFormatAvailable,
        Func<IntPtr, bool> openClipboard,
        Func<uint, IntPtr> getClipboardData,
        Func<IntPtr, UIntPtr> globalSize,
        Func<IntPtr, IntPtr> globalLock,
        Func<IntPtr, bool> globalUnlock,
        Func<bool> closeClipboard)
    {
        if (!isClipboardFormatAvailable(pngFormat) && !isClipboardFormatAvailable(imagePngFormat))
        {
            return null;
        }

        if (!openClipboard(hwndOwner))
        {
            throw new InvalidOperationException("Failed to open Windows clipboard.");
        }

        try
        {
            foreach (var format in new[] { pngFormat, imagePngFormat })
            {
                if (!isClipboardFormatAvailable(format))
                {
                    continue;
                }

                var clipboardData = getClipboardData(format);
                if (clipboardData == IntPtr.Zero)
                {
                    continue;
                }

                var byteCount = globalSize(clipboardData).ToUInt64();
                if (byteCount > (ulong)maximumBytes)
                {
                    throw new InvalidDataException("Clipboard PNG exceeds the maximum allowed size.");
                }

                if (byteCount is 0)
                {
                    return [];
                }

                var source = globalLock(clipboardData);
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
                    _ = globalUnlock(clipboardData);
                }
            }

            return null;
        }
        finally
        {
            _ = closeClipboard();
        }
    }

    private static IntPtr CreateDibFromPng(byte[] pngBytes)
    {
        IntPtr hGlobal = IntPtr.Zero;
        IntPtr pStream = IntPtr.Zero;
        IntPtr token = IntPtr.Zero;
        IntPtr pBitmap = IntPtr.Zero;

        try
        {
            if (!TryInitializeGdiplusBitmap(pngBytes, out token, out pStream, out pBitmap, out uint width, out uint height))
            {
                return IntPtr.Zero;
            }

            const int format32bppArgb = 0x26200A;
            GdiRect rect = new GdiRect { X = 0, Y = 0, Width = (int)width, Height = (int)height };
            BitmapData bmpData = new BitmapData();

            int status = GdipBitmapLockBits(pBitmap, ref rect, 1, format32bppArgb, ref bmpData);
            if (status is not 0)
            {
                return IntPtr.Zero;
            }

            try
            {
                hGlobal = CopyBitmapToDIB(width, height, ref bmpData);
                return hGlobal;
            }
            finally
            {
                _ = GdipBitmapUnlockBits(pBitmap, ref bmpData);
            }
        }
        catch (ArgumentException)
        {
            if (hGlobal != IntPtr.Zero)
            {
                _ = Kernel32.GlobalFree(hGlobal);
            }

            return IntPtr.Zero;
        }
        catch (OutOfMemoryException)
        {
            if (hGlobal != IntPtr.Zero)
            {
                _ = Kernel32.GlobalFree(hGlobal);
            }

            return IntPtr.Zero;
        }
        finally
        {
            CleanupGdiplusResources(token, pStream, pBitmap);
        }
    }

    private static bool TryInitializeGdiplusBitmap(
        byte[] pngBytes,
        out IntPtr token,
        out IntPtr pStream,
        out IntPtr pBitmap,
        out uint width,
        out uint height)
    {
        token = IntPtr.Zero;
        pStream = IntPtr.Zero;
        pBitmap = IntPtr.Zero;
        width = 0;
        height = 0;

        var input = new GdiplusStartupInput { GdiplusVersion = 1 };
        int status = GdiplusStartup(out token, ref input, IntPtr.Zero);
        if (status is not 0)
        {
            return false;
        }

        pStream = Shlwapi.SHCreateMemStream(pngBytes, (uint)pngBytes.Length);
        if (pStream == IntPtr.Zero)
        {
            return false;
        }

        status = GdipCreateBitmapFromStream(pStream, out pBitmap);
        if (status is not 0)
        {
            return false;
        }

        if (GdipGetImageWidth(pBitmap, out width) is not 0 ||
            GdipGetImageHeight(pBitmap, out height) is not 0)
        {
            return false;
        }

        return true;
    }

    private static IntPtr CopyBitmapToDIB(uint width, uint height, ref BitmapData bmpData)
    {
        int sourceStride = bmpData.Stride;
        var sourcePixels = bmpData.Scan0;
        uint absStride = checked((uint)Math.Abs(sourceStride));
        uint bufferSize = checked(height * absStride);
        const uint headerSize = 40;
        var byteCount = checked((nuint)(headerSize + bufferSize));
        using var memory = WindowsClipboardMemory.TryAllocate(WindowsClipboardNative.Instance, byteCount, target =>
        {
            WriteDIBHeader(target, headerSize, width, height, bufferSize);
            IntPtr pixelTarget = IntPtr.Add(target, (int)headerSize);
            byte[] rowBuffer = new byte[absStride];
            for (int y = 0; y < height; y++)
            {
                IntPtr srcRow = IntPtr.Add(sourcePixels, checked(y * sourceStride));
                IntPtr dstRow = IntPtr.Add(pixelTarget, checked((int)(height - 1 - y) * (int)absStride));
                Marshal.Copy(srcRow, rowBuffer, 0, (int)absStride);
                Marshal.Copy(rowBuffer, 0, dstRow, (int)absStride);
            }
        });

        return memory?.Detach() ?? IntPtr.Zero;
    }

    private static void WriteDIBHeader(IntPtr target, uint headerSize, uint width, uint height, uint bufferSize)
    {
        Marshal.WriteInt32(target, 0, (int)headerSize);
        Marshal.WriteInt32(target, 4, (int)width);
        Marshal.WriteInt32(target, 8, (int)height);
        Marshal.WriteInt16(target, 12, 1);
        Marshal.WriteInt16(target, 14, 32);
        Marshal.WriteInt32(target, 16, 0);
        Marshal.WriteInt32(target, 20, (int)bufferSize);
        Marshal.WriteInt32(target, 24, 0);
        Marshal.WriteInt32(target, 28, 0);
        Marshal.WriteInt32(target, 32, 0);
        Marshal.WriteInt32(target, 36, 0);
    }

    private static void CleanupGdiplusResources(IntPtr token, IntPtr pStream, IntPtr pBitmap)
    {
        if (pBitmap != IntPtr.Zero)
        {
            _ = GdipDisposeImage(pBitmap);
        }

        if (pStream != IntPtr.Zero)
        {
            _ = Marshal.Release(pStream);
        }

        if (token != IntPtr.Zero)
        {
            GdiplusShutdown(token);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GdiplusStartupInput
    {
        public uint GdiplusVersion;
        public IntPtr DebugEventCallback;
        public int SuppressBackgroundThread;
        public int SuppressExternalCodecs;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapData
    {
        public uint Width;
        public uint Height;
        public int Stride;
        public int PixelFormat;
        public IntPtr Scan0;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GdiRect
    {
        public int X, Y, Width, Height;
    }

    [LibraryImport("gdiplus.dll")]
    private static partial int GdiplusStartup(out IntPtr token, ref GdiplusStartupInput input, IntPtr output);

    [LibraryImport("gdiplus.dll")]
    private static partial void GdiplusShutdown(IntPtr token);

    [LibraryImport("gdiplus.dll")]
    private static partial int GdipCreateBitmapFromStream(IntPtr stream, out IntPtr bitmap);

    [LibraryImport("gdiplus.dll")]
    private static partial int GdipGetImageWidth(IntPtr image, out uint width);

    [LibraryImport("gdiplus.dll")]
    private static partial int GdipGetImageHeight(IntPtr image, out uint height);

    [LibraryImport("gdiplus.dll")]
    private static partial int GdipBitmapLockBits(IntPtr bitmap, ref GdiRect rect, uint flags, int format, ref BitmapData lockedBitmapData);

    [LibraryImport("gdiplus.dll")]
    private static partial int GdipBitmapUnlockBits(IntPtr bitmap, ref BitmapData lockedBitmapData);

    [LibraryImport("gdiplus.dll")]
    private static partial int GdipDisposeImage(IntPtr image);
}
