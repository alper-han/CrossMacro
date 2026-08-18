
namespace CrossMacro.Platform.Linux.Services;

public sealed class X11InputSimulator : IInputSimulator, IInputSimulatorCapabilities
{
    private IntPtr _display;
    private bool _disposed;

    public string ProviderName => "X11 (XTest)";

    public bool IsSupported { get; }
    public bool SupportsAbsoluteCoordinates => true;

    public X11InputSimulator()
    {
        try
        {
            _display = X11Native.XOpenDisplay(display: null);
            if (_display == IntPtr.Zero)
            {
                Log.Warning("[X11InputSimulator] Failed to open X Display");
                return;
            }

            if (X11Native.XTestQueryExtension(_display, out _, out _, out int major, out int minor))
            {
                IsSupported = true;
                Log.Information("[X11InputSimulator] XTest extension available (v{Major}.{Minor})", major, minor);
            }
            else
            {
                Log.Warning("[X11InputSimulator] XTest extension NOT installed on this system. Simulation disabled.");
                IsSupported = false;
            }
        }
        catch (DllNotFoundException dllEx)
        {
            Log.Warning("[X11InputSimulator] XTest library not found (Simulation disabled): {Message}", dllEx.Message);
            IsSupported = false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[X11InputSimulator] Error during initialization");
            IsSupported = false;
        }
    }

    public void Initialize(int screenWidth = 0, int screenHeight = 0) { /* Empty */ }

    public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Initialize(screenWidth, screenHeight);
        return Task.CompletedTask;
    }

    public void MoveAbsolute(int x, int y)
    {
        if (!IsSupported)
        {
            return;
        }

        _ = X11Native.XTestFakeMotionEvent(_display, -1, x, y, 0);
        _ = X11Native.XFlush(_display);
    }

    public void MoveRelative(int dx, int dy)
    {
        if (!IsSupported)
        {
            return;
        }

        try
        {
            int result = X11Native.XTestFakeRelativeMotionEvent(_display, dx, dy, 0);
            if (result is not 0)
            {
                _ = X11Native.XFlush(_display);
                return;
            }
        }
        catch (EntryPointNotFoundException)
        {
            Log.Warning("[X11InputSimulator] XTestFakeRelativeMotionEvent not found, falling back to absolute simulation.");
        }

        var root = X11Native.XDefaultRootWindow(_display);
        if (X11Native.XQueryPointer(_display, root, out _, out _, out int rx, out int ry, out _, out _, out _))
        {
            MoveAbsolute(rx + dx, ry + dy);
        }
    }

    public void MouseButton(int button, bool pressed)
    {
        if (!IsSupported)
        {
            return;
        }

        uint x11Button = 0;
        switch (button)
        {
            case UInputNative.BTN_LEFT: x11Button = 1; break;
            case UInputNative.BTN_RIGHT: x11Button = 3; break;
            case UInputNative.BTN_MIDDLE: x11Button = 2; break;
            case UInputNative.BTN_SIDE: x11Button = 8; break;
            case UInputNative.BTN_EXTRA: x11Button = 9; break;
            default:
                break;
        }

        if (x11Button > 0)
        {
            _ = X11Native.XTestFakeButtonEvent(_display, x11Button, pressed, 0);
            _ = X11Native.XFlush(_display);
        }
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
        if (!IsSupported)
        {
            return;
        }

        uint button;
        if (isHorizontal)
        {
            button = delta > 0 ? 7u : 6u;
        }
        else
        {
            button = delta > 0 ? 4u : 5u;
        }

        _ = X11Native.XTestFakeButtonEvent(_display, button, is_press: true, 0);
        _ = X11Native.XTestFakeButtonEvent(_display, button, is_press: false, 0);
        _ = X11Native.XFlush(_display);
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        if (!IsSupported)
        {
            return;
        }

        uint x11Keycode = (uint)keyCode + 8;
        _ = X11Native.XTestFakeKeyEvent(_display, x11Keycode, pressed, 0);
        _ = X11Native.XFlush(_display);
    }

    public void Sync()
    {
        if (IsSupported)
        {
            _ = X11Native.XFlush(_display);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_display != IntPtr.Zero)
        {
            _ = X11Native.XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
