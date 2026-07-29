
namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Facade/Composite for X11 Input Capture.
/// Manages both Absolute (Clamped) and Relative (Raw) capture strategies.
/// Acts as a single entry point for dependency injection but delegates work to child captures.
/// </summary>
public sealed class X11InputCapture : IInputCapture
{
    private readonly X11AbsoluteCapture _absoluteCapture;
    private readonly X11RelativeCapture _relativeCapture;
    private readonly ISettingsService _settingsService;

    // Track active capturers
    private bool _disposed;

    public string ProviderName => "X11 Facade (Abs/Raw)";

    public bool IsSupported => _absoluteCapture.IsSupported || _relativeCapture.IsSupported;

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    public X11InputCapture(
        X11AbsoluteCapture absoluteCapture,
        X11RelativeCapture relativeCapture,
        ISettingsService settingsService)
    {
        _absoluteCapture = absoluteCapture;
        _relativeCapture = relativeCapture;
        _settingsService = settingsService;

        _absoluteCapture.InputReceived += (s, e) => InputReceived?.Invoke(this, e);
        _absoluteCapture.CaptureError += (s, e) => CaptureError?.Invoke(this, e);

        _relativeCapture.InputReceived += (s, e) => InputReceived?.Invoke(this, e);
        _relativeCapture.CaptureError += (s, e) => CaptureError?.Invoke(this, e);
    }

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        _absoluteCapture.Configure(captureMouse, captureKeyboard);
        _relativeCapture.Configure(captureMouse, captureKeyboard);
    }



    public async Task StartAsync(CancellationToken ct)
    {
        bool useRelative = _settingsService.Current.ForceRelativeCoordinates;

        if (useRelative)
        {
            // Force Relative (Raw) Mode
            // Only start Relative Capture
            await _relativeCapture.StartAsync(ct).ConfigureAwait(false);
        }
        else
        {
            // Absolute (Standard) Mode
            // Only start Absolute Capture
            await _absoluteCapture.StartAsync(ct).ConfigureAwait(false);
        }
    }

    public void StopCapture()
    {
        _absoluteCapture.StopCapture();
        _relativeCapture.StopCapture();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopCapture();
        _absoluteCapture.Dispose();
        _relativeCapture.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
