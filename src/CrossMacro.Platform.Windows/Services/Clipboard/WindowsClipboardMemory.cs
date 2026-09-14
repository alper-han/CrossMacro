namespace CrossMacro.Platform.Windows.Services.Clipboard;

/// <summary>Owns movable global memory until Windows accepts clipboard ownership.</summary>
internal sealed class WindowsClipboardMemory : SafeHandle
{
    private readonly IWindowsClipboardNative _native;

    private WindowsClipboardMemory(IWindowsClipboardNative native, IntPtr memory)
        : base(IntPtr.Zero, ownsHandle: true)
    {
        _native = native;
        SetHandle(memory);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal static WindowsClipboardMemory? Adopt(IWindowsClipboardNative native, IntPtr memory)
        => memory == IntPtr.Zero ? null : new WindowsClipboardMemory(native, memory);

    internal static WindowsClipboardMemory? TryAllocate(
        IWindowsClipboardNative native,
        nuint byteCount,
        Action<IntPtr> write)
    {
        ArgumentNullException.ThrowIfNull(native);
        ArgumentNullException.ThrowIfNull(write);
        var memory = Adopt(native, native.Allocate(byteCount));
        if (memory is null)
        {
            return null;
        }

        try
        {
            var destination = native.Lock(memory.handle);
            if (destination == IntPtr.Zero)
            {
                memory.Dispose();
                return null;
            }

            try
            {
                write(destination);
            }
            finally
            {
                _ = native.Unlock(memory.handle);
            }

            return memory;
        }
        catch
        {
            memory.Dispose();
            throw;
        }
    }

    internal bool TryPublish(uint format)
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        if (_native.SetData(format, handle) == IntPtr.Zero)
        {
            return false;
        }

        _ = Detach();
        return true;
    }

    internal IntPtr Detach()
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);
        var memory = handle;
        SetHandleAsInvalid();
        return memory;
    }

    protected override bool ReleaseHandle() => _native.Free(handle) == IntPtr.Zero;
}
