using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[StructLayout(LayoutKind.Sequential)]
internal struct WlMessage
{
    public IntPtr Name;
    public IntPtr Signature;
    public IntPtr Types;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WlInterface
{
    public IntPtr Name;
    public int Version;
    public int MethodCount;
    public IntPtr Methods;
    public int EventCount;
    public IntPtr Events;
}

[StructLayout(LayoutKind.Explicit)]
internal struct WlArgument
{
    [FieldOffset(0)] public int i;
    [FieldOffset(0)] public uint u;
    [FieldOffset(0)] public IntPtr s;
    [FieldOffset(0)] public IntPtr o;
    [FieldOffset(0)] public int h;
}

internal sealed class WlArgumentPack : IDisposable
{
    private readonly WlArgument[] _args;
    private readonly GCHandle _handle;

    public WlArgumentPack(int count)
    {
        _args = new WlArgument[count];
        _handle = GCHandle.Alloc(_args, GCHandleType.Pinned);
    }

    public IntPtr Address => _handle.AddrOfPinnedObject();

    public WlArgument this[int index]
    {
        get => _args[index];
        set => _args[index] = value;
    }

    public void Dispose()
    {
        if (_handle.IsAllocated)
        {
            _handle.Free();
        }
    }
}

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
