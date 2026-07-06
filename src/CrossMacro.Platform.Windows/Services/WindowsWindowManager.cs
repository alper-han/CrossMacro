using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Windows.Native;

namespace CrossMacro.Platform.Windows.Services;

public sealed class WindowsWindowManager : IWindowManager
{
    public bool IsSupported => OperatingSystem.IsWindows();

    public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Task.FromResult<WindowInfo?>(null);

        var hwnd = User32.GetForegroundWindow();
        return Task.FromResult(hwnd == IntPtr.Zero ? null : MapWindow(hwnd));
    }

    public Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Task.FromResult<IReadOnlyList<WindowInfo>>([]);

        var windows = new List<WindowInfo>();
        User32.EnumWindows((hwnd, _) =>
        {
            if (IsRealDesktopWindow(hwnd))
                windows.Add(MapWindow(hwnd));

            return true;
        }, IntPtr.Zero);

        return Task.FromResult<IReadOnlyList<WindowInfo>>(windows);
    }

    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Task.FromResult(false);

        if (!TryParseHwnd(address, out var hwnd))
            return Task.FromResult(false);

        return Task.FromResult(FocusWindow(hwnd));
    }

    public Task<bool> FocusWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Task.FromResult(false);

        if (string.IsNullOrWhiteSpace(titleSubstring))
            return Task.FromResult(false);

        var hwnd = FindWindow(info => info.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(hwnd != IntPtr.Zero && FocusWindow(hwnd));
    }

    public Task<bool> FocusWindowByClassAsync(string classSubstring, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Task.FromResult(false);

        if (string.IsNullOrWhiteSpace(classSubstring))
            return Task.FromResult(false);

        var hwnd = FindWindow(info => info.Class.Contains(classSubstring, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(hwnd != IntPtr.Zero && FocusWindow(hwnd));
    }

    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Task.FromResult(false);

        if (!TryParseHwnd(address, out var hwnd) || !User32.IsWindow(hwnd))
            return Task.FromResult(false);

        return Task.FromResult(User32.PostMessage(hwnd, User32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero));
    }

    public Task<bool> CloseWindowByTitleAsync(string titleSubstring, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Task.FromResult(false);

        if (string.IsNullOrWhiteSpace(titleSubstring))
            return Task.FromResult(false);

        var hwnd = FindWindow(info => info.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(hwnd != IntPtr.Zero && User32.PostMessage(hwnd, User32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero));
    }

    public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Task.FromResult(false);

        if (!TryGetActiveWindowPlacement(out var placement))
            return Task.FromResult(false);

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
        if (!IsSupported)
            return Task.FromResult(false);

        if (width <= 0 || height <= 0 || !TryGetActiveWindowPlacement(out var placement))
            return Task.FromResult(false);

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
        ShowActiveWindow(User32.SW_MAXIMIZE);

    public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default) =>
        ShowActiveWindow(User32.SW_MAXIMIZE);

    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Task.FromResult(false);

        if (!TryGetForegroundWindow(out var hwnd))
            return Task.FromResult(false);

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
        if (!IsSupported)
            return Task.FromResult(false);

        if (!TryGetActiveWindowPlacement(out var placement))
            return Task.FromResult(false);

        var monitor = User32.MonitorFromWindow(placement.Hwnd, User32.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return Task.FromResult(false);

        var monitorInfo = new User32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<User32.MONITORINFO>() };
        if (!User32.GetMonitorInfoW(monitor, ref monitorInfo))
            return Task.FromResult(false);

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
        if (!IsSupported)
            return Task.FromResult<string?>(null);

        if (!TryGetForegroundWindow(out var hwnd))
            return Task.FromResult<string?>(null);

        return Task.FromResult(GetWindowDesktopId(hwnd));
    }

    public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Task.FromResult(false);

        if (!TryGetForegroundWindow(out var hwnd))
            return Task.FromResult(false);

        return Task.FromResult(MoveWindowToWorkspace(hwnd, workspace));
    }

    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Task.FromResult(false);

        if (!TryParseHwnd(address, out var hwnd))
            return Task.FromResult(false);

        return Task.FromResult(MoveWindowToWorkspace(hwnd, workspace));
    }

    private static WindowInfo MapWindow(IntPtr hwnd)
    {
        var visibleBounds = GetVisibleBounds(hwnd);
        var title = GetWindowText(hwnd);
        var className = GetClassName(hwnd);
        var foreground = User32.GetForegroundWindow();
        var exStyle = GetExtendedStyle(hwnd);

        User32.GetWindowThreadProcessId(hwnd, out var processId);

        return new WindowInfo
        {
            Address = hwnd.ToInt64().ToString(),
            Title = title,
            Class = className,
            Pid = processId <= int.MaxValue ? (int)processId : -1,
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
            Height = Math.Max(0, visibleBounds.bottom - visibleBounds.top)
        };
    }

    private static bool IsRealDesktopWindow(IntPtr hwnd)
    {
        if (!User32.IsWindowVisible(hwnd))
            return false;

        if (Dwmapi.DwmGetWindowAttribute(hwnd, Dwmapi.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;

        var exStyle = GetExtendedStyle(hwnd);
        if ((exStyle & User32.WS_EX_TOOLWINDOW) != 0)
            return false;

        if ((exStyle & User32.WS_EX_APPWINDOW) != 0)
            return User32.GetWindowTextLengthW(hwnd) > 0;

        var root = User32.GetAncestor(hwnd, User32.GA_ROOTOWNER);
        var walk = root;
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
            return false;

        return User32.GetWindowTextLengthW(hwnd) > 0;
    }

    private static bool TryGetActiveWindowPlacement(out WindowPlacement placement)
    {
        placement = default;
        if (!TryGetForegroundWindow(out var hwnd) || !User32.GetWindowRect(hwnd, out var outerBounds))
            return false;

        var visibleBounds = GetVisibleBounds(hwnd);
        placement = new WindowPlacement(hwnd, outerBounds, visibleBounds);
        return true;
    }

    private static bool TryGetForegroundWindow(out IntPtr hwnd)
    {
        hwnd = User32.GetForegroundWindow();
        return hwnd != IntPtr.Zero && User32.IsWindow(hwnd);
    }

    private static Task<bool> ShowActiveWindow(int command)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(false);

        if (!TryGetForegroundWindow(out var hwnd))
            return Task.FromResult(false);

        return Task.FromResult(User32.ShowWindow(hwnd, command));
    }

    private static RECT GetVisibleBounds(IntPtr hwnd)
    {
        if (Dwmapi.DwmGetWindowAttribute(hwnd, Dwmapi.DWMWA_EXTENDED_FRAME_BOUNDS, out RECT bounds, Marshal.SizeOf<RECT>()) == 0)
            return bounds;

        return User32.GetWindowRect(hwnd, out bounds) ? bounds : default;
    }

    private static string GetWindowText(IntPtr hwnd)
    {
        var length = User32.GetWindowTextLengthW(hwnd);
        if (length <= 0)
            return string.Empty;

        var buffer = new StringBuilder(length + 1);
        var copied = User32.GetWindowTextW(hwnd, buffer, buffer.Capacity);
        return copied <= 0 ? string.Empty : buffer.ToString();
    }

    private static string GetClassName(IntPtr hwnd)
    {
        const int ClassNameBufferLength = 256;
        var buffer = new StringBuilder(ClassNameBufferLength);
        var copied = User32.GetClassNameW(hwnd, buffer, buffer.Capacity);
        return copied <= 0 ? string.Empty : buffer.ToString();
    }

    private static IntPtr FindWindow(Func<WindowInfo, bool> predicate)
    {
        var found = IntPtr.Zero;
        User32.EnumWindows((hwnd, _) =>
        {
            if (!IsRealDesktopWindow(hwnd) || !predicate(MapWindow(hwnd)))
                return true;

            found = hwnd;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    private static bool FocusWindow(IntPtr hwnd)
    {
        if (!User32.IsWindow(hwnd))
            return false;

        var currentThread = Kernel32.GetCurrentThreadId();
        var targetThread = User32.GetWindowThreadProcessId(hwnd, out _);
        var foreground = User32.GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : User32.GetWindowThreadProcessId(foreground, out _);
        var attachedToTarget = false;
        var attachedToForeground = false;
        var oldTimeout = 0u;
        var timeoutRead = User32.SystemParametersInfo(User32.SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref oldTimeout, 0);

        try
        {
            User32.SystemParametersInfo(User32.SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, 0);
            User32.AllowSetForegroundWindow(User32.ASFW_ANY);
            User32.LockSetForegroundWindow(User32.LSFW_UNLOCK);

            if (targetThread != 0 && targetThread != currentThread)
                attachedToTarget = User32.AttachThreadInput(currentThread, targetThread, true);

            if (foregroundThread != 0 && foregroundThread != currentThread && foregroundThread != targetThread)
                attachedToForeground = User32.AttachThreadInput(currentThread, foregroundThread, true);

            if (User32.IsIconic(hwnd))
                User32.ShowWindow(hwnd, User32.SW_RESTORE);

            User32.BringWindowToTop(hwnd);
            var focused = User32.SetForegroundWindow(hwnd);
            User32.SetActiveWindow(hwnd);
            User32.SetFocus(hwnd);
            return focused || User32.GetForegroundWindow() == hwnd;
        }
        finally
        {
            if (attachedToForeground)
                User32.AttachThreadInput(currentThread, foregroundThread, false);

            if (attachedToTarget)
                User32.AttachThreadInput(currentThread, targetThread, false);

            if (timeoutRead)
                User32.SystemParametersInfo(User32.SPI_SETFOREGROUNDLOCKTIMEOUT, 0, new IntPtr(oldTimeout), 0);
        }
    }

    private static string? GetWindowDesktopId(IntPtr hwnd)
    {
        try
        {
            using var manager = CreateVirtualDesktopManager();
            return manager.GetWindowDesktopId(hwnd, out var desktopId) == 0 ? desktopId.ToString() : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static bool MoveWindowToWorkspace(IntPtr hwnd, string workspace)
    {
        if (!User32.IsWindow(hwnd) || !Guid.TryParse(workspace, out var desktopId))
            return false;

        try
        {
            using var manager = CreateVirtualDesktopManager();
            return manager.MoveWindowToDesktop(hwnd, ref desktopId) == 0;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool TryParseHwnd(string address, out IntPtr hwnd)
    {
        hwnd = IntPtr.Zero;
        if (!long.TryParse(address, out var handleVal))
            return false;

        hwnd = new IntPtr(handleVal);
        return hwnd != IntPtr.Zero;
    }

    private static VirtualDesktopManager CreateVirtualDesktopManager()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows Virtual Desktop Manager is available only on Windows.");

        return VirtualDesktopManager.Create();
    }

    private static uint GetExtendedStyle(IntPtr hwnd) =>
        (uint)User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE).ToInt64();

    private static bool IsFullscreen(IntPtr hwnd, RECT visibleBounds)
    {
        var monitor = User32.MonitorFromWindow(hwnd, User32.MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return false;

        var monitorInfo = new User32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<User32.MONITORINFO>() };
        if (!User32.GetMonitorInfoW(monitor, ref monitorInfo))
            return false;

        var monitorBounds = monitorInfo.rcMonitor;
        return visibleBounds.left <= monitorBounds.left
            && visibleBounds.top <= monitorBounds.top
            && visibleBounds.right >= monitorBounds.right
            && visibleBounds.bottom >= monitorBounds.bottom;
    }

    private readonly record struct WindowPlacement(IntPtr Hwnd, RECT OuterBounds, RECT VisibleBounds)
    {
        public int LeftMargin => VisibleBounds.left - OuterBounds.left;
        public int TopMargin => VisibleBounds.top - OuterBounds.top;
        public int RightMargin => OuterBounds.right - VisibleBounds.right;
        public int BottomMargin => OuterBounds.bottom - VisibleBounds.bottom;
        public int HorizontalMargin => LeftMargin + RightMargin;
        public int VerticalMargin => TopMargin + BottomMargin;
        public int VisibleWidth => Math.Max(0, VisibleBounds.right - VisibleBounds.left);
        public int VisibleHeight => Math.Max(0, VisibleBounds.bottom - VisibleBounds.top);
    }
}
