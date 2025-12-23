using System;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Native.X11;
using Serilog;

namespace CrossMacro.Infrastructure.Wayland
{
    /// <summary>
    /// Mouse position provider for X11 using XQueryPointer
    /// Works across all X11 desktop environments (GNOME, KDE, XFCE, i3, etc.)
    /// </summary>
    public class X11PositionProvider : IMousePositionProvider
    {
        private IntPtr _display;
        private int _screen;
        private bool _disposed;

        public string ProviderName => "X11 (XQueryPointer)";
        public bool IsSupported { get; }

        public X11PositionProvider()
        {
            // Attempt to open X display
            _display = X11Native.XOpenDisplay(null);
            
            if (_display == IntPtr.Zero)
            {
                IsSupported = false;
                Log.Warning("[X11PositionProvider] Failed to open X Display - X11 not available");
            }
            else
            {
                IsSupported = true;
                _screen = X11Native.XDefaultScreen(_display);
                Log.Information("[X11PositionProvider] Successfully connected to X11 display");
            }
        }

        /// <summary>
        /// Sets the absolute cursor position using XWarpPointer (X11-specific)
        /// This is needed because uinput absolute positioning doesn't move the cursor in X11
        /// </summary>
        public void SetAbsolutePositionAsync(int x, int y)
        {
            if (_disposed || !IsSupported)
                return;

            try
            {
                var rootWindow = X11Native.XRootWindow(_display, _screen);
                
                X11Native.XWarpPointer(
                    _display,
                    IntPtr.Zero,      // src_w: no source window
                    rootWindow,       // dest_w: root window
                    0, 0,             // src_x, src_y
                    0, 0,             // src_width, src_height
                    x, y);            // dest_x, dest_y
                
                X11Native.XFlush(_display);  // Critical: ensure command is sent immediately
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[X11PositionProvider] Error setting absolute position to ({X}, {Y})", x, y);
            }
        }


        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            if (_disposed || !IsSupported)
                return Task.FromResult<(int X, int Y)?>(null);

            try
            {
                var root = X11Native.XDefaultRootWindow(_display);
                
                bool success = X11Native.XQueryPointer(
                    _display,
                    root,
                    out _,
                    out _,
                    out int rootX,
                    out int rootY,
                    out _,
                    out _,
                    out _);

                if (success)
                {
                    return Task.FromResult<(int X, int Y)?>((rootX, rootY));
                }
                else
                {
                    Log.Warning("[X11PositionProvider] XQueryPointer failed");
                    return Task.FromResult<(int X, int Y)?>(null);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[X11PositionProvider] Error getting absolute position");
                return Task.FromResult<(int X, int Y)?>(null);
            }
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync()
        {
            if (_disposed || !IsSupported)
                return Task.FromResult<(int Width, int Height)?>(null);

            try
            {
                var screen = X11Native.XDefaultScreen(_display);
                int width = X11Native.XDisplayWidth(_display, screen);
                int height = X11Native.XDisplayHeight(_display, screen);

                if (width > 0 && height > 0)
                {
                    return Task.FromResult<(int Width, int Height)?>((width, height));
                }
                else
                {
                    Log.Warning("[X11PositionProvider] Invalid screen dimensions: {Width}x{Height}", width, height);
                    return Task.FromResult<(int Width, int Height)?>(null);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[X11PositionProvider] Error getting screen resolution");
                return Task.FromResult<(int Width, int Height)?>(null);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_display != IntPtr.Zero)
            {
                try
                {
                    X11Native.XCloseDisplay(_display);
                    Log.Debug("[X11PositionProvider] Closed X11 display connection");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[X11PositionProvider] Error closing X display");
                }
                finally
                {
                    _display = IntPtr.Zero;
                }
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
