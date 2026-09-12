
namespace CrossMacro.Platform.Linux.DisplayServer.X11;

/// <summary>
/// Mouse position provider for X11 using XQueryPointer
/// Works across all X11 desktop environments (GNOME, KDE, XFCE, i3, etc.)
/// </summary>
public sealed class X11PositionProvider : IMousePositionProvider
{
    private readonly IX11PositionNativeApi _native;
    private readonly Lock _lock = new();
    private IntPtr _display;
    private bool _disposed;

    public string ProviderName => "X11 (XQueryPointer)";
    public bool IsSupported { get; }

    public X11PositionProvider()
        : this(X11NativeApi.Instance) { /* Empty */ }

    internal X11PositionProvider(IX11PositionNativeApi native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));

        // Attempt to open X display
        _display = _native.OpenDisplay(display: null);

        if (_display == IntPtr.Zero)
        {
            IsSupported = false;
            Log.Warning("[X11PositionProvider] Failed to open X Display - X11 not available");
        }
        else
        {
            IsSupported = true;
            Log.Information("[X11PositionProvider] Successfully connected to X11 display");
        }
    }


    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        lock (_lock)
        {
            if (_disposed || !IsSupported)
            {
                return Task.FromResult<(int X, int Y)?>(null);
            }

            try
            {
                var root = _native.DefaultRootWindow(_display);
                if (root == IntPtr.Zero)
                {
                    Log.Warning("[X11PositionProvider] Failed to resolve X11 root window");
                    return Task.FromResult<(int X, int Y)?>(null);
                }

                bool success = _native.QueryPointer(_display, root, out int rootX, out int rootY);

                if (success)
                {
                    return Task.FromResult<(int X, int Y)?>((rootX, rootY));
                }

                Log.Warning("[X11PositionProvider] XQueryPointer failed");
                return Task.FromResult<(int X, int Y)?>(null);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[X11PositionProvider] Error getting absolute position");
                return Task.FromResult<(int X, int Y)?>(null);
            }
        }
    }

    public Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        lock (_lock)
        {
            if (_disposed || !IsSupported)
            {
                return Task.FromResult<(int Width, int Height)?>(null);
            }

            try
            {
                var screen = _native.DefaultScreen(_display);
                int width = _native.DisplayWidth(_display, screen);
                int height = _native.DisplayHeight(_display, screen);

                if (width > 0 && height > 0)
                {
                    return Task.FromResult<(int Width, int Height)?>((width, height));
                }

                Log.Warning("[X11PositionProvider] Invalid screen dimensions: {Width}x{Height}", width, height);
                return Task.FromResult<(int Width, int Height)?>(null);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[X11PositionProvider] Error getting screen resolution");
                return Task.FromResult<(int Width, int Height)?>(null);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var display = _display;
            _display = IntPtr.Zero;

            if (display != IntPtr.Zero)
            {
                try
                {
                    _ = _native.CloseDisplay(display);
                    Log.Debug("[X11PositionProvider] Closed X11 display connection");
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Warning(ex, "[X11PositionProvider] Error closing X display");
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}
