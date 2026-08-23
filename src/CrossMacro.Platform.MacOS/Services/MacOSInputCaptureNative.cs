namespace CrossMacro.Platform.MacOS.Services;

internal sealed class MacOSInputCaptureNative : IMacOSInputCaptureNative
{
    public IntPtr GetCurrentRunLoop() => CoreFoundation.CFRunLoopGetCurrent();

    public IntPtr CreateEventTap(
        CoreGraphics.CGEventTapLocation location,
        CoreGraphics.CGEventTapPlacement placement,
        CoreGraphics.CGEventTapOptions options,
        ulong eventsOfInterest,
        IntPtr callback) =>
        CoreGraphics.CGEventTapCreate(
            location,
            placement,
            options,
            eventsOfInterest,
            callback,
            IntPtr.Zero);

    public IntPtr CreateRunLoopSource(IntPtr eventTap) =>
        CoreFoundation.CFMachPortCreateRunLoopSource(IntPtr.Zero, eventTap, IntPtr.Zero);

    public void AddRunLoopSource(IntPtr runLoop, IntPtr source) =>
        CoreFoundation.CFRunLoopAddSource(runLoop, source, CoreFoundation.kCFRunLoopCommonModes);

    public void EnableEventTap(IntPtr eventTap, bool enable) =>
        CoreGraphics.CGEventTapEnable(eventTap, enable);

    public void RunLoopOnce(double seconds) =>
        _ = CoreFoundation.CFRunLoopRunInMode(
            CoreFoundation.kCFRunLoopDefaultMode,
            seconds,
            returnAfterSourceHandled: false);

    public void StopRunLoop(IntPtr runLoop) => CoreFoundation.CFRunLoopStop(runLoop);

    public void Release(IntPtr handle) => CoreFoundation.CFRelease(handle);
}
