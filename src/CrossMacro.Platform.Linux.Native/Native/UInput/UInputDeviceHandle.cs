namespace CrossMacro.Platform.Linux.Native.UInput;

/// <summary>Owns the descriptor and destroys only a successfully created kernel device.</summary>
internal sealed class UInputDeviceHandle : SafeHandle
{
    private readonly Action<int> _destroy;
    private readonly Action<int> _close;
    private bool _created;

    internal UInputDeviceHandle(int descriptor)
        : this(descriptor, static fd => { _ = UInputNative.ioctl(fd, UInputNative.UI_DEV_DESTROY, 0); },
            static fd => { _ = UInputNative.close(fd); }) { }

    internal UInputDeviceHandle(int descriptor, Action<int> destroy, Action<int> close)
        : base(new IntPtr(-1), ownsHandle: true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(descriptor);
        _destroy = destroy ?? throw new ArgumentNullException(nameof(destroy));
        _close = close ?? throw new ArgumentNullException(nameof(close));
        SetHandle(new IntPtr(descriptor));
    }

    public override bool IsInvalid => handle.ToInt64() < 0;

    internal int Descriptor => IsClosed ? -1 : handle.ToInt32();

    internal void MarkCreated() => _created = true;

    protected override bool ReleaseHandle()
    {
        var succeeded = true;
        try
        {
            if (_created)
            {
                _destroy(handle.ToInt32());
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            succeeded = false;
        }
        try { _close(handle.ToInt32()); }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            succeeded = false;
        }
        return succeeded;
    }
}
