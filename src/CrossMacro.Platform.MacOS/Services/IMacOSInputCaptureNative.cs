namespace CrossMacro.Platform.MacOS.Services;

internal interface IMacOSInputCaptureNative
{
    public IntPtr GetCurrentRunLoop();

    public IntPtr CreateEventTap(
        CoreGraphics.CGEventTapLocation location,
        CoreGraphics.CGEventTapPlacement placement,
        CoreGraphics.CGEventTapOptions options,
        ulong eventsOfInterest,
        IntPtr callback);

    public IntPtr CreateRunLoopSource(IntPtr eventTap);

    public void AddRunLoopSource(IntPtr runLoop, IntPtr source);

    public void EnableEventTap(IntPtr eventTap, bool enable);

    public void RunLoopOnce(double seconds);

    public void StopRunLoop(IntPtr runLoop);

    public void Release(IntPtr handle);
}
