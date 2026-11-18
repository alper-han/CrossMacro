namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandCoreClipboardKeyboard(
    WaylandLibrary library,
    IntPtr proxy,
    IntPtr surface,
    uint seatVersion) : WaylandProxyListener
{
    private readonly WaylandLibrary _library = library;
    private readonly IntPtr _proxy = proxy;
    private readonly uint _seatVersion = seatVersion;
    private readonly IntPtr _surface = surface;

    public bool HasFocus { get; private set; }
    public uint FocusSerial { get; private set; }

    protected override void OnEvent(uint opcode, IntPtr args)
    {
        var argumentSize = Marshal.SizeOf<WlArgument>();
        switch (opcode)
        {
            case 0:
                var fileDescriptor = Marshal.PtrToStructure<WlArgument>(args + argumentSize).h;
                LinuxFileDescriptorNative.Close(fileDescriptor);
                break;
            case 1:
                var serial = Marshal.PtrToStructure<WlArgument>(args).u;
                var enteredSurface = Marshal.PtrToStructure<WlArgument>(args + argumentSize).o;
                if (enteredSurface == _surface)
                {
                    FocusSerial = serial;
                    HasFocus = true;
                }

                break;
        }
    }

    protected override void OnDisposed()
    {
        if (_proxy != IntPtr.Zero)
        {
            if (_seatVersion >= 3)
            {
                _ = _library.MarshalRequest(_proxy, 0, args: null, version: 1, flags: 1);
            }
            else
            {
                _library.DestroyProxy(_proxy);
            }
        }
    }
}
