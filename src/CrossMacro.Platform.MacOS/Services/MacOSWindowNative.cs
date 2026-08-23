namespace CrossMacro.Platform.MacOS.Services;

internal sealed class MacOSWindowNative : IMacOSWindowNative
{
    private const int AxSuccess = 0;
    private const int AxValueCGPointType = 1;
    private const int AxValueCGSizeType = 2;
    private const uint WindowListOptions = (uint)(
        CoreGraphics.CGWindowListOption.OnScreenOnly
        | CoreGraphics.CGWindowListOption.ExcludeDesktopElements);
    private readonly IMacOSCoreGraphicsNative _coreGraphics;

    internal MacOSWindowNative()
        : this(new MacOSCoreGraphicsNative()) { }

    internal MacOSWindowNative(IMacOSCoreGraphicsNative coreGraphics)
    {
        _coreGraphics = coreGraphics ?? throw new ArgumentNullException(nameof(coreGraphics));
    }

    public bool IsAvailable => OperatingSystem.IsMacOS() && MacOSPermissionChecker.IsAccessibilityTrusted();

    public MacOSCfSafeHandle CreateSystemWideElement() =>
        new(Accessibility.AXUIElementCreateSystemWide());

    public MacOSCfSafeHandle CreateApplicationElement(int pid) =>
        new(Accessibility.AXUIElementCreateApplication(pid));

    public MacOSCfSafeHandle CopyAttribute(IntPtr element, string attribute)
    {
        return WithString(attribute, key =>
        {
            var result = Accessibility.AXUIElementCopyAttributeValue(element, key, out var value);
            return new MacOSCfSafeHandle(result is AxSuccess ? value : IntPtr.Zero);
        });
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
        WithString(action, value => Accessibility.AXUIElementPerformAction(element, value) is AxSuccess);

    public bool ElementsEqual(IntPtr left, IntPtr right) =>
        left != IntPtr.Zero && right != IntPtr.Zero && CoreFoundation.CFEqual(left, right);

    public void SetMessagingTimeout(IntPtr element, float timeoutSeconds)
    {
        if (element != IntPtr.Zero)
        {
            _ = Accessibility.AXUIElementSetMessagingTimeout(element, timeoutSeconds);
        }
    }

    public IReadOnlyCollection<int> GetOnScreenApplicationPids()
    {
        var infoArray = CoreGraphics.CGWindowListCopyWindowInfo(WindowListOptions, 0);
        if (infoArray == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var pids = new HashSet<int>();
            var count = CoreFoundation.CFArrayGetCount(infoArray);
            for (var index = 0; index < count; index++)
            {
                var dictionary = CoreFoundation.CFArrayGetValueAtIndex(infoArray, index);
                var pid = GetDictionaryInt(dictionary, "kCGWindowOwnerPID");
                if (pid is > 0)
                {
                    _ = pids.Add(pid.Value);
                }
            }

            return pids;
        }
        finally
        {
            CoreFoundation.CFRelease(infoArray);
        }
    }

    public uint? GetWindowId(int pid, string title, ScreenRect frame)
    {
        var infoArray = CoreGraphics.CGWindowListCopyWindowInfo(WindowListOptions, 0);
        if (infoArray == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            uint? match = null;
            var count = CoreFoundation.CFArrayGetCount(infoArray);
            for (var index = 0; index < count; index++)
            {
                var dictionary = CoreFoundation.CFArrayGetValueAtIndex(infoArray, index);
                if (GetDictionaryInt(dictionary, "kCGWindowOwnerPID") != pid
                    || GetDictionaryInt(dictionary, "kCGWindowLayer") is not 0
                    || GetDictionaryRect(dictionary, "kCGWindowBounds") != frame)
                {
                    continue;
                }

                var nativeTitle = GetDictionaryString(dictionary, "kCGWindowName");
                if (!string.IsNullOrEmpty(nativeTitle)
                    && !string.Equals(nativeTitle, title, StringComparison.Ordinal))
                {
                    continue;
                }

                var id = GetDictionaryInt(dictionary, "kCGWindowNumber");
                if (id is not > 0 || match is not null)
                {
                    return null;
                }

                match = checked((uint)id.Value);
            }

            return match;
        }
        finally
        {
            CoreFoundation.CFRelease(infoArray);
        }
    }

    public bool IsFrameOnScreen(ScreenRect frame) =>
        _coreGraphics.GetDisplaysWithRect(ToCGRect(frame)).Length > 0;

    public ScreenRect? GetContainingDisplayBounds(ScreenRect frame)
    {
        var displays = _coreGraphics.GetDisplaysWithRect(ToCGRect(frame));
        ScreenRect? selected = null;
        long selectedArea = -1;
        foreach (var display in displays)
        {
            var bounds = ToScreenRect(_coreGraphics.GetDisplayBounds(display));
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

    private static bool SetAttribute(IntPtr element, string attribute, IntPtr value)
    {
        return WithString(attribute, key =>
            Accessibility.AXUIElementIsAttributeSettable(element, key, out var settable) is AxSuccess
            && settable
            && Accessibility.AXUIElementSetAttributeValue(element, key, value) is AxSuccess);
    }

    private static bool SetAxValueAttribute<T>(IntPtr element, string attribute, int type, T value)
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

    private static int? GetDictionaryInt(IntPtr dictionary, string key)
    {
        return WithString<int?>(key, cfKey =>
        {
            var value = dictionary == IntPtr.Zero
                ? IntPtr.Zero
                : CoreFoundation.CFDictionaryGetValue(dictionary, cfKey);
            return value != IntPtr.Zero
                && CoreFoundation.CFNumberGetValue(
                    value,
                    CoreFoundation.kCFNumberSInt32Type,
                    out var result)
                ? result
                : null;
        });
    }

    private static ScreenRect? GetDictionaryRect(IntPtr dictionary, string key)
    {
        return WithString<ScreenRect?>(key, cfKey =>
        {
            var value = dictionary == IntPtr.Zero
                ? IntPtr.Zero
                : CoreFoundation.CFDictionaryGetValue(dictionary, cfKey);
            return value != IntPtr.Zero
                && CoreGraphics.CGRectMakeWithDictionaryRepresentation(value, out var rect)
                ? ToScreenRect(rect)
                : null;
        });
    }

    private static string? GetDictionaryString(IntPtr dictionary, string key)
    {
        return WithString<string?>(key, cfKey =>
        {
            var value = dictionary == IntPtr.Zero
                ? IntPtr.Zero
                : CoreFoundation.CFDictionaryGetValue(dictionary, cfKey);
            return value == IntPtr.Zero ? null : ReadString(value);
        });
    }

    private static T WithString<T>(string text, Func<IntPtr, T> callback)
    {
        var value = CoreFoundation.CFStringCreateWithCString(
            IntPtr.Zero,
            text,
            CoreFoundation.kCFStringEncodingUtf8);
        if (value == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to allocate a Core Foundation string.");
        }

        try
        {
            return callback(value);
        }
        finally
        {
            CoreFoundation.CFRelease(value);
        }
    }

    private static CoreGraphics.CGRect ToCGRect(ScreenRect rect) => new()
    {
        origin = new CoreGraphics.CGPoint { X = rect.X, Y = rect.Y },
        size = new CoreGraphics.CGSize { width = rect.Width, height = rect.Height },
    };

    private static ScreenRect ToScreenRect(CoreGraphics.CGRect rect) => new(
        checked((int)Math.Floor(rect.origin.X)),
        checked((int)Math.Floor(rect.origin.Y)),
        checked((int)Math.Ceiling(rect.size.width)),
        checked((int)Math.Ceiling(rect.size.height)));

    private static long GetIntersectionArea(ScreenRect left, ScreenRect right)
    {
        var width = Math.Max(0L, Math.Min((long)left.Right, right.Right) - Math.Max(left.X, right.X));
        var height = Math.Max(0L, Math.Min((long)left.Bottom, right.Bottom) - Math.Max(left.Y, right.Y));
        return width * height;
    }
}
