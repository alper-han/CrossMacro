
namespace CrossMacro.Platform.Linux.Services.Input;

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
                CloseDisplayOnce();
            }
        }
        catch (DllNotFoundException dllEx)
        {
            CloseDisplayOnce();
            Log.Warning("[X11InputSimulator] XTest library not found (Simulation disabled): {Message}", dllEx.Message);
            IsSupported = false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            CloseDisplayOnce();
            Log.LogError(ex, "[X11InputSimulator] Error during initialization");
            IsSupported = false;
        }
    }

    public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public void MoveAbsolute(int x, int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSupported)
        {
            return;
        }

        _ = X11Native.XTestFakeMotionEvent(_display, -1, x, y, 0);
        _ = X11Native.XFlush(_display);
    }

    public void MoveRelative(int dx, int dy)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSupported)
        {
            return;
        }

        if (X11InputCodes.TryToX11Button(button, out var x11Button))
        {
            _ = X11Native.XTestFakeButtonEvent(_display, x11Button, pressed, 0);
            _ = X11Native.XFlush(_display);
        }
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSupported)
        {
            return;
        }

        var button = X11InputCodes.ScrollButton(delta, isHorizontal);

        _ = X11Native.XTestFakeButtonEvent(_display, button, is_press: true, 0);
        _ = X11Native.XTestFakeButtonEvent(_display, button, is_press: false, 0);
        _ = X11Native.XFlush(_display);
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsSupported)
        {
            return;
        }

        var x11Keycode = X11InputCodes.ToX11Keycode(keyCode);
        _ = X11Native.XTestFakeKeyEvent(_display, x11Keycode, pressed, 0);
        _ = X11Native.XFlush(_display);
    }

    public void Sync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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

        _disposed = true;
        CloseDisplayOnce();
    }

    private void CloseDisplayOnce()
    {
        var display = Interlocked.Exchange(ref _display, IntPtr.Zero);
        if (display != IntPtr.Zero)
        {
            _ = X11Native.XCloseDisplay(display);
        }
    }
}
