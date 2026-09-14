namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxScreenBackendRegistryTests
{
    [Fact]
    public void Registry_ProbesWithoutConstructingProviders_AndConstructsOnlyRequestedBackend()
    {
        var probes = new List<LinuxScreenReaderBackend>();
        var creations = new List<LinuxScreenReaderBackend>();
        var registry = new LinuxScreenBackendRegistry(Enum.GetValues<LinuxScreenReaderBackend>().Select(backend =>
            new LinuxScreenBackendDescriptor(backend,
                () => { probes.Add(backend); return LinuxScreenReaderBackendCapability.Available(backend); },
                _ => { creations.Add(backend); return new UnavailableLinuxScreenFrameProvider(ScreenReadErrorKind.Unsupported, "fake"); })));

        var snapshot = registry.ProbeSnapshot();
        Assert.Equal(5, probes.Count);
        Assert.Empty(creations);
        using var provider = registry.CreateProvider(snapshot.Portal);
        Assert.Equal(new[] { LinuxScreenReaderBackend.Portal }, creations);
    }

    [Fact]
    public async Task Detector_GetSnapshotIsCacheOnly_AndEnsureReadyAsyncPerformsAsynchronousDiscovery()
    {
        var firstProbeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstProbe = new TaskCompletionSource<LinuxScreenReaderBackendCapability>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new LinuxScreenBackendRegistry(Enum.GetValues<LinuxScreenReaderBackend>().Select(backend =>
            new LinuxScreenBackendDescriptor(
                backend,
                () => throw new InvalidOperationException("Synchronous probing must not run."),
                _ => throw new InvalidOperationException("Probe must not create a provider."),
                async cancellationToken =>
                {
                    if (backend is LinuxScreenReaderBackend.ExtImageCopy)
                    {
                        firstProbeStarted.TrySetResult();
                        return await releaseFirstProbe.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    return LinuxScreenReaderBackendCapability.Available(backend);
                })));
        using var detector = new LinuxScreenReaderCapabilityDetector(registry, new FakeReadiness(isSession: false));

        var initial = detector.GetSnapshot();

        Assert.False(detector.IsReady);
        Assert.False(initial.ExtImageCopy.IsAvailable);
        Assert.False(firstProbeStarted.Task.IsCompleted);

        var discovery = detector.EnsureReadyAsync(CancellationToken.None);
        await firstProbeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
        Assert.False(discovery.IsCompleted);

        releaseFirstProbe.TrySetResult(LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy));
        await discovery;
        var completed = detector.GetSnapshot();

        Assert.True(detector.IsReady);
        Assert.True(completed.ExtImageCopy.IsAvailable);
        Assert.True(detector.GetSnapshot().ExtImageCopy.IsAvailable);
    }

    [Fact]
    public async Task Detector_CallerCancellationDoesNotDiscardTheSharedCapabilityDiscovery()
    {
        var firstProbeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstProbe = new TaskCompletionSource<LinuxScreenReaderBackendCapability>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new LinuxScreenBackendRegistry(Enum.GetValues<LinuxScreenReaderBackend>().Select(backend =>
            new LinuxScreenBackendDescriptor(
                backend,
                () => throw new InvalidOperationException("Synchronous probing must not run."),
                _ => throw new InvalidOperationException("Probe must not create a provider."),
                async cancellationToken =>
                {
                    if (backend is LinuxScreenReaderBackend.ExtImageCopy)
                    {
                        firstProbeStarted.TrySetResult();
                        return await releaseFirstProbe.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    return LinuxScreenReaderBackendCapability.Available(backend);
                })));
        using var detector = new LinuxScreenReaderCapabilityDetector(registry, new FakeReadiness(isSession: false));
        using var cancellation = new CancellationTokenSource();

        var canceledWait = detector.EnsureReadyAsync(cancellation.Token);
        await firstProbeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledWait);

        releaseFirstProbe.TrySetResult(LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy));
        await detector.EnsureReadyAsync(CancellationToken.None);
        var completed = detector.GetSnapshot();

        Assert.True(detector.IsReady);
        Assert.True(completed.ExtImageCopy.IsAvailable);
    }

    [Fact]
    public async Task Detector_ReadinessRefreshesRegistry_AndDisposalRemovesSubscription()
    {
        var readiness = new FakeReadiness();
        var registry = new LinuxScreenBackendRegistry(Enum.GetValues<LinuxScreenReaderBackend>().Select(backend =>
            new LinuxScreenBackendDescriptor(backend,
                () => readiness.IsAvailable ? LinuxScreenReaderBackendCapability.Available(backend)
                    : LinuxScreenReaderBackendCapability.Unavailable(backend, ScreenReadErrorKind.BackendUnavailable, "initializing"),
                _ => throw new InvalidOperationException("Probe must not create a provider."))));
        var detector = new LinuxScreenReaderCapabilityDetector(registry, readiness);
        Assert.False(detector.GetSnapshot().GnomeExtension.IsAvailable);
        var waiting = detector.EnsureReadyAsync(CancellationToken.None);
        Assert.False(waiting.IsCompleted);
        readiness.Complete();
        await waiting;
        Assert.True(detector.GetSnapshot().GnomeExtension.IsAvailable);
        Assert.Equal(1, readiness.Subscribers);
        detector.Dispose();
        Assert.Equal(0, readiness.Subscribers);
    }

    private sealed class FakeReadiness(bool isSession = true) : IGnomeScreenReadingReadiness
    {
        private readonly TaskCompletionSource _initialization = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private EventHandler? _changed;
        public bool IsSession { get; } = isSession;
        public bool IsAvailable { get; private set; }
        public Task Initialization => _initialization.Task;
        public int Subscribers { get; private set; }
        public event EventHandler? Changed
        {
            add { _changed += value; Subscribers++; }
            remove { _changed -= value; Subscribers--; }
        }
        public void Complete()
        {
            IsAvailable = true;
            _changed?.Invoke(this, EventArgs.Empty);
            _initialization.SetResult();
        }
    }
}
