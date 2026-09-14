
namespace CrossMacro.Platform.Windows.Services.WindowManagement;

internal sealed class WindowsWindowBackend : IWindowsWindowBackend
{
    public bool IsSupported => OperatingSystem.IsWindows();

    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult<WindowInfo?>(null);
        }

        var hwnd = User32.GetForegroundWindow();
        return Task.FromResult(hwnd == IntPtr.Zero ? null : MapWindow(hwnd));
    }

    public Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult<IReadOnlyList<WindowInfo>>([]);
        }

        var windows = new List<WindowInfo>();
        _ = User32.EnumWindows((hwnd, _) =>
        {
            if (IsRealDesktopWindow(hwnd))
            {
                windows.Add(MapWindow(hwnd));
            }

            return true;
        }, IntPtr.Zero);

        return Task.FromResult<IReadOnlyList<WindowInfo>>(windows);
    }

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        if (!TryParseHwnd(address, out var hwnd))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(FocusWindow(hwnd));
    }





    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        if (!TryParseHwnd(address, out var hwnd) || !User32.IsWindow(hwnd))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(User32.PostMessage(hwnd, User32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero));
    }



    public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        if (!TryGetActiveWindowPlacement(out var placement))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(User32.SetWindowPos(
            placement.Hwnd,
            IntPtr.Zero,
            x - placement.LeftMargin,
            y - placement.TopMargin,
            0,
            0,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_NOSIZE));
    }

    public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        if (width <= 0 || height <= 0 || !TryGetActiveWindowPlacement(out var placement))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(User32.SetWindowPos(
            placement.Hwnd,
            IntPtr.Zero,
            0,
            0,
            width + placement.HorizontalMargin,
            height + placement.VerticalMargin,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_NOMOVE));
    }

    public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default) =>
        ShowActiveWindowAsync(User32.SW_MAXIMIZE, cancellationToken);

    public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default) =>
        ShowActiveWindowAsync(User32.SW_MAXIMIZE, cancellationToken);

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        if (!TryGetForegroundWindow(out var hwnd))
        {
            return Task.FromResult(false);
        }

        var exStyle = GetExtendedStyle(hwnd);
        var insertAfter = (exStyle & User32.WS_EX_TOPMOST) == 0 ? User32.HWND_TOPMOST : User32.HWND_NOTOPMOST;
        return Task.FromResult(User32.SetWindowPos(
            hwnd,
            insertAfter,
            0,
            0,
            0,
            0,
            User32.SWP_NOMOVE | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE));
    }

    public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        if (!TryGetActiveWindowPlacement(out var placement))
        {
            return Task.FromResult(false);
        }

        var monitor = User32.MonitorFromWindow(placement.Hwnd, User32.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return Task.FromResult(false);
        }

        var monitorInfo = new User32.MonitorInfo { cbSize = (uint)Marshal.SizeOf<User32.MonitorInfo>() };
        if (!User32.GetMonitorInfoW(monitor, ref monitorInfo))
        {
            return Task.FromResult(false);
        }

        var work = monitorInfo.rcWork;
        var x = work.left + ((work.right - work.left - placement.VisibleWidth) / 2);
        var y = work.top + ((work.bottom - work.top - placement.VisibleHeight) / 2);

        return Task.FromResult(User32.SetWindowPos(
            placement.Hwnd,
            IntPtr.Zero,
            x - placement.LeftMargin,
            y - placement.TopMargin,
            0,
            0,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_NOSIZE));
    }

    public Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult<string?>(null);
        }

        if (!TryGetForegroundWindow(out var hwnd))
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult(GetWindowDesktopId(hwnd));
    }

    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        CanceledAwareUnsupportedResultAsync(cancellationToken);

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        if (!TryGetForegroundWindow(out var hwnd))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(MoveWindowToWorkspace(hwnd, workspace));
    }

    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
        {
            return Task.FromResult(false);
        }

        if (!TryParseHwnd(address, out var hwnd))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(MoveWindowToWorkspace(hwnd, workspace));
    }

    private static string GetProcessName(int pid)
    {
        if (pid <= 0)
        {
            return string.Empty;
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
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

    private static WindowInfo MapWindow(IntPtr hwnd)
    {
        var visibleBounds = GetVisibleBounds(hwnd);
        var title = GetWindowText(hwnd);
        var className = GetClassName(hwnd);
        var foreground = User32.GetForegroundWindow();
        var exStyle = GetExtendedStyle(hwnd);

        _ = User32.GetWindowThreadProcessId(hwnd, out var processId);

        return new WindowInfo
        {
            Address = hwnd.ToInt64().ToString(CultureInfo.InvariantCulture),
            Title = title,
            Class = className,
            Pid = processId <= int.MaxValue ? (int)processId : -1,
            ProcessName = processId <= int.MaxValue ? GetProcessName((int)processId) : string.Empty,
            Workspace = GetWindowDesktopId(hwnd) ?? string.Empty,
            IsFocused = hwnd == foreground,
            IsFullscreen = IsFullscreen(hwnd, visibleBounds),
            IsMaximized = User32.IsZoomed(hwnd),
            IsFloating = (exStyle & User32.WS_EX_TOPMOST) != 0,
            IsPinned = false,
            IsHidden = false,
            X = visibleBounds.left,
            Y = visibleBounds.top,
            Width = Math.Max(0, visibleBounds.right - visibleBounds.left),
            Height = Math.Max(0, visibleBounds.bottom - visibleBounds.top),
        };
    }

    private static bool IsRealDesktopWindow(IntPtr hwnd)
    {
        if (!User32.IsWindowVisible(hwnd))
        {
            return false;
        }

        if (Dwmapi.DwmGetWindowAttribute(hwnd, Dwmapi.DWMWA_CLOAKED, out int cloaked, sizeof(int)) is 0 && cloaked is not 0)
        {
            return false;
        }

        var exStyle = GetExtendedStyle(hwnd);
        if ((exStyle & User32.WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        if ((exStyle & User32.WS_EX_APPWINDOW) != 0)
        {
            return User32.GetWindowTextLengthW(hwnd) > 0;
        }

        var walk = User32.GetAncestor(hwnd, User32.GA_ROOTOWNER);
        while (true)
        {
            var pop = User32.GetLastActivePopup(walk);
            if (pop == walk || User32.IsWindowVisible(pop))
            {
                walk = pop;
                break;
            }

            walk = pop;
        }

        if (walk != hwnd)
        {
            return false;
        }

        return User32.GetWindowTextLengthW(hwnd) > 0;
    }

    private static bool TryGetActiveWindowPlacement(out WindowsWindowPlacement placement)
    {
        placement = default;
        if (!TryGetForegroundWindow(out var hwnd) || !User32.GetWindowRect(hwnd, out var outerBounds))
        {
            return false;
        }

        var visibleBounds = GetVisibleBounds(hwnd);
        placement = new WindowsWindowPlacement(hwnd, outerBounds, visibleBounds);
        return true;
    }

    private static bool TryGetForegroundWindow(out IntPtr hwnd)
    {
        hwnd = User32.GetForegroundWindow();
        return hwnd != IntPtr.Zero && User32.IsWindow(hwnd);
    }

    private static Task<bool> ShowActiveWindowAsync(int command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(false);
        }

        if (!TryGetForegroundWindow(out var hwnd))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(User32.ShowWindow(hwnd, command));
    }

    private static Task<bool> CanceledAwareUnsupportedResultAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    private static RectStruct GetVisibleBounds(IntPtr hwnd)
    {
        if (Dwmapi.DwmGetWindowAttribute(hwnd, Dwmapi.DWMWA_EXTENDED_FRAME_BOUNDS, out RectStruct bounds, Marshal.SizeOf<RectStruct>()) is 0)
        {
            return bounds;
        }

        return User32.GetWindowRect(hwnd, out bounds) ? bounds : default;
    }

    private static string GetWindowText(IntPtr hwnd)
    {
        var length = User32.GetWindowTextLengthW(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[length + 1];
        int copied;
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            copied = User32.GetWindowTextW(hwnd, handle.AddrOfPinnedObject(), buffer.Length);
        }
        finally
        {
            handle.Free();
        }
        if (copied <= 0)
        {
            return string.Empty;
        }

        return new string(buffer, 0, copied);
    }

    private static string GetClassName(IntPtr hwnd)
    {
        const int ClassNameBufferLength = 256;
        var buffer = new char[ClassNameBufferLength];
        int copied;
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            copied = User32.GetClassNameW(hwnd, handle.AddrOfPinnedObject(), buffer.Length);
        }
        finally
        {
            handle.Free();
        }
        if (copied <= 0)
        {
            return string.Empty;
        }

        return new string(buffer, 0, copied);
    }

    public Task<WindowInfo?> FindWindowAsync(Func<WindowInfo, bool> predicate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(predicate);
        if (!IsSupported) { return Task.FromResult<WindowInfo?>(null); }
        WindowInfo? found = null;
        _ = User32.EnumWindows((hwnd, _) =>
        {
            if (!IsRealDesktopWindow(hwnd)) { return true; }
            var info = MapWindow(hwnd);
            if (!predicate(info)) { return true; }
            found = info;
            return false;
        }, IntPtr.Zero);
        return Task.FromResult(found);
    }

    private static bool FocusWindow(IntPtr hwnd) => new WindowsWindowFocus(new WindowsWindowFocusNative()).Focus(hwnd);

    private static string? GetWindowDesktopId(IntPtr hwnd)
    {
        try
        {
            using var manager = CreateVirtualDesktopManager();
            return manager.GetWindowDesktopId(hwnd, out var desktopId) is 0 ? desktopId.ToString() : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static bool MoveWindowToWorkspace(IntPtr hwnd, string workspace)
    {
        if (!User32.IsWindow(hwnd) || !Guid.TryParse(workspace, out var desktopId))
        {
            return false;
        }

        try
        {
            using var manager = CreateVirtualDesktopManager();
            return manager.MoveWindowToDesktop(hwnd, ref desktopId) is 0;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool TryParseHwnd(string address, out IntPtr hwnd)
        => WindowsWindowAddressParser.TryParse(address, out hwnd);

    private static VirtualDesktopManager CreateVirtualDesktopManager()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Virtual Desktop Manager is available only on Windows.");
        }

        return VirtualDesktopManager.Create();
    }

    private static uint GetExtendedStyle(IntPtr hwnd) =>
        (uint)User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE).ToInt64();

    private static bool IsFullscreen(IntPtr hwnd, RectStruct visibleBounds)
    {
        var monitor = User32.MonitorFromWindow(hwnd, User32.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new User32.MonitorInfo { cbSize = (uint)Marshal.SizeOf<User32.MonitorInfo>() };
        if (!User32.GetMonitorInfoW(monitor, ref monitorInfo))
        {
            return false;
        }

        var monitorBounds = monitorInfo.rcMonitor;
        return visibleBounds.left <= monitorBounds.left
            && visibleBounds.top <= monitorBounds.top
            && visibleBounds.right >= monitorBounds.right
            && visibleBounds.bottom >= monitorBounds.bottom;
    }

}
