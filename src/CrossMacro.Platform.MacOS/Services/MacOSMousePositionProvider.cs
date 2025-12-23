using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Native;

namespace CrossMacro.Platform.MacOS.Services;

public class MacOSMousePositionProvider : IMousePositionProvider
{
    public string ProviderName => "macOS CoreGraphics";
    public bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        var eventRef = CoreGraphics.CGEventCreate(IntPtr.Zero);
        var loc = CoreGraphics.CGEventGetLocation(eventRef);
        CoreFoundation.CFRelease(eventRef);
        return Task.FromResult<(int X, int Y)?>(((int)loc.X, (int)loc.Y));
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
