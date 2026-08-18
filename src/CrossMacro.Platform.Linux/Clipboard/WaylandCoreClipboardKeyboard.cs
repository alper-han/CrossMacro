namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class WaylandCoreClipboardKeyboard : IDisposable
{
    private readonly WaylandLibrary _library;
    private readonly IntPtr _proxy;
    private readonly uint _seatVersion;
    private readonly IntPtr _surface;
    private GCHandle _dispatcherHandle;
    private bool _disposed;

    public WaylandCoreClipboardKeyboard(
        WaylandLibrary library,
        IntPtr proxy,
        IntPtr surface,
        uint seatVersion)
    {
        _library = library;
        _proxy = proxy;
        _surface = surface;
        _seatVersion = seatVersion;
        var dispatcher = (KeyboardDispatcher)Dispatch;
        _dispatcherHandle = GCHandle.Alloc(dispatcher, GCHandleType.Normal);
        DispatcherPtr = Marshal.GetFunctionPointerForDelegate(dispatcher);
    }

    public IntPtr DispatcherPtr { get; }
    public bool HasFocus { get; private set; }
    public uint FocusSerial { get; private set; }

    private delegate int KeyboardDispatcher(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args);

    private int Dispatch(IntPtr userData, IntPtr target, uint opcode, IntPtr message, IntPtr args)
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
                var surface = Marshal.PtrToStructure<WlArgument>(args + argumentSize).o;
                if (surface == _surface)
                {
                    FocusSerial = serial;
                    HasFocus = true;
                }

                break;
        }

        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_dispatcherHandle.IsAllocated)
        {
            _dispatcherHandle.Free();
        }

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
