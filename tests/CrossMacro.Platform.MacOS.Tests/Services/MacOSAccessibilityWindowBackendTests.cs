namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSAccessibilityWindowBackendTests
{
    [Fact]
    public void GetWindows_KeepsStableIdentityAcrossTitleAndFrameChangesAndFiltersHiddenWindows()
    {
        using var native = new FakeWindowNative();
        native.AddVisibleWindow(FakeWindowNative.FirstWindow, "First", new ScreenRect(10, 20, 300, 200));
        native.AddVisibleWindow(FakeWindowNative.SecondWindow, "Hidden", new ScreenRect(30, 40, 300, 200), minimized: true);
        using var backend = new MacOSAccessibilityWindowBackend(native);

        var firstSnapshot = Assert.Single(backend.GetWindows());
        native.SetTitle(FakeWindowNative.FirstWindow, "Renamed");
        native.SetFrame(FakeWindowNative.FirstWindow, new ScreenRect(100, 120, 640, 480));
        var secondSnapshot = Assert.Single(backend.GetWindows());

        Assert.Equal(firstSnapshot.Address, secondSnapshot.Address);
        Assert.Equal("Renamed", secondSnapshot.Title);
        Assert.Equal(new ScreenRect(100, 120, 640, 480), ToFrame(secondSnapshot));
        Assert.Equal(1, native.RetainCounts[FakeWindowNative.FirstWindow]);
    }

    [Fact]
    public void CloseAndZoom_UseStandardWindowButtonsAndAxPress()
    {
        using var native = new FakeWindowNative();
        native.AddVisibleWindow(FakeWindowNative.FirstWindow, "First", new ScreenRect(10, 20, 300, 200));
        using var backend = new MacOSAccessibilityWindowBackend(native);
        var window = Assert.Single(backend.GetWindows());

        Assert.True(backend.Zoom(window.Address));
        Assert.True(backend.Close(window.Address));

        Assert.Contains((FakeWindowNative.ZoomButton, "AXPress"), native.Actions);
        Assert.Contains((FakeWindowNative.CloseButton, "AXPress"), native.Actions);
        Assert.DoesNotContain(native.Actions, static action => action.Action is "AXClose" or "AXZoomWindow");
        Assert.False(backend.SetPosition(window.Address, 1, 2));
    }

    [Fact]
    public void ToggleFullscreen_WhenAttributeCannotBeSet_PressesFullscreenButton()
    {
        using var native = new FakeWindowNative { AllowFullscreenAttributeWrite = false };
        native.AddVisibleWindow(FakeWindowNative.FirstWindow, "First", new ScreenRect(10, 20, 300, 200));
        using var backend = new MacOSAccessibilityWindowBackend(native);
        var window = Assert.Single(backend.GetWindows());

        Assert.True(backend.ToggleFullscreen(window.Address));

        Assert.Contains((FakeWindowNative.FullscreenButton, "AXPress"), native.Actions);
    }

    [Fact]
    public void Focus_ActivatesApplicationAndVerifiesFocusedWindow()
    {
        using var native = new FakeWindowNative();
        native.AddVisibleWindow(FakeWindowNative.FirstWindow, "First", new ScreenRect(10, 20, 300, 200));
        native.FocusedWindow = FakeWindowNative.FirstWindow;
        native.FocusedApplication = FakeWindowNative.Application;
        using var backend = new MacOSAccessibilityWindowBackend(native);
        var window = Assert.Single(backend.GetWindows());

        Assert.True(backend.Focus(window.Address));

        Assert.Contains((FakeWindowNative.Application, "AXFrontmost", true), native.BooleanWrites);
        Assert.Contains((FakeWindowNative.FirstWindow, "AXMinimized", false), native.BooleanWrites);
        Assert.Contains((FakeWindowNative.FirstWindow, "AXRaise"), native.Actions);
    }

    [Fact]
    public void ExactExternalAddress_ResolvesAcrossBackendInstancesWithoutRetargetingStaleMetadata()
    {
        using var native = new FakeWindowNative();
        native.AddVisibleWindow(FakeWindowNative.FirstWindow, "First", new ScreenRect(10, 20, 300, 200));
        string address;
        using (var firstBackend = new MacOSAccessibilityWindowBackend(native))
        {
            address = Assert.Single(firstBackend.GetWindows()).Address;
        }

        using var secondBackend = new MacOSAccessibilityWindowBackend(native);
        Assert.True(secondBackend.SetPosition(address, 100, 200));

        native.SetTitle(FakeWindowNative.FirstWindow, "Renamed");
        Assert.False(secondBackend.Close(address));
        Assert.DoesNotContain((FakeWindowNative.CloseButton, "AXPress"), native.Actions);
    }

    [Fact]
    public void ExactExternalAddress_WhenMultipleWindowsAreIdentical_RejectsAmbiguousMutation()
    {
        using var native = new FakeWindowNative();
        var frame = new ScreenRect(10, 20, 300, 200);
        native.AddVisibleWindow(FakeWindowNative.FirstWindow, "Same", frame);
        string address;
        using (var firstBackend = new MacOSAccessibilityWindowBackend(native))
        {
            address = Assert.Single(firstBackend.GetWindows()).Address;
        }

        native.AddVisibleWindow(FakeWindowNative.SecondWindow, "Same", frame);
        using var secondBackend = new MacOSAccessibilityWindowBackend(native);

        Assert.False(secondBackend.Close(address));
        Assert.DoesNotContain((FakeWindowNative.CloseButton, "AXPress"), native.Actions);
    }

    [Fact]
    public void ExactExternalAddress_WhenWindowIsRecreatedWithSameMetadata_RejectsStaleWindowId()
    {
        using var native = new FakeWindowNative();
        var frame = new ScreenRect(10, 20, 300, 200);
        native.AddVisibleWindow(FakeWindowNative.FirstWindow, "Same", frame, windowId: 100);
        string address;
        using (var firstBackend = new MacOSAccessibilityWindowBackend(native))
        {
            address = Assert.Single(firstBackend.GetWindows()).Address;
        }

        native.RemoveWindow(FakeWindowNative.FirstWindow);
        native.AddVisibleWindow(FakeWindowNative.SecondWindow, "Same", frame, windowId: 200);
        using var secondBackend = new MacOSAccessibilityWindowBackend(native);

        Assert.False(secondBackend.Close(address));
        Assert.DoesNotContain((FakeWindowNative.CloseButton, "AXPress"), native.Actions);
    }

    private static ScreenRect ToFrame(WindowInfo info) => new(info.X, info.Y, info.Width, info.Height);

    private sealed class FakeWindowNative : IMacOSWindowNative, IDisposable
    {
        internal static readonly IntPtr SystemWide = new(1);
        internal static readonly IntPtr Application = new(2);
        internal static readonly IntPtr WindowsArray = new(3);
        internal static readonly IntPtr FirstWindow = new(10);
        internal static readonly IntPtr SecondWindow = new(11);
        internal static readonly IntPtr CloseButton = new(20);
        internal static readonly IntPtr ZoomButton = new(21);
        internal static readonly IntPtr FullscreenButton = new(22);

        private readonly Dictionary<IntPtr, string> _titles = [];
        private readonly Dictionary<IntPtr, ScreenRect> _frames = [];
        private readonly Dictionary<IntPtr, uint> _windowIds = [];
        private readonly HashSet<IntPtr> _minimized = [];
        private readonly List<IntPtr> _windows = [];
        private readonly List<MacOSCfSafeHandle> _createdHandles = [];

        public bool IsAvailable => true;
        public bool AllowFullscreenAttributeWrite { get; init; } = true;
        public IntPtr FocusedWindow { get; set; }
        public IntPtr FocusedApplication { get; set; }
        public Dictionary<IntPtr, int> RetainCounts { get; } = [];
        public List<(IntPtr Element, string Action)> Actions { get; } = [];
        public List<(IntPtr Element, string Attribute, bool Value)> BooleanWrites { get; } = [];

        public void AddVisibleWindow(
            IntPtr window,
            string title,
            ScreenRect frame,
            bool minimized = false,
            uint? windowId = null)
        {
            _windows.Add(window);
            _titles[window] = title;
            _frames[window] = frame;
            _windowIds[window] = windowId ?? checked((uint)window.ToInt64());
            if (minimized)
            {
                _ = _minimized.Add(window);
            }
        }

        public void RemoveWindow(IntPtr window)
        {
            _ = _windows.Remove(window);
            _ = _titles.Remove(window);
            _ = _frames.Remove(window);
            _ = _windowIds.Remove(window);
            _ = _minimized.Remove(window);
        }

        public void SetTitle(IntPtr window, string title) => _titles[window] = title;
        public void SetFrame(IntPtr window, ScreenRect frame) => _frames[window] = frame;

        public MacOSCfSafeHandle CreateSystemWideElement() => CreateHandle(SystemWide);
        public MacOSCfSafeHandle CreateApplicationElement(int pid) => CreateHandle(Application);

        public MacOSCfSafeHandle CopyAttribute(IntPtr element, string attribute)
        {
            var value = (element, attribute) switch
            {
                (var e, "AXFocusedApplication") when e == SystemWide => FocusedApplication,
                (var e, "AXFocusedWindow") when e == Application => FocusedWindow,
                (var e, "AXWindows") when e == Application => WindowsArray,
                (var e, "AXCloseButton") when _frames.ContainsKey(e) => CloseButton,
                (var e, "AXZoomButton") when _frames.ContainsKey(e) => ZoomButton,
                (var e, "AXFullScreenButton") when _frames.ContainsKey(e) => FullscreenButton,
                _ => IntPtr.Zero,
            };
            return CreateHandle(value);
        }

        public IReadOnlyList<IntPtr> GetArrayValues(IntPtr array) => array == WindowsArray ? _windows : [];
        public int? GetPid(IntPtr element) => element == Application ? 123 : null;
        public string? GetStringAttribute(IntPtr element, string attribute) => attribute switch
        {
            "AXRole" when _frames.ContainsKey(element) => "AXWindow",
            "AXTitle" when _titles.TryGetValue(element, out var title) => title,
            _ => null,
        };

        public bool? GetBooleanAttribute(IntPtr element, string attribute) => attribute switch
        {
            "AXMinimized" => _minimized.Contains(element),
            "AXFullScreen" => false,
            "AXZoomed" => false,
            _ => null,
        };

        public CoreGraphics.CGPoint? GetPointAttribute(IntPtr element, string attribute) =>
            string.Equals(attribute, "AXPosition", StringComparison.Ordinal)
                && _frames.TryGetValue(element, out var frame)
                ? new CoreGraphics.CGPoint { X = frame.X, Y = frame.Y }
                : null;

        public CoreGraphics.CGSize? GetSizeAttribute(IntPtr element, string attribute) =>
            string.Equals(attribute, "AXSize", StringComparison.Ordinal)
                && _frames.TryGetValue(element, out var frame)
                ? new CoreGraphics.CGSize { width = frame.Width, height = frame.Height }
                : null;

        public bool SetBooleanAttribute(IntPtr element, string attribute, bool value)
        {
            BooleanWrites.Add((element, attribute, value));
            return !string.Equals(attribute, "AXFullScreen", StringComparison.Ordinal)
                || AllowFullscreenAttributeWrite;
        }

        public bool SetElementAttribute(IntPtr element, string attribute, IntPtr value)
        {
            if (element is var application
                && application == Application
                && string.Equals(attribute, "AXFocusedWindow", StringComparison.Ordinal))
            {
                FocusedWindow = value;
            }

            return true;
        }

        public bool SetPointAttribute(IntPtr element, string attribute, CoreGraphics.CGPoint point) => _frames.ContainsKey(element);
        public bool SetSizeAttribute(IntPtr element, string attribute, CoreGraphics.CGSize size) => _frames.ContainsKey(element);

        public bool PerformAction(IntPtr element, string action)
        {
            Actions.Add((element, action));
            return true;
        }

        public bool ElementsEqual(IntPtr left, IntPtr right) => left == right;
        public void SetMessagingTimeout(IntPtr element, float timeoutSeconds) { }
        public IReadOnlyCollection<int> GetOnScreenApplicationPids() => [123];
        public uint? GetWindowId(int pid, string title, ScreenRect frame)
        {
            uint? match = null;
            foreach (var window in _windows)
            {
                if (!_titles.TryGetValue(window, out var candidateTitle)
                    || !_frames.TryGetValue(window, out var candidateFrame)
                    || !string.Equals(candidateTitle, title, StringComparison.Ordinal)
                    || candidateFrame != frame)
                {
                    continue;
                }

                if (match is not null)
                {
                    return null;
                }

                match = _windowIds[window];
            }

            return match;
        }
        public bool IsFrameOnScreen(ScreenRect frame) => true;
        public ScreenRect? GetContainingDisplayBounds(ScreenRect frame) => new ScreenRect(0, 0, 1920, 1080);

        public IntPtr Retain(IntPtr element)
        {
            RetainCounts[element] = RetainCounts.GetValueOrDefault(element) + 1;
            return element;
        }

        public bool Release(IntPtr element) => true;

        public void Dispose()
        {
            foreach (var handle in _createdHandles)
            {
                handle.Dispose();
            }
        }

        private MacOSCfSafeHandle CreateHandle(IntPtr value)
        {
            var handle = new MacOSCfSafeHandle(value, static _ => true);
            _createdHandles.Add(handle);
            return handle;
        }
    }
}
