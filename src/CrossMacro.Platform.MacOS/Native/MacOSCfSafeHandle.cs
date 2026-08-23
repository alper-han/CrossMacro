namespace CrossMacro.Platform.MacOS.Native;

internal sealed class MacOSCfSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly Func<IntPtr, bool> _release;

    internal MacOSCfSafeHandle(IntPtr handle, Func<IntPtr, bool>? release = null)
        : base(ownsHandle: true)
    {
        _release = release ?? ReleaseCoreFoundationHandle;
        SetHandle(handle);
    }

    internal IntPtr Value => handle;

    protected override bool ReleaseHandle() => _release(handle);

    private static bool ReleaseCoreFoundationHandle(IntPtr value)
    {
        CoreFoundation.CFRelease(value);
        return true;
    }
}
