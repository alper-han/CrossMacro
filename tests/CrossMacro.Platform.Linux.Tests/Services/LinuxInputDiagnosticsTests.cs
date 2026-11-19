using CrossMacro.Platform.Linux.Native;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxInputDiagnosticsTests
{
    [Fact]
    public async Task GetSnapshotAsync_WithoutDaemonProbe_ReportsDiagnosticsDisabledWithoutProbingTheDaemon()
    {
        var handshakes = 0;
        using var provider = InputSnapshotTestFactory.Create(
            fileExists: InputSnapshotTestFactory.IsDaemonSocket,
            handshake: InputSnapshotTestFactory.Handshake((path, timeout) =>
            {
                handshakes++;
                return InputSnapshotTestFactory.Success(path, timeout);
            }));
        var diagnostics = Create(provider, socketAccessProbe: null);

        var snapshot = await diagnostics.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.False(snapshot.DaemonDiagnosticsEnabled);
        Assert.Null(snapshot.DaemonSocketAccess);
        Assert.Equal(LinuxDaemonHandshakeStatus.MissingSocket, snapshot.DaemonHandshake.Status);
        Assert.False(snapshot.CanUseDaemon);
        Assert.Equal(0, handshakes);
    }

    [Fact]
    public async Task GetSnapshotAsync_WithDaemonProbe_ProbesTheDefaultSocketForTheRequiredGroupAndHandshakesWithATwoSecondBudget()
    {
        LinuxDaemonSocketProbeOptions? probed = null;
        string? handshakePath = null;
        TimeSpan? handshakeBudget = null;
        using var provider = InputSnapshotTestFactory.Create(
            handshake: InputSnapshotTestFactory.Handshake((path, timeout) =>
            {
                handshakePath = path;
                handshakeBudget = timeout;
                return InputSnapshotTestFactory.Success(path, timeout);
            }));
        var diagnostics = Create(provider, SocketAccess(options =>
        {
            probed = options;
            return LinuxDaemonSocketAccessResult.Accessible(options.SocketPath, LinuxDaemonGroupMembershipStatus.Member);
        }));

        var snapshot = await diagnostics.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new LinuxDaemonSocketProbeOptions(IpcProtocol.DefaultSocketPath, "crossmacro"), probed);
        Assert.Equal(IpcProtocol.DefaultSocketPath, handshakePath);
        Assert.Equal(TimeSpan.FromSeconds(2), handshakeBudget);
        Assert.True(snapshot.DaemonDiagnosticsEnabled);
        Assert.Equal(LinuxDaemonSocketAccessStatus.Accessible, snapshot.DaemonSocketAccess!.Value.Status);
        Assert.True(snapshot.CanUseDaemon);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenSocketIsMissing_SkipsTheHandshake()
    {
        var handshakes = 0;
        using var provider = InputSnapshotTestFactory.Create(
            handshake: InputSnapshotTestFactory.Handshake((path, timeout) =>
            {
                handshakes++;
                return InputSnapshotTestFactory.Success(path, timeout);
            }));
        var diagnostics = Create(provider, SocketAccess(options => LinuxDaemonSocketAccessResult.Missing(options.SocketPath)));

        var snapshot = await diagnostics.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LinuxDaemonHandshakeStatus.MissingSocket, snapshot.DaemonHandshake.Status);
        Assert.Equal("Daemon socket is missing.", snapshot.DaemonHandshake.Message);
        Assert.False(snapshot.CanUseDaemon);
        Assert.Equal(0, handshakes);
    }

    [Fact]
    public async Task GetSnapshotAsync_ProbesTheDaemonAgainOnEveryCallInsteadOfReusingTheCachedObservation()
    {
        var statuses = new Queue<LinuxDaemonHandshakeStatus>([LinuxDaemonHandshakeStatus.Timeout, LinuxDaemonHandshakeStatus.Success]);
        var handshakes = 0;
        using var provider = InputSnapshotTestFactory.Create(
            fileExists: InputSnapshotTestFactory.IsDaemonSocket,
            handshake: InputSnapshotTestFactory.Handshake((path, timeout) =>
            {
                handshakes++;
                var status = statuses.Dequeue();
                return status is LinuxDaemonHandshakeStatus.Success
                    ? InputSnapshotTestFactory.Success(path, timeout)
                    : InputSnapshotTestFactory.Failure(path, timeout, status);
            }));
        var diagnostics = Create(provider, SocketAccess(options => LinuxDaemonSocketAccessResult.Accessible(options.SocketPath)));

        var first = await diagnostics.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var second = await diagnostics.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LinuxDaemonHandshakeStatus.Timeout, first.DaemonHandshake.Status);
        Assert.Equal(LinuxDaemonHandshakeStatus.Success, second.DaemonHandshake.Status);
        Assert.Equal(2, handshakes);
    }

    [Fact]
    public async Task GetSnapshotAsync_DoesNotFeedItsProbeIntoTheCachedBackendObservation()
    {
        var handshakes = 0;
        using var provider = InputSnapshotTestFactory.Create(
            fileExists: InputSnapshotTestFactory.IsDaemonSocket,
            handshake: InputSnapshotTestFactory.Handshake((path, timeout) =>
            {
                handshakes++;
                return handshakes is 1
                    ? InputSnapshotTestFactory.Success(path, timeout)
                    : InputSnapshotTestFactory.Failure(path, timeout, LinuxDaemonHandshakeStatus.Timeout);
            }));
        var diagnostics = Create(provider, SocketAccess(options => LinuxDaemonSocketAccessResult.Accessible(options.SocketPath)));
        _ = await diagnostics.GetSnapshotAsync(TestContext.Current.CancellationToken);

        var cached = await provider.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, handshakes);
        Assert.Equal(LinuxDaemonHandshakeStatus.Timeout, cached.DaemonHandshake.Status);
    }

    [Fact]
    public async Task GetSnapshotAsync_ProbesDirectAccessFreshAndReportsPerNodeUInputFacts()
    {
        var writable = false;
        using var provider = InputSnapshotTestFactory.Create(
            canOpenForWrite: _ => writable,
            hasReadableInputDevices: () => false);
        var diagnostics = Create(
            provider,
            socketAccessProbe: null,
            fileExists: path => InputSnapshotTestFactory.IsPrimaryUInput(path) || InputSnapshotTestFactory.IsAlternateUInput(path),
            canOpenForWrite: InputSnapshotTestFactory.IsAlternateUInput);
        _ = await provider.GetSnapshotAsync(TestContext.Current.CancellationToken);
        writable = true;

        var snapshot = await diagnostics.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LinuxDirectInputFallbackStatus.MissingInputEventReadAccess, snapshot.DirectInput.Status);
        Assert.True(snapshot.DirectInput.CanWriteUInput);
        Assert.Equal(new LinuxUInputNodeAccess(Exists: true, CanWrite: false), snapshot.UInputPrimary);
        Assert.Equal(new LinuxUInputNodeAccess(Exists: true, CanWrite: true), snapshot.UInputAlternate);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenUInputNodeIsMissing_DoesNotProbeItForWriting()
    {
        var writeProbes = new List<string>();
        using var provider = InputSnapshotTestFactory.Create();
        var diagnostics = Create(
            provider,
            socketAccessProbe: null,
            fileExists: _ => false,
            canOpenForWrite: path =>
            {
                writeProbes.Add(path);
                return true;
            });

        var snapshot = await diagnostics.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(default, snapshot.UInputPrimary);
        Assert.Equal(default, snapshot.UInputAlternate);
        Assert.Empty(writeProbes);
    }

    [Fact]
    public async Task GetSnapshotAsync_PassesTheCallersTokenToTheSocketProbe()
    {
        CancellationToken probedToken = default;
        var probe = Substitute.For<ILinuxDaemonSocketAccessProbe>();
        _ = probe.ProbeAsync(Arg.Any<LinuxDaemonSocketProbeOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                probedToken = call.Arg<CancellationToken>();
                return ValueTask.FromResult(LinuxDaemonSocketAccessResult.Missing(IpcProtocol.DefaultSocketPath));
            });
        using var provider = InputSnapshotTestFactory.Create();
        using var cancellation = new CancellationTokenSource();
        var diagnostics = Create(provider, probe);

        _ = await diagnostics.GetSnapshotAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, probedToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSnapshotAsync_ForwardsTheGsrVirtualKeyboardState(bool active)
    {
        using var provider = InputSnapshotTestFactory.Create();
        var diagnostics = Create(provider, socketAccessProbe: null, isGsrVirtualKeyboardActive: () => active);

        var snapshot = await diagnostics.GetSnapshotAsync(TestContext.Current.CancellationToken);

        Assert.Equal(active, snapshot.GsrVirtualKeyboardDetected);
    }

    private static ILinuxDaemonSocketAccessProbe SocketAccess(Func<LinuxDaemonSocketProbeOptions, LinuxDaemonSocketAccessResult> probe)
    {
        var substitute = Substitute.For<ILinuxDaemonSocketAccessProbe>();
        _ = substitute.ProbeAsync(Arg.Any<LinuxDaemonSocketProbeOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => ValueTask.FromResult(probe(call.Arg<LinuxDaemonSocketProbeOptions>())));
        return substitute;
    }

    private static LinuxInputDiagnostics Create(
        ILinuxInputCapabilitySnapshotProvider snapshots,
        ILinuxDaemonSocketAccessProbe? socketAccessProbe,
        Func<string, bool>? fileExists = null,
        Func<string, bool>? canOpenForWrite = null,
        Func<bool>? isGsrVirtualKeyboardActive = null) =>
        new(
            snapshots,
            socketAccessProbe,
            fileExists ?? (static _ => false),
            canOpenForWrite ?? (static _ => false),
            isGsrVirtualKeyboardActive ?? (static () => false));
}
