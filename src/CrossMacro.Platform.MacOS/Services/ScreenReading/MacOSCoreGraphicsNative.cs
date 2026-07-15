using System;
using System.Runtime.InteropServices;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services.ScreenReading;

internal sealed class MacOSCoreGraphicsNative : IMacOSCoreGraphicsNative
{
    public uint GetActiveDisplayCount()
    {
        var error = CoreGraphics.CGGetActiveDisplayList(0, activeDisplays: null, out var count);
        ThrowIfFailed(error, "CGGetActiveDisplayList count");
        return count;
    }

    public uint[] GetActiveDisplays(uint count)
    {
        var displays = new uint[count];
        var error = CoreGraphics.CGGetActiveDisplayList(count, displays, out var actualCount);
        ThrowIfFailed(error, "CGGetActiveDisplayList");
        return actualCount == count ? displays : displays[..checked((int)actualCount)];
    }

    public uint[] GetDisplaysWithRect(CoreGraphics.CGRect rect)
    {
        var countError = CoreGraphics.CGGetDisplaysWithRect(rect, 0, displays: null, out var count);
        ThrowIfFailed(countError, "CGGetDisplaysWithRect count");
        if (count == 0)
        {
            return [];
        }

        var displays = new uint[count];
        var error = CoreGraphics.CGGetDisplaysWithRect(rect, count, displays, out var actualCount);
        ThrowIfFailed(error, "CGGetDisplaysWithRect");
        return actualCount == count ? displays : displays[..checked((int)actualCount)];
    }

    public CoreGraphics.CGRect GetDisplayBounds(uint display) => CoreGraphics.CGDisplayBounds(display);

    public MacOSCapturedImage CreateImageForRect(uint display, CoreGraphics.CGRect rect)
    {
        var image = CoreGraphics.CGDisplayCreateImageForRect(display, rect);
        if (image == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CGDisplayCreateImageForRect failed for display {display}.");
        }

        try
        {
            return CopyImage(image);
        }
        finally
        {
            CoreGraphics.CGImageRelease(image);
        }
    }

    private static MacOSCapturedImage CopyImage(IntPtr image)
    {
        var provider = CoreGraphics.CGImageGetDataProvider(image);
        if (provider == IntPtr.Zero)
        {
            throw new InvalidOperationException("CGImageGetDataProvider returned null.");
        }

        var data = CoreGraphics.CGDataProviderCopyData(provider);
        if (data == IntPtr.Zero)
        {
            throw new InvalidOperationException("CGDataProviderCopyData returned null.");
        }

        try
        {
            var length = checked((int)CoreFoundation.CFDataGetLength(data));
            var bytes = new byte[length];
            if (length > 0)
            {
                var source = CoreFoundation.CFDataGetBytePtr(data);
                if (source == IntPtr.Zero)
                {
                    throw new InvalidOperationException("CFDataGetBytePtr returned null for non-empty image data.");
                }

                Marshal.Copy(source, bytes, 0, length);
            }

            return new MacOSCapturedImage(
                checked((int)CoreGraphics.CGImageGetWidth(image)),
                checked((int)CoreGraphics.CGImageGetHeight(image)),
                checked((int)CoreGraphics.CGImageGetBitsPerComponent(image)),
                checked((int)CoreGraphics.CGImageGetBitsPerPixel(image)),
                checked((int)CoreGraphics.CGImageGetBytesPerRow(image)),
                CoreGraphics.CGImageGetBitmapInfo(image),
                bytes);
        }
        finally
        {
            CoreFoundation.CFRelease(data);
        }
    }

    private static void ThrowIfFailed(CoreGraphics.CGError error, string operation)
    {
        if (error is not CoreGraphics.CGError.Success)
        {
            throw new BackendUnavailableException($"{operation} failed with CoreGraphics error {(int)error}.");
        }
    }
}
