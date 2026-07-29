
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class WaylandShmBuffer : IDisposable
{
    private const uint MemfdCloexec = 0x0001;
    private const int ProtRead = 0x1;
    private const int ProtWrite = 0x2;
    private const int MapShared = 0x01;
    private static readonly IntPtr MapFailed = new(-1);
    private int _disposed;

    private WaylandShmBuffer(int fd, IntPtr address, int size)
    {
        Fd = fd;
        Address = address;
        Size = size;
    }

    public int Fd { get; }
    public IntPtr Address { get; }
    public int Size { get; }

    public static WaylandShmBuffer Create(int size)
    {
        var fd = PortalPipeWireLibc.memfd_create("crossmacro-wayland-wlr", MemfdCloexec);
        if (fd < 0)
        {
            throw new InvalidOperationException($"memfd_create failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        if (PortalPipeWireLibc.ftruncate(fd, size) is not 0)
        {
            _ = PortalPipeWireLibc.close(fd);
            throw new InvalidOperationException($"ftruncate failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        var address = PortalPipeWireLibc.mmap(IntPtr.Zero, (UIntPtr)size, ProtRead | ProtWrite, MapShared, fd, IntPtr.Zero);
        if (address == MapFailed)
        {
            _ = PortalPipeWireLibc.close(fd);
            throw new InvalidOperationException($"mmap failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        return new WaylandShmBuffer(fd, address, size);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        if (Address != IntPtr.Zero)
        {
            _ = PortalPipeWireLibc.munmap(Address, (UIntPtr)Size);
        }

        if (Fd >= 0)
        {
            _ = PortalPipeWireLibc.close(Fd);
        }
    }
}
