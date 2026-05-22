using System;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services;

public class MacOSMousePositionProvider : IMousePositionProvider
{
    public string ProviderName => "macOS CoreGraphics";
    public bool IsSupported => OperatingSystem.IsMacOS();

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        var eventRef = CoreGraphics.CGEventCreate(IntPtr.Zero);
        if (eventRef == IntPtr.Zero)
        {
            return Task.FromResult<(int X, int Y)?>(null);
        }

        try
        {
            return Task.FromResult(ReadPosition(eventRef));
        }
        finally
        {
            CoreFoundation.CFRelease(eventRef);
        }
    }

    internal static (int X, int Y)? ReadPosition(IntPtr eventRef)
    {
        if (eventRef == IntPtr.Zero)
        {
            return null;
        }

        var loc = CoreGraphics.CGEventGetLocation(eventRef);
        return ((int)loc.X, (int)loc.Y);
    }

    public Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        uint mainDisplay = CoreGraphics.CGMainDisplayID();
        var bounds = CoreGraphics.CGDisplayBounds(mainDisplay);
        return Task.FromResult<(int Width, int Height)?>((
            (int)bounds.size.width, 
            (int)bounds.size.height
        ));
    }

    public void Dispose()
    {
    }
}
