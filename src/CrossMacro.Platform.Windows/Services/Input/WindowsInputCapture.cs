namespace CrossMacro.Platform.Windows.Services.Input;

/// <summary>
/// Owns capture admission and configuration. Every native thread receives a new
/// session, and a stopped session must finish before another one can start.
/// </summary>
public sealed class WindowsInputCapture : IInputCapture, IMouseCoordinateModeInputCapture, IAsyncDisposable
{
    private readonly Lock _stateLock = new();
    private readonly Func<IWindowsCaptureSession> _sessionFactory;
    private IWindowsCaptureSession? _session;
    private Task? _startup;
    private bool _stopping;
    private bool _disposed;
    private bool _captureMouse;
    private bool _captureKeyboard;
    private bool _useAbsoluteCoordinates;
    private bool _useLogicalCoordinates;

    public WindowsInputCapture() : this(new DefaultWindowsHookInstaller()) { }

    internal WindowsInputCapture(IWindowsHookInstaller hookInstaller)
        : this(() => new WindowsCaptureSession(hookInstaller))
    {
        ArgumentNullException.ThrowIfNull(hookInstaller);
    }

    internal WindowsInputCapture(Func<IWindowsCaptureSession> sessionFactory)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    public string ProviderName => "Windows Hooks";
    public bool IsSupported => OperatingSystem.IsWindows();
    public event EventHandler<CapturedInputEventArgs>? InputReceived;
    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        lock (_stateLock)
        {
            _captureMouse = captureMouse;
            _captureKeyboard = captureKeyboard;
        }
    }

    public void ConfigureCoordinateMode(bool useAbsoluteCoordinates, bool useLogicalCoordinates)
    {
        lock (_stateLock)
        {
            _useAbsoluteCoordinates = useAbsoluteCoordinates;
            _useLogicalCoordinates = useLogicalCoordinates;
        }
    }

    public async Task StartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        while (true)
        {
            Task startup;
            Task? stopping = null;
            lock (_stateLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                RetireCompletedSession();
                if (_session is not null && (_stopping || _session.IsStopRequested))
                {
                    stopping = _session.Completion;
                    startup = Task.CompletedTask;
                }
                else
                {
                    if (_session is null)
                    {
                        var session = _sessionFactory();
                        session.Configure(_captureMouse, _captureKeyboard);
                        session.ConfigureCoordinateMode(_useAbsoluteCoordinates, _useLogicalCoordinates);
                        session.InputReceived += ForwardInput;
                        session.CaptureError += ForwardError;
                        _session = session;
                        _startup = session.StartAsync(ct);
                    }
                    startup = _startup ?? Task.CompletedTask;
                }
            }
            if (stopping is not null)
            {
                await stopping.WaitAsync(ct).ConfigureAwait(false);
                continue;
            }
            await startup.WaitAsync(ct).ConfigureAwait(false);
            return;
        }
    }

    public void StopCapture()
    {
        IWindowsCaptureSession? session;
        lock (_stateLock)
        {
            session = _session;
            _stopping = session is not null;
        }
        session?.StopCapture();
    }

    private void RetireCompletedSession()
    {
        if (_session is not null && _session.Completion.IsCompleted)
        {
            _session.InputReceived -= ForwardInput;
            _session.CaptureError -= ForwardError;
            _session.Dispose();
            _session = null;
            _startup = null;
            _stopping = false;
        }
    }

    private void ForwardInput(object? sender, CapturedInputEventArgs args)
    {
        if (!Volatile.Read(ref _disposed))
        {
            InputReceived?.Invoke(this, args);
        }
    }

    private void ForwardError(object? sender, InputCaptureErrorEventArgs args)
    {
        if (!Volatile.Read(ref _disposed))
        {
            CaptureError?.Invoke(this, args);
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed) { return; }
            _disposed = true;
        }
        StopCapture();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        Task completion;
        lock (_stateLock)
        {
            completion = _session?.Completion ?? Task.CompletedTask;
        }
        await completion.ConfigureAwait(false);
        lock (_stateLock)
        {
            RetireCompletedSession();
        }
    }

    internal static bool TryMapMouseButtonOrScroll(uint msg, uint mouseData, out ushort evdevCode, out int value, out ushort type) =>
        WindowsCaptureSession.TryMapMouseButtonOrScroll(msg, mouseData, out evdevCode, out value, out type);

    internal static (ushort XCode, int XValue, ushort YCode, int YValue) ResolveMouseMovement(
        bool useAbsoluteCoordinates,
        int currentX,
        int currentY,
        int previousX,
        int previousY) =>
        WindowsCaptureSession.ResolveMouseMovement(useAbsoluteCoordinates, currentX, currentY, previousX, previousY);

    internal static bool TryResolveRawRelativeMovement(
        ushort flags,
        int deltaX,
        int deltaY,
        out int rawDeltaX,
        out int rawDeltaY) =>
        WindowsCaptureSession.TryResolveRawRelativeMovement(flags, deltaX, deltaY, out rawDeltaX, out rawDeltaY);

    internal static int MapKeyboardEvent(ushort virtualKey, uint hookFlags) =>
        WindowsCaptureSession.MapKeyboardEvent(virtualKey, hookFlags);

    internal static bool ShouldIgnoreKeyboardHookEvent(uint hookFlags, IntPtr extraInfo) =>
        WindowsCaptureSession.ShouldIgnoreKeyboardHookEvent(hookFlags, extraInfo);

    internal static bool IsSessionRecoveryMessage(uint message, IntPtr wParam) =>
        WindowsCaptureSession.IsSessionRecoveryMessage(message, wParam);

    internal static long GetMonotonicTimestampMicroseconds() =>
        WindowsCaptureSession.GetMonotonicTimestampMicroseconds();

    internal static long ToMicroseconds(long timestamp, long frequency) =>
        WindowsCaptureSession.ToMicroseconds(timestamp, frequency);
}
