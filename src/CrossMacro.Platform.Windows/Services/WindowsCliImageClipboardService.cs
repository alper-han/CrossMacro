using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Windows.Native;

namespace CrossMacro.Platform.Windows.Services;

[SupportedOSPlatform("windows")]
internal sealed class WindowsCliImageClipboardService : IImageClipboardService
{
    private readonly StaMessageThread _staThread;
    private static uint? _pngFormatId;
    private static uint? _imagePngFormatId;

    public WindowsCliImageClipboardService(StaMessageThread staThread)
    {
        _staThread = staThread;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default)
    {
        if (pngBytes.IsEmpty)
        {
            return Task.CompletedTask;
        }

        byte[] pngArray = pngBytes.ToArray();

        return _staThread.InvokeAsync(() =>
        {
            _pngFormatId ??= User32.RegisterClipboardFormat("PNG");
            _imagePngFormatId ??= User32.RegisterClipboardFormat("image/png");

            if (_pngFormatId is 0 || _imagePngFormatId is 0)
            {
                throw new InvalidOperationException("Failed to register PNG clipboard formats.");
            }

            IntPtr hDib = CreateDibFromPng(pngArray);

            IntPtr hPng = Kernel32.GlobalAlloc(Kernel32.GHND, (UIntPtr)pngArray.Length);
            IntPtr hImagePng = Kernel32.GlobalAlloc(Kernel32.GHND, (UIntPtr)pngArray.Length);

            if (hPng != IntPtr.Zero)
            {
                IntPtr pngTarget = Kernel32.GlobalLock(hPng);
                if (pngTarget != IntPtr.Zero)
                {
                    Marshal.Copy(pngArray, 0, pngTarget, pngArray.Length);
                    Kernel32.GlobalUnlock(hPng);
                }
            }

            if (hImagePng != IntPtr.Zero)
            {
                IntPtr imagePngTarget = Kernel32.GlobalLock(hImagePng);
                if (imagePngTarget != IntPtr.Zero)
                {
                    Marshal.Copy(pngArray, 0, imagePngTarget, pngArray.Length);
                    Kernel32.GlobalUnlock(hImagePng);
                }
            }

            IntPtr hwndOwner = Kernel32.GetConsoleWindow();
            if (hwndOwner == IntPtr.Zero)
            {
                hwndOwner = _staThread.MessageWindowHandle;
            }

            if (!User32.OpenClipboard(hwndOwner))
            {
                if (hDib != IntPtr.Zero) Kernel32.GlobalFree(hDib);
                if (hPng != IntPtr.Zero) Kernel32.GlobalFree(hPng);
                if (hImagePng != IntPtr.Zero) Kernel32.GlobalFree(hImagePng);
                throw new InvalidOperationException("Failed to open Windows clipboard.");
            }

            try
            {
                if (!User32.EmptyClipboard())
                {
                    throw new InvalidOperationException("Failed to empty Windows clipboard.");
                }

                bool pngOwned = false;
                bool imagePngOwned = false;
                bool dibOwned = false;

                try
                {
                    if (hPng != IntPtr.Zero && User32.SetClipboardData(_pngFormatId.Value, hPng) != IntPtr.Zero)
                    {
                        pngOwned = true;
                    }

                    if (hImagePng != IntPtr.Zero && User32.SetClipboardData(_imagePngFormatId.Value, hImagePng) != IntPtr.Zero)
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
                    if (hPng != IntPtr.Zero && !pngOwned) Kernel32.GlobalFree(hPng);
                    if (hImagePng != IntPtr.Zero && !imagePngOwned) Kernel32.GlobalFree(hImagePng);
                    if (hDib != IntPtr.Zero && !dibOwned) Kernel32.GlobalFree(hDib);
                }
            }
            finally
            {
                User32.CloseClipboard();
                Thread.Sleep(500);
            }
        });
    }

    private IntPtr CreateDibFromPng(byte[] pngBytes)
    {
        IntPtr hGlobal = IntPtr.Zero;
        IntPtr pStream = IntPtr.Zero;
        IntPtr token = IntPtr.Zero;
        IntPtr pBitmap = IntPtr.Zero;

        try
        {
            var input = new GdiplusStartupInput { GdiplusVersion = 1 };
            int status = GdiplusStartup(out token, ref input, IntPtr.Zero);
            if (status is not 0) return IntPtr.Zero;

            pStream = Shlwapi.SHCreateMemStream(pngBytes, (uint)pngBytes.Length);
            if (pStream == IntPtr.Zero) return IntPtr.Zero;

            status = GdipCreateBitmapFromStream(pStream, out pBitmap);
            if (status is not 0) return IntPtr.Zero;

            status = GdipGetImageWidth(pBitmap, out uint width);
            status = GdipGetImageHeight(pBitmap, out uint height);

            const int format32bppArgb = 0x26200A;

            GdiRect rect = new GdiRect { X = 0, Y = 0, Width = (int)width, Height = (int)height };
            BitmapData bmpData = new BitmapData();

            status = GdipBitmapLockBits(pBitmap, ref rect, 1, format32bppArgb, ref bmpData);
            if (status is not 0) return IntPtr.Zero;

            try
            {
                int sourceStride = bmpData.Stride;
                uint absStride = (uint)Math.Abs(sourceStride);
                uint bufferSize = height * absStride;
                const uint headerSize = 40;

                hGlobal = Kernel32.GlobalAlloc(Kernel32.GHND, (UIntPtr)(headerSize + bufferSize));
                if (hGlobal == IntPtr.Zero) return IntPtr.Zero;

                IntPtr target = Kernel32.GlobalLock(hGlobal);
                if (target == IntPtr.Zero)
                {
                    Kernel32.GlobalFree(hGlobal);
                    return IntPtr.Zero;
                }

                try
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
                    Kernel32.GlobalUnlock(hGlobal);
                }
            }
            finally
            {
                GdipBitmapUnlockBits(pBitmap, ref bmpData);
            }

            return hGlobal;
        }
        catch
        {
            if (hGlobal != IntPtr.Zero) Kernel32.GlobalFree(hGlobal);
            return IntPtr.Zero;
        }
        finally
        {
            if (pBitmap != IntPtr.Zero) GdipDisposeImage(pBitmap);
            if (pStream != IntPtr.Zero) Marshal.Release(pStream);
            if (token != IntPtr.Zero) GdiplusShutdown(token);
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

    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdiplusStartup(out IntPtr token, ref GdiplusStartupInput input, IntPtr output);

    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern void GdiplusShutdown(IntPtr token);

    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipCreateBitmapFromStream(IntPtr stream, out IntPtr bitmap);

    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipGetImageWidth(IntPtr image, out uint width);

    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipGetImageHeight(IntPtr image, out uint height);

    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipBitmapLockBits(IntPtr bitmap, ref GdiRect rect, uint flags, int format, ref BitmapData lockedBitmapData);

    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipBitmapUnlockBits(IntPtr bitmap, ref BitmapData lockedBitmapData);

    [DllImport("gdiplus.dll", ExactSpelling = true)]
    private static extern int GdipDisposeImage(IntPtr image);
}
