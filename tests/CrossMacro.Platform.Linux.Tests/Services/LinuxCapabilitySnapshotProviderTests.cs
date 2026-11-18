using static CrossMacro.Platform.Linux.Tests.Services.InputSnapshotTestFactory;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxCapabilitySnapshotProviderTests
{
    private static LinuxEnvironmentVariables Wayland() =>
        new(name => name is "XDG_SESSION_TYPE" ? "wayland" : null);

    [LinuxFact]
    public void InputFactories_WhenDaemonSelected_DoNotReprobeDirectDevices()
    {
        var directProbes = 0;
        using var input = Create(
            fileExists: _ => true,
            hasReadableInputDevices: () =>
            {
                directProbes++;
                return false;
            });
        var provider = new LinuxCapabilitySnapshotProvider(
            Wayland(), input, Substitute.For<ILinuxScreenReaderCapabilityDetector>());
        Assert.Equal(InputProviderMode.Daemon, provider.GetSnapshot().Input.ResolvedMode);
        Assert.Equal(1, directProbes);
        using var ipc = new IpcClient(() => "/fixture/missing.sock");
        using var capture = new LinuxIpcInputCapture(ipc, "fixture capture");
        using var simulator = new LinuxIpcInputSimulator(ipc);
        var captureFactory = new LinuxCaptureFactory(provider,
            () => throw new InvalidOperationException("Direct capture must not be used"),
            () => capture, () => throw new InvalidOperationException("Not X11"));
        var simulatorFactory = new LinuxSimulatorFactory(provider,
            () => throw new InvalidOperationException("Direct simulator must not be used"),
            () => simulator, () => throw new InvalidOperationException("Not X11"));

        Assert.Same(capture, captureFactory.Create());
        Assert.Same(simulator, simulatorFactory.Create());
        Assert.Equal(1, directProbes);
    }

    [Fact]
    public async Task InvalidateDirectInputCache_PreservesScreenReadinessAndDaemonGraceHistory()
    {
        var now = Epoch;
        var socketExists = true;
        var writable = true;
        var handshakes = 0;
        using var input = Create(
            fileExists: _ => socketExists,
            canOpenForWrite: _ => writable,
            hasReadableInputDevices: () => true,
            handshake: Handshake((path, timeout) =>
            {
                handshakes++;
                return Success(path, timeout);
            }),
            utcNow: () => now);
        var extProbe = new MutableExtImageCopyProbe(WaylandSupportResult.Supported());
        using var screen = new LinuxScreenReaderCapabilityDetector(
            extProbe, new FixedWlrProbe(), new FixedPortalProbe(), new FixedKWinProbe());
        var provider = new LinuxCapabilitySnapshotProvider(Wayland(), input, screen);
        await screen.EnsureReadyAsync(TestContext.Current.CancellationToken);
        var initial = provider.GetSnapshot();
        Assert.Equal(InputProviderMode.Daemon, initial.Input.ResolvedMode);
        Assert.True(initial.ScreenReading.ExtImageCopy.IsAvailable);

        socketExists = false;
        now = now.AddSeconds(6);
        Assert.Equal(InputProviderMode.Legacy, provider.GetSnapshot().Input.ResolvedMode);
        writable = false;
        provider.InvalidateDirectInputCache();
        var refreshed = provider.GetSnapshot();

        Assert.False(refreshed.Input.CanUseDirectUInput);
        Assert.Equal(InputProviderMode.Daemon, refreshed.Input.ResolvedMode);
        Assert.True(refreshed.ScreenReading.ExtImageCopy.IsAvailable);
        Assert.Equal(1, extProbe.CallCount);
        Assert.Equal(1, handshakes);
    }

    [Fact]
    public void GetSnapshot_AfterInputProbeTtl_ObservesRecoveredDaemonWithoutExplicitInvalidation()
    {
        var now = Epoch;
        var socketExists = false;
        using var input = Create(fileExists: _ => socketExists, utcNow: () => now);
        var provider = new LinuxCapabilitySnapshotProvider(
            Substitute.For<ILinuxEnvironmentVariables>(), input,
            Substitute.For<ILinuxScreenReaderCapabilityDetector>());

        Assert.False(provider.GetSnapshot().Input.DaemonHandshakeSucceeded);
        socketExists = true;
        now = now.AddSeconds(6);

        Assert.True(provider.GetSnapshot().Input.DaemonHandshakeSucceeded);
    }

    [Fact]
    public async Task InputSnapshotAsync_PassesTheCallersTokenToTheHandshake()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var provider = Create(
            fileExists: _ => true,
            handshake: async (_, _, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, token);
                return LinuxDaemonHandshakeProbeResult.Success(IpcProtocol.DefaultSocketPath, TimeSpan.FromSeconds(5));
            });
        using var cancellation = new CancellationTokenSource();
        var capture = provider.GetSnapshotAsync(cancellation.Token).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None));
    }

    [Fact]
    public async Task InvalidateScreenReadingCache_DoesNotReprobeInputDaemon()
    {
        var daemonProbeCount = 0;
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_SESSION_TYPE"] = "wayland",
            ["WAYLAND_DISPLAY"] = "wayland-0",
            ["DISPLAY"] = null,
        };
        var screenDetector = new LinuxScreenReaderCapabilityDetector(
            new FixedExtImageCopyProbe(),
            new FixedWlrProbe(),
            new FixedPortalProbe(),
            new FixedKWinProbe());
        using var input = Create(
            fileExists: _ => true,
            handshake: Handshake((path, timeout) =>
            {
                daemonProbeCount++;
                return Failure(path, timeout);
            }));
        var provider = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => environment.TryGetValue(name, out var value) ? value : null),
            input,
            screenDetector);

        await screenDetector.EnsureReadyAsync(CancellationToken.None);
        _ = provider.GetSnapshot();
        provider.InvalidateScreenReadingCache();
        _ = provider.GetSnapshot();

        Assert.Equal(1, daemonProbeCount);
    }

    [Fact]
    public async Task InvalidateCache_ReprobesInputAndScreenCapabilities()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_SESSION_TYPE"] = "wayland",
            ["WAYLAND_DISPLAY"] = "wayland-0",
            ["DISPLAY"] = null,
        };
        var extProbe = new MutableExtImageCopyProbe(WaylandSupportResult.Unsupported("initial"));
        var screenDetector = new LinuxScreenReaderCapabilityDetector(
            extProbe,
            new FixedWlrProbe(),
            new FixedPortalProbe(),
            new FixedKWinProbe());
        var readableInputProbeCount = 0;
        using var input = Create(hasReadableInputDevices: () =>
        {
            readableInputProbeCount++;
            return false;
        });
        var provider = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => environment.TryGetValue(name, out var value) ? value : null),
            input,
            screenDetector);

        await screenDetector.EnsureReadyAsync(CancellationToken.None);
        _ = provider.GetSnapshot();
        provider.InvalidateCache();
        await screenDetector.EnsureReadyAsync(CancellationToken.None);
        _ = provider.GetSnapshot();

        Assert.Equal(2, readableInputProbeCount);
        Assert.Equal(2, extProbe.CallCount);
    }

    [Fact]
    public void InputDeviceAccessProbe_UsesInjectedSyncDelegate()
    {
        var probe = new LinuxInputDeviceAccessProbe(static () => false);

        Assert.False(probe.HasUsableReadableInputDevices());
    }

    [Fact]
    public async Task InputDeviceAccessProbeAsync_UsesInjectedDelegateAndToken()
    {
        using var cancellation = new CancellationTokenSource();
        var expectedToken = cancellation.Token;
        var observedToken = default(CancellationToken);
        var probe = new LinuxInputDeviceAccessProbe(
            hasUsableReadableInputDevices: static () => false,
            hasUsableReadableInputDevicesAsync: token =>
            {
                observedToken = token;
                return ValueTask.FromResult(true);
            });

        Assert.True(await probe.HasUsableReadableInputDevicesAsync(expectedToken));
        Assert.Equal(expectedToken, observedToken);
    }

    [Fact]
    public async Task InvalidateCache_WhenSessionChangesToX11_SkipsWaylandScreenProbes()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_SESSION_TYPE"] = "wayland",
            ["WAYLAND_DISPLAY"] = "wayland-0",
            ["DISPLAY"] = null,
        };
        var extProbe = new MutableExtImageCopyProbe(WaylandSupportResult.Unsupported("initial"));
        var screenDetector = new LinuxScreenReaderCapabilityDetector(
            extProbe,
            new FixedWlrProbe(),
            new FixedPortalProbe(),
            new FixedKWinProbe());
        using var input = Create();
        var provider = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => environment.TryGetValue(name, out var value) ? value : null),
            input,
            screenDetector);

        await screenDetector.EnsureReadyAsync(CancellationToken.None);
        Assert.Equal(CompositorType.Other, provider.GetSnapshot().Compositor);
        Assert.Equal(1, extProbe.CallCount);
        extProbe.Result = WaylandSupportResult.Supported();
        environment["XDG_SESSION_TYPE"] = "x11";
        environment["WAYLAND_DISPLAY"] = null;
        environment["DISPLAY"] = ":0";

        provider.InvalidateCache();

        var refreshed = provider.GetSnapshot();
        Assert.Equal(CompositorType.X11, refreshed.Compositor);
        Assert.False(refreshed.ScreenReading.ExtImageCopy.IsAvailable);
        Assert.Equal(ScreenReadErrorKind.Unsupported, refreshed.ScreenReading.ExtImageCopy.ErrorKind);
        Assert.Equal(1, extProbe.CallCount);
    }

    private sealed class MutableExtImageCopyProbe(WaylandSupportResult result) : IExtImageCopySupportProbe
    {
        public WaylandSupportResult Result { get; set; } = result;
        public int CallCount { get; private set; }

        public WaylandSupportResult ProbeSupport()
        {
            CallCount++;
            return Result;
        }
    }

    private sealed class FixedExtImageCopyProbe : IExtImageCopySupportProbe
    {
        public WaylandSupportResult ProbeSupport() => WaylandSupportResult.Unsupported("ext-image-copy");
    }

    private sealed class FixedWlrProbe : IWlrScreencopySupportProbe
    {
        public WaylandSupportResult ProbeSupport() => WaylandSupportResult.Unsupported("wlr");
    }

    private sealed class FixedPortalProbe : IPortalScreenCastSupportProbe
    {
        public WaylandSupportResult ProbeSupport() => WaylandSupportResult.Unsupported("portal");
    }

    private sealed class FixedKWinProbe : IKWinScreenShotSupportProbe
    {
        public WaylandSupportResult ProbeSupport() => WaylandSupportResult.Unsupported("kwin");
    }
}
