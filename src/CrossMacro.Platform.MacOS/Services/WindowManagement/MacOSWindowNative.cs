namespace CrossMacro.Platform.MacOS.Services.WindowManagement;

internal sealed class MacOSWindowNative : IMacOSWindowNative
{
    private const int AxSuccess = 0;
    private const int AxErrorApiDisabled = -25211;
    private const int AxValueCGPointType = 1;
    private const int AxValueCGSizeType = 2;
    private const uint WindowListOptions = (uint)(
        CoreGraphics.CGWindowListOption.OnScreenOnly
        | CoreGraphics.CGWindowListOption.ExcludeDesktopElements);

    private static readonly IntPtr WindowOwnerPidKey = CfStringKeys.Get("kCGWindowOwnerPID");
    private static readonly IntPtr WindowLayerKey = CfStringKeys.Get("kCGWindowLayer");
    private static readonly IntPtr WindowBoundsKey = CfStringKeys.Get("kCGWindowBounds");
    private static readonly IntPtr WindowNameKey = CfStringKeys.Get("kCGWindowName");
    private static readonly IntPtr WindowNumberKey = CfStringKeys.Get("kCGWindowNumber");

    private readonly IMacOSCoreGraphicsNative _coreGraphics;
    private volatile bool _trusted;

    internal MacOSWindowNative()
        : this(new MacOSCoreGraphicsNative()) { }

    internal MacOSWindowNative(IMacOSCoreGraphicsNative coreGraphics)
    {
        _coreGraphics = coreGraphics ?? throw new ArgumentNullException(nameof(coreGraphics));
    }

    /// <summary>
    /// Accessibility trust is remembered once granted and dropped again as soon as an Accessibility call
    /// reports that the API is disabled, so revoking the permission is noticed without re-probing every call.
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsMacOS())
            {
                return false;
            }

            if (!_trusted)
            {
                _trusted = Accessibility.AXIsProcessTrusted();
            }

            return _trusted;
        }
    }

    public MacOSCfSafeHandle CreateSystemWideElement() =>
        new(Accessibility.AXUIElementCreateSystemWide());

    public MacOSCfSafeHandle CreateApplicationElement(int pid) =>
        new(Accessibility.AXUIElementCreateApplication(pid));

    public MacOSCfSafeHandle CopyAttribute(IntPtr element, string attribute)
    {
        var result = Accessibility.AXUIElementCopyAttributeValue(element, CfStringKeys.Get(attribute), out var value);
        return new MacOSCfSafeHandle(Succeeded(result) ? value : IntPtr.Zero);
    }

    public IReadOnlyList<IntPtr> GetArrayValues(IntPtr array)
    {
        if (array == IntPtr.Zero)
        {
            return [];
        }

        var count = CoreFoundation.CFArrayGetCount(array);
        var values = new List<IntPtr>(checked((int)count));
        for (var index = 0; index < count; index++)
        {
            var value = CoreFoundation.CFArrayGetValueAtIndex(array, index);
            if (value != IntPtr.Zero)
            {
                values.Add(value);
            }
        }

        return values;
    }

    public int? GetPid(IntPtr element) =>
        Accessibility.AXUIElementGetPid(element, out var pid) is AxSuccess && pid > 0 ? pid : null;

    public string? GetStringAttribute(IntPtr element, string attribute)
    {
        using var value = CopyAttribute(element, attribute);
        return value.IsInvalid ? null : ReadString(value.Value);
    }

    public bool? GetBooleanAttribute(IntPtr element, string attribute)
    {
        using var value = CopyAttribute(element, attribute);
        return value.IsInvalid ? null : CoreFoundation.CFBooleanGetValue(value.Value);
    }

    public CoreGraphics.CGPoint? GetPointAttribute(IntPtr element, string attribute)
    {
        using var value = CopyAttribute(element, attribute);
        return !value.IsInvalid
            && Accessibility.AXValueGetValue(
                value.Value,
                AxValueCGPointType,
                out CoreGraphics.CGPoint point)
            ? point
            : null;
    }

    public CoreGraphics.CGSize? GetSizeAttribute(IntPtr element, string attribute)
    {
        using var value = CopyAttribute(element, attribute);
        return !value.IsInvalid
            && Accessibility.AXValueGetValue(
                value.Value,
                AxValueCGSizeType,
                out CoreGraphics.CGSize size)
            ? size
            : null;
    }

    public bool SetBooleanAttribute(IntPtr element, string attribute, bool value) =>
        SetAttribute(element, attribute, value ? CoreFoundation.kCFBooleanTrue : CoreFoundation.kCFBooleanFalse);

    public bool SetElementAttribute(IntPtr element, string attribute, IntPtr value) =>
        value != IntPtr.Zero && SetAttribute(element, attribute, value);

    public bool SetPointAttribute(IntPtr element, string attribute, CoreGraphics.CGPoint point) =>
        SetAxValueAttribute(element, attribute, AxValueCGPointType, point);

    public bool SetSizeAttribute(IntPtr element, string attribute, CoreGraphics.CGSize size) =>
        SetAxValueAttribute(element, attribute, AxValueCGSizeType, size);

    public bool PerformAction(IntPtr element, string action) =>
        Succeeded(Accessibility.AXUIElementPerformAction(element, CfStringKeys.Get(action)));

    public bool ElementsEqual(IntPtr left, IntPtr right) =>
        left != IntPtr.Zero && right != IntPtr.Zero && CoreFoundation.CFEqual(left, right);

    public void SetMessagingTimeout(IntPtr element, float timeoutSeconds)
    {
        if (element != IntPtr.Zero)
        {
            _ = Accessibility.AXUIElementSetMessagingTimeout(element, timeoutSeconds);
        }
    }

    public IReadOnlyList<MacOSOnScreenWindow> GetOnScreenWindows()
    {
        var infoArray = CoreGraphics.CGWindowListCopyWindowInfo(WindowListOptions, 0);
        if (infoArray == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var count = CoreFoundation.CFArrayGetCount(infoArray);
            var windows = new List<MacOSOnScreenWindow>(checked((int)count));
            for (var index = 0; index < count; index++)
            {
                var dictionary = CoreFoundation.CFArrayGetValueAtIndex(infoArray, index);
                windows.Add(new MacOSOnScreenWindow(
                    GetDictionaryInt(dictionary, WindowOwnerPidKey),
                    GetDictionaryInt(dictionary, WindowLayerKey),
                    GetDictionaryInt(dictionary, WindowNumberKey),
                    GetDictionaryRect(dictionary, WindowBoundsKey),
                    GetDictionaryString(dictionary, WindowNameKey)));
            }

            return windows;
        }
        finally
        {
            CoreFoundation.CFRelease(infoArray);
        }
    }

    public bool IsFrameOnScreen(ScreenRect frame) =>
        _coreGraphics.GetDisplaysWithRect(MacOSScreenGeometry.ToCGRect(frame)).Length > 0;

    public ScreenRect? GetContainingDisplayBounds(ScreenRect frame)
    {
        var displays = _coreGraphics.GetDisplaysWithRect(MacOSScreenGeometry.ToCGRect(frame));
        ScreenRect? selected = null;
        long selectedArea = -1;
        foreach (var display in displays)
        {
            var bounds = MacOSScreenGeometry.ToScreenRect(_coreGraphics.GetDisplayBounds(display));
            var area = GetIntersectionArea(frame, bounds);
            if (area > selectedArea)
            {
                selected = bounds;
                selectedArea = area;
            }
        }

        return selected;
    }

    public IntPtr Retain(IntPtr element) => element == IntPtr.Zero ? IntPtr.Zero : CoreFoundation.CFRetain(element);

    public bool Release(IntPtr element)
    {
        if (element != IntPtr.Zero)
        {
            CoreFoundation.CFRelease(element);
        }

        return true;
    }

    private bool Succeeded(int axResult)
    {
        if (axResult is AxErrorApiDisabled)
        {
            _trusted = false;
        }

        return axResult is AxSuccess;
    }

    private bool SetAttribute(IntPtr element, string attribute, IntPtr value)
    {
        var key = CfStringKeys.Get(attribute);
        return Succeeded(Accessibility.AXUIElementIsAttributeSettable(element, key, out var settable))
            && settable
            && Succeeded(Accessibility.AXUIElementSetAttributeValue(element, key, value));
    }

    private bool SetAxValueAttribute<T>(IntPtr element, string attribute, int type, T value)
        where T : struct
    {
        var memory = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        try
        {
            Marshal.StructureToPtr(value, memory, fDeleteOld: false);
            var axValue = Accessibility.AXValueCreate(type, memory);
            if (axValue == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return SetAttribute(element, attribute, axValue);
            }
            finally
            {
                CoreFoundation.CFRelease(axValue);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static string? ReadString(IntPtr value)
    {
        if (CoreFoundation.CFGetTypeID(value) != CoreFoundation.CFStringGetTypeID())
        {
            return null;
        }

        var characterCount = CoreFoundation.CFStringGetLength(value);
        var maximumBytes = CoreFoundation.CFStringGetMaximumSizeForEncoding(
            characterCount,
            CoreFoundation.kCFStringEncodingUtf8);
        if (maximumBytes is < 0 or >= int.MaxValue)
        {
            throw new InvalidOperationException("Core Foundation reported an invalid UTF-8 string buffer size.");
        }

        var bufferLength = checked((int)maximumBytes + 1);
        var buffer = new byte[bufferLength];
        if (!CoreFoundation.CFStringGetCString(
                value,
                buffer,
                buffer.Length,
                CoreFoundation.kCFStringEncodingUtf8))
        {
            return null;
        }

        var terminator = Array.IndexOf(buffer, (byte)0);
        var length = terminator >= 0 ? terminator : buffer.Length;
        return Encoding.UTF8.GetString(buffer, 0, length);
    }

    private static IntPtr GetDictionaryValue(IntPtr dictionary, IntPtr key) =>
        dictionary == IntPtr.Zero ? IntPtr.Zero : CoreFoundation.CFDictionaryGetValue(dictionary, key);

    private static int? GetDictionaryInt(IntPtr dictionary, IntPtr key)
    {
        var value = GetDictionaryValue(dictionary, key);
        return value != IntPtr.Zero
            && CoreFoundation.CFNumberGetValue(value, CoreFoundation.kCFNumberSInt32Type, out var result)
                ? result
                : null;
    }

    private static ScreenRect? GetDictionaryRect(IntPtr dictionary, IntPtr key)
    {
        var value = GetDictionaryValue(dictionary, key);
        return value != IntPtr.Zero && CoreGraphics.CGRectMakeWithDictionaryRepresentation(value, out var rect)
            ? MacOSScreenGeometry.ToScreenRect(rect)
            : null;
    }

    private static string? GetDictionaryString(IntPtr dictionary, IntPtr key)
    {
        var value = GetDictionaryValue(dictionary, key);
        return value == IntPtr.Zero ? null : ReadString(value);
    }

    private static long GetIntersectionArea(ScreenRect left, ScreenRect right)
    {
        var width = Math.Max(0L, Math.Min((long)left.Right, right.Right) - Math.Max(left.X, right.X));
        var height = Math.Max(0L, Math.Min((long)left.Bottom, right.Bottom) - Math.Max(left.Y, right.Y));
        return width * height;
    }
}
