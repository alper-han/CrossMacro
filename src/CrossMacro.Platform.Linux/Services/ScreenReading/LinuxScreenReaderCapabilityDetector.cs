
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class LinuxScreenReaderCapabilityDetector : ILinuxScreenReaderCapabilityDetector, IDisposable
{
    private readonly LinuxScreenBackendRegistry _backends;
    private readonly IGnomeScreenReadingReadiness _readiness;
    private readonly bool _ownsReadiness;
    private readonly Lock _readinessLock = new();
    private readonly Lock _snapshotLock = new();
    private readonly CancellationTokenSource _disposeCancellation = new();

    private Task? _readinessTask;
    private LinuxScreenReaderCapabilitySnapshot? _snapshot;
    private Task<LinuxScreenReaderCapabilitySnapshot>? _snapshotTask;
    private CancellationTokenSource? _snapshotCancellation;
    private long _snapshotGeneration;
    private int _disposeState;

    private static readonly TimeSpan GnomeInitializationTimeout = TimeSpan.FromSeconds(5);

    internal LinuxScreenReaderCapabilityDetector(GnomePositionProvider gnomePositionProvider)
        : this(
            WaylandExtImageCopySupportProbe.Instance,
            new WlrScreencopyCapture(),
            PortalScreenCastSupportProbe.Instance,
            new KWinScreenShotCapture(),
            gnomePositionProvider)
    { /* Empty */ }

    internal LinuxScreenReaderCapabilityDetector(
        IExtImageCopySupportProbe extImageCopyProbe,
        GnomePositionProvider gnomePositionProvider)
        : this(
            extImageCopyProbe,
            new WlrScreencopyCapture(),
            PortalScreenCastSupportProbe.Instance,
            new KWinScreenShotCapture(),
            gnomePositionProvider)
    { /* Empty */ }

    internal LinuxScreenReaderCapabilityDetector(
        IExtImageCopySupportProbe extImageCopyProbe,
        IWlrScreencopySupportProbe wlrScreencopyProbe,
        IPortalScreenCastSupportProbe portalScreenCastProbe,
        IKWinScreenShotSupportProbe kWinScreenShotProbe)
        : this(extImageCopyProbe, wlrScreencopyProbe, portalScreenCastProbe, kWinScreenShotProbe, new GnomePositionProvider()) { /* Empty */ }

    public LinuxScreenReaderCapabilityDetector(
        IExtImageCopySupportProbe extImageCopyProbe,
        IWlrScreencopySupportProbe wlrScreencopyProbe,
        IPortalScreenCastSupportProbe portalScreenCastProbe,
        IKWinScreenShotSupportProbe kWinScreenShotProbe,
        GnomePositionProvider gnomePositionProvider)
    {
        ArgumentNullException.ThrowIfNull(extImageCopyProbe);
        ArgumentNullException.ThrowIfNull(wlrScreencopyProbe);
        ArgumentNullException.ThrowIfNull(portalScreenCastProbe);
        ArgumentNullException.ThrowIfNull(kWinScreenShotProbe);
        _readiness = new GnomeScreenReadingReadiness(gnomePositionProvider);
        _ownsReadiness = true;
        _backends = new LinuxScreenBackendRegistry([
            LinuxScreenBackendDescriptors.Ext(extImageCopyProbe),
            LinuxScreenBackendDescriptors.Wlr(wlrScreencopyProbe),
            LinuxScreenBackendDescriptors.Portal(portalScreenCastProbe),
            LinuxScreenBackendDescriptors.KWin(kWinScreenShotProbe),
            LinuxScreenBackendDescriptors.Gnome(_readiness)]);
        _readiness.Changed += OnGnomeExtensionStatusUpdated;
    }

    internal LinuxScreenReaderCapabilityDetector(LinuxScreenBackendRegistry backends, IGnomeScreenReadingReadiness readiness)
    {
        _backends = backends ?? throw new ArgumentNullException(nameof(backends));
        _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
        _readiness.Changed += OnGnomeExtensionStatusUpdated;
    }

    public bool IsGnomeSession => _readiness.IsSession;

    public bool IsReady
    {
        get
        {
            lock (_snapshotLock)
            {
                return _snapshot is not null;
            }
        }
    }

    /// <summary>
    /// Reads only the last acquired snapshot. Capability acquisition belongs to
    /// <see cref="EnsureReadyAsync"/> so synchronous diagnostics cannot perform
    /// external I/O on a UI or startup path.
    /// </summary>
    public LinuxScreenReaderCapabilitySnapshot GetSnapshot()
    {
        lock (_snapshotLock)
        {
            return _snapshot ?? LinuxScreenReaderCapabilitySnapshot.Initializing();
        }
    }

    public Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        Task<LinuxScreenReaderCapabilitySnapshot> snapshotTask;
        lock (_snapshotLock)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) is not 0, this);
            snapshotTask = _snapshot is { } snapshot
                ? Task.FromResult(snapshot)
                : _snapshotTask ??= CreateSnapshotTaskLockedAsync();
        }

        return snapshotTask.WaitAsync(cancellationToken);
    }

    public void InvalidateCache()
    {
        CancellationTokenSource? cancellation;
        lock (_snapshotLock)
        {
            _snapshotGeneration++;
            _snapshot = null;
            _snapshotTask = null;
            cancellation = _snapshotCancellation;
            _snapshotCancellation = null;
        }

        if (cancellation is not null)
        {
            _ = cancellation.CancelAsync();
        }
    }

    private Task<LinuxScreenReaderCapabilitySnapshot> CreateSnapshotTaskLockedAsync()
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellation.Token);
        _snapshotCancellation = cancellation;
        return ProbeSnapshotAsync(_snapshotGeneration, cancellation);
    }

    private async Task<LinuxScreenReaderCapabilitySnapshot> ProbeSnapshotAsync(
        long generation,
        CancellationTokenSource cancellation)
    {
        try
        {
            await EnsureGnomeReadinessAsync(cancellation.Token).ConfigureAwait(false);
            var snapshot = await _backends.ProbeSnapshotAsync(cancellation.Token).ConfigureAwait(false);
            lock (_snapshotLock)
            {
                if (_snapshotGeneration == generation && !cancellation.IsCancellationRequested)
                {
                    _snapshot = snapshot;
                }
            }

            return snapshot;
        }
        finally
        {
            lock (_snapshotLock)
            {
                if (_snapshotGeneration == generation && ReferenceEquals(_snapshotCancellation, cancellation))
                {
                    _snapshotCancellation = null;
                    _snapshotTask = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private Task EnsureGnomeReadinessAsync(CancellationToken cancellationToken)
    {
        if (!IsGnomeSession)
        {
            return Task.CompletedTask;
        }

        Task readinessTask;
        lock (_readinessLock)
        {
            _readinessTask ??= WaitForGnomeInitializationAsync();
            readinessTask = _readinessTask;
        }

        return readinessTask.WaitAsync(cancellationToken);
    }

    private async Task WaitForGnomeInitializationAsync()
    {
        try
        {
            await _readiness.Initialization
                .WaitAsync(GnomeInitializationTimeout, TimeProvider.System, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Log.Warning(
                "[LinuxScreenReaderCapabilityDetector] GNOME extension initialization did not complete within {TimeoutSeconds}s; using the configured fallback order",
                GnomeInitializationTimeout.TotalSeconds);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[LinuxScreenReaderCapabilityDetector] GNOME extension readiness check failed; using the configured fallback order");
        }
    }

    private void OnGnomeExtensionStatusUpdated(object? sender, EventArgs args)
    {
        lock (_snapshotLock)
        {
            // A discovery already waiting for GNOME will observe the new status
            // when it probes the final backend. Do not cancel that useful work.
            if (_snapshotTask is { IsCompleted: false })
            {
                return;
            }
        }

        InvalidateCache();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) is not 0)
        {
            return;
        }

        _readiness.Changed -= OnGnomeExtensionStatusUpdated;
        _disposeCancellation.Cancel();
        InvalidateCache();
        _disposeCancellation.Dispose();
        if (_ownsReadiness && _readiness is IDisposable disposable) { disposable.Dispose(); }
    }
}
