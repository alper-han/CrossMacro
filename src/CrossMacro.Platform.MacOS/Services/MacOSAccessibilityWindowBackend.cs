namespace CrossMacro.Platform.MacOS.Services;

internal sealed class MacOSAccessibilityWindowBackend : IMacOSWindowBackend
{
    private const float MessagingTimeoutSeconds = 2.0f;
    private readonly IMacOSWindowNative _native;
    private readonly MacOSWindowRegistry _registry;

    internal MacOSAccessibilityWindowBackend()
        : this(new MacOSWindowNative()) { }

    internal MacOSAccessibilityWindowBackend(IMacOSWindowNative native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
        _registry = new MacOSWindowRegistry(native.Retain, native.Release, native.ElementsEqual);
    }

    public bool IsAvailable => _native.IsAvailable;

    public WindowInfo? GetActiveWindow()
    {
        if (!IsAvailable)
        {
            return null;
        }

        using var systemWide = _native.CreateSystemWideElement();
        if (systemWide.IsInvalid)
        {
            return null;
        }

        _native.SetMessagingTimeout(systemWide.Value, MessagingTimeoutSeconds);
        using var application = _native.CopyAttribute(
            systemWide.Value,
            "AXFocusedApplication");
        if (application.IsInvalid || _native.GetPid(application.Value) is not { } pid)
        {
            return null;
        }

        _native.SetMessagingTimeout(application.Value, MessagingTimeoutSeconds);
        using var window = _native.CopyAttribute(application.Value, "AXFocusedWindow");
        return window.IsInvalid ? null : MapWindow(window.Value, pid, isFocused: true);
    }

    public IReadOnlyList<WindowInfo> GetWindows()
    {
        if (!IsAvailable)
        {
            return [];
        }

        var activeAddress = GetActiveWindow()?.Address;
        var windows = new List<WindowInfo>();
        var visibleAddresses = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pid in _native.GetOnScreenApplicationPids())
        {
            using var application = _native.CreateApplicationElement(pid);
            if (application.IsInvalid)
            {
                continue;
            }

            _native.SetMessagingTimeout(application.Value, MessagingTimeoutSeconds);
            using var axWindows = _native.CopyAttribute(application.Value, "AXWindows");
            if (axWindows.IsInvalid)
            {
                continue;
            }

            foreach (var window in _native.GetArrayValues(axWindows.Value))
            {
                var info = MapWindow(window, pid, isFocused: false);
                if (info is null || info.IsHidden || !_native.IsFrameOnScreen(ToFrame(info)))
                {
                    continue;
                }

                windows.Add(info with
                {
                    IsFocused = string.Equals(info.Address, activeAddress, StringComparison.Ordinal),
                });
                _ = visibleAddresses.Add(info.Address);
            }
        }

        _registry.PruneExcept(visibleAddresses);
        return windows;
    }

    public bool Focus(string address) => UseWindow(address, (window, pid) =>
    {
        using var application = _native.CreateApplicationElement(pid);
        if (application.IsInvalid)
        {
            return false;
        }

        var app = application.Value;
        _native.SetMessagingTimeout(app, MessagingTimeoutSeconds);
        _ = _native.SetBooleanAttribute(window, "AXMinimized", value: false);
        _ = _native.SetBooleanAttribute(app, "AXFrontmost", value: true);
        _ = _native.SetElementAttribute(app, "AXMainWindow", window);
        _ = _native.SetElementAttribute(app, "AXFocusedWindow", window);
        _ = _native.SetBooleanAttribute(window, "AXMain", value: true);
        _ = _native.SetBooleanAttribute(window, "AXFocused", value: true);
        _ = _native.PerformAction(window, "AXRaise");
        return VerifyFocused(app, window);
    });

    public bool Close(string address)
    {
        var closed = UseWindow(address, (window, _) => PressWindowButton(window, "AXCloseButton"));
        if (closed)
        {
            _registry.Remove(address);
        }

        return closed;
    }

    public bool SetPosition(string address, int x, int y) =>
        UseWindow(address, (window, _) => _native.SetPointAttribute(
            window,
            "AXPosition",
            new CoreGraphics.CGPoint { X = x, Y = y }));

    public bool SetSize(string address, int width, int height) =>
        width > 0 && height > 0 && UseWindow(address, (window, _) => _native.SetSizeAttribute(
            window,
            "AXSize",
            new CoreGraphics.CGSize { width = width, height = height }));

    public bool Zoom(string address) =>
        UseWindow(address, (window, _) => PressWindowButton(window, "AXZoomButton"));

    public bool ToggleFullscreen(string address) => UseWindow(address, (window, _) =>
    {
        var current = _native.GetBooleanAttribute(window, "AXFullScreen");
        if (current is not null
            && _native.SetBooleanAttribute(window, "AXFullScreen", !current.Value))
        {
            return true;
        }

        return PressWindowButton(window, "AXFullScreenButton");
    });

    public ScreenRect? GetContainingDisplayBounds(string address)
    {
        ScreenRect? bounds = null;
        _ = UseWindow(address, (window, _) =>
        {
            var frame = GetFrame(window);
            bounds = frame is null ? null : _native.GetContainingDisplayBounds(frame.Value);
            return bounds is not null;
        });
        return bounds;
    }

    public void Dispose() => _registry.Dispose();

    private WindowInfo? MapWindow(IntPtr window, int pid, bool isFocused)
    {
        if (!string.Equals(_native.GetStringAttribute(window, "AXRole"), "AXWindow", StringComparison.Ordinal))
        {
            return null;
        }

        var frame = GetFrame(window);
        if (frame is null)
        {
            return null;
        }

        var title = _native.GetStringAttribute(window, "AXTitle") ?? string.Empty;
        var windowId = _native.GetWindowId(pid, title, frame.Value) ?? 0;
        var processName = GetProcessName(pid);
        return new WindowInfo
        {
            Address = _registry.Register(
                window,
                MacOSWindowAddress.FromWindow(pid, windowId, title, frame.Value)),
            Title = title,
            Class = processName,
            Pid = pid,
            ProcessName = processName,
            IsFocused = isFocused,
            IsFullscreen = _native.GetBooleanAttribute(window, "AXFullScreen") is true,
            IsMaximized = _native.GetBooleanAttribute(window, "AXZoomed") is true,
            IsFloating = false,
            IsPinned = false,
            IsHidden = _native.GetBooleanAttribute(window, "AXMinimized") is true,
            X = frame.Value.X,
            Y = frame.Value.Y,
            Width = frame.Value.Width,
            Height = frame.Value.Height,
        };
    }

    private bool UseWindow(string address, Func<IntPtr, int, bool> operation)
    {
        if (!IsAvailable)
        {
            return false;
        }

        if (_registry.TryUse(address, (window, pid) =>
        {
            _native.SetMessagingTimeout(window, MessagingTimeoutSeconds);
            return operation(window, pid);
        }, out var operationResult))
        {
            return operationResult;
        }

        if (_registry.WasIssuedByThisRegistry(address))
        {
            return false;
        }

        return TryUseExternalAddress(address, operation);
    }

    private bool TryUseExternalAddress(string address, Func<IntPtr, int, bool> operation)
    {
        if (!MacOSWindowAddress.TryParse(address, out var target) || target.WindowId is 0)
        {
            return false;
        }

        using var application = _native.CreateApplicationElement(target.Pid);
        if (application.IsInvalid)
        {
            return false;
        }

        _native.SetMessagingTimeout(application.Value, MessagingTimeoutSeconds);
        using var windows = _native.CopyAttribute(application.Value, "AXWindows");
        if (windows.IsInvalid)
        {
            return false;
        }

        IntPtr exactMatch = IntPtr.Zero;
        foreach (var window in _native.GetArrayValues(windows.Value))
        {
            if (!TryGetAddress(window, target.Pid, out var candidate) || candidate != target)
            {
                continue;
            }

            if (exactMatch != IntPtr.Zero)
            {
                return false;
            }

            exactMatch = window;
        }

        if (exactMatch == IntPtr.Zero)
        {
            return false;
        }

        _native.SetMessagingTimeout(exactMatch, MessagingTimeoutSeconds);
        return operation(exactMatch, target.Pid);
    }

    private bool TryGetAddress(IntPtr window, int pid, out MacOSWindowAddress address)
    {
        address = default;
        if (!string.Equals(_native.GetStringAttribute(window, "AXRole"), "AXWindow", StringComparison.Ordinal)
            || GetFrame(window) is not { } frame)
        {
            return false;
        }

        address = MacOSWindowAddress.FromWindow(
            pid,
            _native.GetWindowId(pid, _native.GetStringAttribute(window, "AXTitle") ?? string.Empty, frame) ?? 0,
            _native.GetStringAttribute(window, "AXTitle") ?? string.Empty,
            frame);
        return true;
    }

    private bool PressWindowButton(IntPtr window, string attribute)
    {
        using var button = _native.CopyAttribute(window, attribute);
        return !button.IsInvalid && _native.PerformAction(button.Value, "AXPress");
    }

    private bool VerifyFocused(IntPtr application, IntPtr window)
    {
        using var focusedWindow = _native.CopyAttribute(application, "AXFocusedWindow");
        if (focusedWindow.IsInvalid
            || !_native.ElementsEqual(focusedWindow.Value, window))
        {
            return false;
        }

        using var systemWide = _native.CreateSystemWideElement();
        if (systemWide.IsInvalid)
        {
            return false;
        }

        using var focusedApplication = _native.CopyAttribute(
            systemWide.Value,
            "AXFocusedApplication");
        return !focusedApplication.IsInvalid
            && _native.ElementsEqual(focusedApplication.Value, application);
    }

    private ScreenRect? GetFrame(IntPtr window)
    {
        var position = _native.GetPointAttribute(window, "AXPosition");
        var size = _native.GetSizeAttribute(window, "AXSize");
        if (position is null || size is not { width: > 0, height: > 0 })
        {
            return null;
        }

        return new ScreenRect(
            checked((int)Math.Round(position.Value.X, MidpointRounding.AwayFromZero)),
            checked((int)Math.Round(position.Value.Y, MidpointRounding.AwayFromZero)),
            checked((int)Math.Round(size.Value.width, MidpointRounding.AwayFromZero)),
            checked((int)Math.Round(size.Value.height, MidpointRounding.AwayFromZero)));
    }

    private static ScreenRect ToFrame(WindowInfo info) =>
        new(info.X, info.Y, info.Width, info.Height);

    private static string GetProcessName(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }
}
