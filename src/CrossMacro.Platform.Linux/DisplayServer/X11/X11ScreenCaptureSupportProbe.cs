using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.Services;

namespace CrossMacro.Platform.Linux.DisplayServer.X11;

public sealed class X11ScreenCaptureSupportProbe : IX11ScreenCaptureSupportProbe
{
    private const string DisplayEnvironmentVariable = "DISPLAY";

    public static X11ScreenCaptureSupportProbe Instance { get; } = new(X11NativeApi.Instance, Environment.GetEnvironmentVariable);

    private readonly IX11NativeApi _native;
    private readonly Func<string, string?> _getEnvironmentVariable;

    internal X11ScreenCaptureSupportProbe(IX11NativeApi native, Func<string, string?> getEnvironmentVariable)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
    }

    internal X11ScreenCaptureSupportProbe(IX11NativeApi native, LinuxEnvironmentSnapshot environment)
        : this(native, name => string.Equals(name, DisplayEnvironmentVariable, StringComparison.Ordinal) ? environment.Display : null)
    {
    }

    public X11ScreenCaptureSupportResult ProbeSupport()
    {
        if (string.IsNullOrWhiteSpace(_getEnvironmentVariable(DisplayEnvironmentVariable)))
        {
            return X11ScreenCaptureSupportResult.Unsupported("DISPLAY is not set; X11 screen reading requires a native X11 session.");
        }

        try
        {
            var display = _native.OpenDisplay(display: null);
            if (display == IntPtr.Zero)
            {
                return X11ScreenCaptureSupportResult.Unsupported("Failed to open the X11 display for screen reading.");
            }

            try
            {
                var root = _native.DefaultRootWindow(display);
                var status = _native.GetGeometry(display, root, out _, out _, out _, out var width, out var height, out _, out _);
                return status is not 0 && width > 0 && height > 0
                    ? X11ScreenCaptureSupportResult.Supported()
                    : X11ScreenCaptureSupportResult.Unsupported("Failed to read X11 root window geometry for screen reading.");
            }
            finally
            {
                _native.CloseDisplay(display);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return X11ScreenCaptureSupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
    }
}
