
namespace CrossMacro.Platform.Windows.Services;

[SupportedOSPlatform("windows")]
internal sealed partial class WindowsNativeImageClipboardService(Lazy<StaMessageThread> staThread) : IImageClipboardService
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

        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
    }

    private static void SetPngInternal(byte[] pngArray, uint pngFormat, uint imagePngFormat, IntPtr hwndOwner)
    {
        IntPtr hDib = CreateDibFromPng(pngArray);
        IntPtr hPng = AllocateAndCopy(pngArray);
        IntPtr hImagePng = AllocateAndCopy(pngArray);

        if (!User32.OpenClipboard(hwndOwner))
        {
            FreeUnowned(hDib, hPng, hImagePng, pngOwned: false, imagePngOwned: false, dibOwned: false);
            throw new InvalidOperationException("Failed to open Windows clipboard.");
        }

        try
        {
            if (!User32.EmptyClipboard())
            {
                throw new InvalidOperationException("Failed to empty Windows clipboard.");
            }

            SetPngClipboardData(hDib, hPng, hImagePng, pngFormat, imagePngFormat);
        }
        finally
        {
            _ = User32.CloseClipboard();
        }
    }

    private static IntPtr AllocateAndCopy(byte[] data)
    {
        IntPtr hGlobal = Kernel32.GlobalAlloc(Kernel32.GHND, (UIntPtr)data.Length);
        if (hGlobal == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr target = Kernel32.GlobalLock(hGlobal);
        if (target != IntPtr.Zero)
        {
            Marshal.Copy(data, 0, target, data.Length);
            _ = Kernel32.GlobalUnlock(hGlobal);
        }
        return hGlobal;
    }

    private static void SetPngClipboardData(IntPtr hDib, IntPtr hPng, IntPtr hImagePng, uint pngFormat, uint imagePngFormat)
    {
        bool pngOwned = false;
        bool imagePngOwned = false;
        bool dibOwned = false;

        try
        {
            if (hPng != IntPtr.Zero && User32.SetClipboardData(pngFormat, hPng) != IntPtr.Zero)
            {
                pngOwned = true;
            }

            if (hImagePng != IntPtr.Zero && User32.SetClipboardData(imagePngFormat, hImagePng) != IntPtr.Zero)
            {
                imagePngOwned = true;
            }

            if (hDib != IntPtr.Zero && User32.SetClipboardData(User32.CF_DIB, hDib) != IntPtr.Zero)
            {
                dibOwned = true;
            }
        }
        finally
        {
            FreeUnowned(hDib, hPng, hImagePng, pngOwned, imagePngOwned, dibOwned);
        }
    }

    private static void FreeUnowned(IntPtr hDib, IntPtr hPng, IntPtr hImagePng, bool pngOwned, bool imagePngOwned, bool dibOwned)
    {
        if (hPng != IntPtr.Zero && !pngOwned)
        {
            _ = Kernel32.GlobalFree(hPng);
        }

        if (hImagePng != IntPtr.Zero && !imagePngOwned)
        {
            _ = Kernel32.GlobalFree(hImagePng);
        }

        if (hDib != IntPtr.Zero && !dibOwned)
        {
            _ = Kernel32.GlobalFree(hDib);
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
        uint absStride = (uint)Math.Abs(sourceStride);
        uint bufferSize = height * absStride;
        const uint headerSize = 40;

        IntPtr hGlobal = Kernel32.GlobalAlloc(Kernel32.GHND, (UIntPtr)(headerSize + bufferSize));
        if (hGlobal == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        IntPtr target = Kernel32.GlobalLock(hGlobal);
        if (target == IntPtr.Zero)
        {
            _ = Kernel32.GlobalFree(hGlobal);
            return IntPtr.Zero;
        }

        try
        {
            WriteDIBHeader(target, headerSize, width, height, bufferSize);

            IntPtr pixelTarget = IntPtr.Add(target, (int)headerSize);
            byte[] rowBuffer = new byte[absStride];
            for (int y = 0; y < height; y++)
            {
                IntPtr srcRow = IntPtr.Add(bmpData.Scan0, y * sourceStride);
                IntPtr dstRow = IntPtr.Add(pixelTarget, (int)(height - 1 - y) * (int)absStride);

                Marshal.Copy(srcRow, rowBuffer, 0, (int)absStride);
                Marshal.Copy(rowBuffer, 0, dstRow, (int)absStride);
            }
        }
        finally
        {
            _ = Kernel32.GlobalUnlock(hGlobal);
        }

        return hGlobal;
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
