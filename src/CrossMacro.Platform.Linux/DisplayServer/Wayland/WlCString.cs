using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WlCString : IDisposable
{
    private readonly GCHandle _handle;

    public WlCString(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value + "\0");
        _handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
    }

    public IntPtr Address => _handle.AddrOfPinnedObject();

    public void Dispose() => _handle.Free();
}
