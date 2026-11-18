
namespace CrossMacro.Platform.Linux.Services.Input;

/// <summary>
/// X11 input capture. Delegates to the absolute (clamped) or relative (raw) capture according to the
/// coordinate mode the recording asks for.
/// </summary>
public sealed class X11InputCapture : IInputCapture, IMouseCoordinateModeInputCapture
{
    private readonly X11AbsoluteCapture _absoluteCapture;
    private readonly X11RelativeCapture _relativeCapture;
    private readonly ISettingsService _settingsService;
    private bool? _useAbsoluteCoordinates;
    private bool? _useLogicalCoordinates;

    // Track active capturers
    private bool _disposed;

    public string ProviderName => "X11 (Absolute/Raw)";

    public bool IsSupported => _absoluteCapture.IsSupported || _relativeCapture.IsSupported;

    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    public X11InputCapture(
        X11AbsoluteCapture absoluteCapture,
        X11RelativeCapture relativeCapture,
        ISettingsService settingsService)
    {
        _absoluteCapture = absoluteCapture ?? throw new ArgumentNullException(nameof(absoluteCapture));
        _relativeCapture = relativeCapture ?? throw new ArgumentNullException(nameof(relativeCapture));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _absoluteCapture.InputReceived += (s, e) => InputReceived?.Invoke(this, e);
        _absoluteCapture.CaptureError += (s, e) => CaptureError?.Invoke(this, e);

        _relativeCapture.InputReceived += (s, e) => InputReceived?.Invoke(this, e);
        _relativeCapture.CaptureError += (s, e) => CaptureError?.Invoke(this, e);
    }

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _absoluteCapture.Configure(captureMouse, captureKeyboard);
        _relativeCapture.Configure(captureMouse, captureKeyboard);
    }

    public void ConfigureCoordinateMode(
        bool useAbsoluteCoordinates,
        bool useLogicalCoordinates)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _useAbsoluteCoordinates = useAbsoluteCoordinates;
        _useLogicalCoordinates = useLogicalCoordinates;
    }



    public async Task StartAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        bool useLogicalCapture = ShouldUseLogicalCapture(
            _useAbsoluteCoordinates,
            _useLogicalCoordinates,
            _settingsService.Current.ForceRelativeCoordinates);

        if (!useLogicalCapture)
        {
            await _relativeCapture.StartAsync(ct).ConfigureAwait(false);
        }
        else
        {
            await _absoluteCapture.StartAsync(ct).ConfigureAwait(false);
        }
    }

    internal static bool ShouldUseLogicalCapture(
        bool? useAbsoluteCoordinates,
        bool? useLogicalCoordinates,
        bool legacyForceRelativeSetting)
    {
        if (useAbsoluteCoordinates is not null)
        {
            return useAbsoluteCoordinates.Value || useLogicalCoordinates is true;
        }

        return !legacyForceRelativeSetting;
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

        _disposed = true;
        StopCapture();
        _absoluteCapture.Dispose();
        _relativeCapture.Dispose();
    }
}
