using System.Diagnostics;

namespace CrossMacro.Platform.Windows.Services.WindowManagement;

/// <summary>
/// Builds <see cref="WindowInfo"/> values for one enumeration pass. The virtual desktop COM object,
/// the foreground window and the process-name lookups are shared by every window of the pass.
/// </summary>
internal sealed class WindowsWindowMapper : IDisposable
{
    private const int StackBufferChars = 512;
    private const int ClassNameBufferLength = 256;

    private readonly VirtualDesktopManager? _desktops = TryCreateVirtualDesktopManager();
    private readonly Dictionary<int, string> _processNames = [];
    private readonly IntPtr _foreground = User32.GetForegroundWindow();

    public WindowInfo Map(IntPtr hwnd)
    {
        var visibleBounds = WindowsWindowBackend.GetVisibleBounds(hwnd);
        var exStyle = WindowsWindowBackend.GetExtendedStyle(hwnd);

        _ = User32.GetWindowThreadProcessId(hwnd, out var processId);
        int pid = processId <= int.MaxValue ? (int)processId : -1;

        return new WindowInfo
        {
            Address = hwnd.ToInt64().ToString(CultureInfo.InvariantCulture),
            Title = GetWindowText(hwnd),
            Class = GetClassName(hwnd),
            Pid = pid,
            ProcessName = GetCachedProcessName(pid),
            Workspace = GetDesktopId(hwnd) ?? string.Empty,
            IsFocused = hwnd == _foreground,
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

    public void Dispose() => _desktops?.Dispose();

    private static VirtualDesktopManager? TryCreateVirtualDesktopManager()
    {
        try
        {
            return WindowsWindowBackend.CreateVirtualDesktopManager();
        }
        catch (COMException)
        {
            return null;
        }
    }

    private string GetCachedProcessName(int pid)
    {
        if (!_processNames.TryGetValue(pid, out var name))
        {
            name = GetProcessName(pid);
            _processNames[pid] = name;
        }

        return name;
    }

    private string? GetDesktopId(IntPtr hwnd) =>
        _desktops is not null && _desktops.GetWindowDesktopId(hwnd, out var desktopId) is 0
            ? desktopId.ToString()
            : null;

    private static string GetProcessName(int pid)
    {
        if (pid <= 0)
        {
            return string.Empty;
        }

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

    private static string GetWindowText(IntPtr hwnd)
    {
        var length = User32.GetWindowTextLengthW(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        Span<char> buffer = length < StackBufferChars ? stackalloc char[StackBufferChars] : new char[length + 1];
        int copied = User32.GetWindowTextW(hwnd, ref MemoryMarshal.GetReference(buffer), buffer.Length);
        return copied <= 0 ? string.Empty : new string(buffer[..copied]);
    }

    private static string GetClassName(IntPtr hwnd)
    {
        Span<char> buffer = stackalloc char[ClassNameBufferLength];
        int copied = User32.GetClassNameW(hwnd, ref MemoryMarshal.GetReference(buffer), buffer.Length);
        return copied <= 0 ? string.Empty : new string(buffer[..copied]);
    }

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
