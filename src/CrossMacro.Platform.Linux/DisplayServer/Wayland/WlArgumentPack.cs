
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

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
