namespace CrossMacro.Platform.MacOS.Services;

internal interface IMacOSWindowNative
{
    public bool IsAvailable { get; }

    public MacOSCfSafeHandle CreateSystemWideElement();

    public MacOSCfSafeHandle CreateApplicationElement(int pid);

    public MacOSCfSafeHandle CopyAttribute(IntPtr element, string attribute);

    public IReadOnlyList<IntPtr> GetArrayValues(IntPtr array);

    public int? GetPid(IntPtr element);

    public string? GetStringAttribute(IntPtr element, string attribute);

    public bool? GetBooleanAttribute(IntPtr element, string attribute);

    public CoreGraphics.CGPoint? GetPointAttribute(IntPtr element, string attribute);

    public CoreGraphics.CGSize? GetSizeAttribute(IntPtr element, string attribute);

    public bool SetBooleanAttribute(IntPtr element, string attribute, bool value);

    public bool SetElementAttribute(IntPtr element, string attribute, IntPtr value);

    public bool SetPointAttribute(IntPtr element, string attribute, CoreGraphics.CGPoint point);

    public bool SetSizeAttribute(IntPtr element, string attribute, CoreGraphics.CGSize size);

    public bool PerformAction(IntPtr element, string action);

    public bool ElementsEqual(IntPtr left, IntPtr right);

    public void SetMessagingTimeout(IntPtr element, float timeoutSeconds);

    public IReadOnlyCollection<int> GetOnScreenApplicationPids();

    public uint? GetWindowId(int pid, string title, ScreenRect frame);

    public bool IsFrameOnScreen(ScreenRect frame);

    public ScreenRect? GetContainingDisplayBounds(ScreenRect frame);

    public IntPtr Retain(IntPtr element);

    public bool Release(IntPtr element);
}
