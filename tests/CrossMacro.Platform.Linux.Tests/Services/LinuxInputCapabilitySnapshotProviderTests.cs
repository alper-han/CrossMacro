using static CrossMacro.Platform.Linux.Tests.Services.InputSnapshotTestFactory;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxInputCapabilitySnapshotProviderTests
{
    [Fact]
    public async Task Snapshot_DuringConcurrentInvalidation_KeepsReportingProbedAccess()
    {
        using var provider = Create(canOpenForWrite: _ => true, hasReadableInputDevices: () => true);
        using var start = new Barrier(3);
        var tasks = Enumerable.Range(0, 3).Select(worker => Task.Factory.StartNew(() =>
        {
            start.SignalAndWait(TestContext.Current.CancellationToken);
            for (var iteration = 0; iteration < 2_000; iteration++)
            {
                if (worker is 0)
                {
                    provider.InvalidateDirectInputCache();
                }

                Assert.True(provider.GetSnapshot().HasDirectInputAccess);
            }
        }, TestContext.Current.CancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void GetSnapshot_WhenDaemonHandshakeSucceeds_ResolvesDaemonAndStillProbesDirectInput()
    {
        var directProbeCount = 0;
        using var provider = Create(
            fileExists: IsDaemonSocket,
            hasReadableInputDevices: () =>
            {
                directProbeCount++;
                return false;
            });

        var snapshot = provider.GetSnapshot();

        Assert.Equal(InputProviderMode.Daemon, snapshot.ResolvedMode);
        Assert.True(snapshot.DaemonHandshakeSucceeded);
        Assert.False(snapshot.CanUseDirectUInput);
        Assert.False(snapshot.CanReadInputEvents);
        Assert.Equal(1, directProbeCount);
    }

    [Fact]
    public void GetSnapshot_PassesFiveSecondBudgetAndTheResolvedSocketPathToTheHandshake()
    {
        string? probedPath = null;
        TimeSpan? probedTimeout = null;
        using var provider = Create(
            fileExists: IsDaemonSocket,
            handshake: Handshake((path, timeout) =>
            {
                probedPath = path;
                probedTimeout = timeout;
                return Failure(path, timeout, LinuxDaemonHandshakeStatus.Timeout);
            }));

        var snapshot = provider.GetSnapshot();

        Assert.Equal(IpcProtocol.DefaultSocketPath, probedPath);
        Assert.Equal(TimeSpan.FromSeconds(5), probedTimeout);
        Assert.False(snapshot.DaemonHandshakeSucceeded);
        Assert.Equal(InputProviderMode.None, snapshot.ResolvedMode);
    }

    [Fact]
    public void GetSnapshot_WhenDaemonIsDisabled_SkipsSocketAndHandshakeProbes()
    {
        var socketProbes = 0;
        var handshakes = 0;
        using var provider = Create(
            fileExists: _ =>
            {
                socketProbes++;
                return true;
            },
            canOpenForWrite: IsPrimaryUInput,
            hasReadableInputDevices: () => true,
            handshake: Handshake((path, timeout) =>
            {
                handshakes++;
                return Success(path, timeout);
            }),
            daemonEnabled: false);

        var snapshot = provider.GetSnapshot();

        Assert.False(snapshot.DaemonSocketExists);
        Assert.False(snapshot.DaemonHandshakeSucceeded);
        Assert.True(snapshot.HasDirectInputAccess);
        Assert.Equal(InputProviderMode.Legacy, snapshot.ResolvedMode);
        Assert.Equal(0, socketProbes);
        Assert.Equal(0, handshakes);
    }

    [Fact]
    public void GetSnapshot_WhenOnlyAlternateUInputIsWritable_ResolvesDirectMode()
    {
        using var provider = Create(canOpenForWrite: IsAlternateUInput);

        var snapshot = provider.GetSnapshot();

        Assert.True(snapshot.CanUseDirectUInput);
        Assert.Equal(InputProviderMode.Legacy, snapshot.ResolvedMode);
    }

    [Fact]
    public void GetSnapshot_WhenSocketExistsButHandshakeFailsAndDirectUInputWorks_ResolvesDirectMode()
    {
        using var provider = Create(
            fileExists: IsDaemonSocket,
            canOpenForWrite: IsAlternateUInput,
            handshake: HandshakeThatFails());

        var snapshot = provider.GetSnapshot();

        Assert.False(snapshot.DaemonHandshakeSucceeded);
        Assert.Equal(InputProviderMode.Legacy, snapshot.ResolvedMode);
    }

    [Fact]
    public void GetSnapshot_WhenSocketExistsButHandshakeFailsAndNoDirectAccess_FailsFast()
    {
        using var provider = Create(fileExists: IsDaemonSocket, handshake: HandshakeThatFails());

        Assert.Equal(InputProviderMode.None, provider.GetSnapshot().ResolvedMode);
    }

    [Fact]
    public void GetSnapshot_WhenNothingIsUsable_ResolvesNone()
    {
        using var provider = Create();

        var snapshot = provider.GetSnapshot();

        Assert.False(snapshot.DaemonHandshakeSucceeded);
        Assert.False(snapshot.CanUseDirectUInput);
        Assert.Equal(InputProviderMode.None, snapshot.ResolvedMode);
        Assert.Equal(LinuxDaemonHandshakeStatus.MissingSocket, snapshot.DaemonHandshake.Status);
    }

    [Fact]
    public void GetSnapshot_WhenUInputWritableButNoUsableReadableDevice_IsNotDirectInputAccess()
    {
        using var provider = Create(
            fileExists: IsPrimaryUInput,
            canOpenForWrite: IsPrimaryUInput,
            hasReadableInputDevices: () => false);

        var snapshot = provider.GetSnapshot();

        Assert.True(snapshot.CanUseDirectUInput);
        Assert.False(snapshot.CanReadInputEvents);
        Assert.False(snapshot.HasDirectInputAccess);
        Assert.Equal(InputProviderMode.Legacy, snapshot.ResolvedMode);
    }

    [Fact]
    public void GetSnapshot_WhenDaemonRecentlySucceededAndSocketRemains_DoesNotReprobeAfterTtl()
    {
        var probes = 0;
        var now = Epoch;
        using var provider = Create(
            fileExists: IsDaemonSocket,
            handshake: Handshake((path, timeout) =>
            {
                probes++;
                return probes is 1 ? Success(path, timeout) : Failure(path, timeout);
            }),
            utcNow: () => now);

        var first = provider.GetSnapshot();
        Assert.Equal(now, first.DaemonObservedAtUtc);
        Assert.False(first.DaemonAvailabilityFromSocketHistory);

        now = now.AddSeconds(6);
        var second = provider.GetSnapshot();

        Assert.Equal(InputProviderMode.Daemon, first.ResolvedMode);
        Assert.Equal(InputProviderMode.Daemon, second.ResolvedMode);
        Assert.Equal(now, second.DaemonObservedAtUtc);
        Assert.True(second.DaemonAvailabilityFromSocketHistory);
        Assert.Equal(1, probes);
    }

    [Fact]
    public void GetSnapshot_WithinTtl_ReusesTheCachedObservation()
    {
        var socketProbes = 0;
        var now = Epoch;
        using var provider = Create(
            fileExists: path =>
            {
                socketProbes++;
                return IsDaemonSocket(path);
            },
            utcNow: () => now);

        _ = provider.GetSnapshot();
        now = now.AddSeconds(4);
        _ = provider.GetSnapshot();

        Assert.Equal(1, socketProbes);
    }

    [Fact]
    public void GetSnapshot_WhenPriorSuccessAndDirectFallbackAvailable_StaysOnDaemonWhileSocketRemains()
    {
        var now = Epoch;
        using var provider = Create(
            fileExists: path => IsDaemonSocket(path) || IsAlternateUInput(path),
            canOpenForWrite: IsAlternateUInput,
            utcNow: () => now);

        var first = provider.GetSnapshot();
        now = now.AddSeconds(6);
        var second = provider.GetSnapshot();

        Assert.Equal(InputProviderMode.Daemon, first.ResolvedMode);
        Assert.Equal(InputProviderMode.Daemon, second.ResolvedMode);
    }

    [Fact]
    public void GetSnapshot_WhenSocketDisappearsAfterSuccessAndDirectFallbackAvailable_FallsBackToDirectMode()
    {
        var now = Epoch;
        var socketAvailable = true;
        using var provider = Create(
            fileExists: path => (IsDaemonSocket(path) && socketAvailable) || IsAlternateUInput(path),
            canOpenForWrite: IsAlternateUInput,
            utcNow: () => now);

        var first = provider.GetSnapshot();
        socketAvailable = false;
        now = now.AddSeconds(6);
        var second = provider.GetSnapshot();

        Assert.Equal(InputProviderMode.Daemon, first.ResolvedMode);
        Assert.Equal(InputProviderMode.Legacy, second.ResolvedMode);
    }

    [Fact]
    public void GetSnapshot_PreservesDaemonThroughTransientFailuresThenFailsFast()
    {
        var now = Epoch;
        var socketAvailable = true;
        using var provider = Create(
            fileExists: path => IsDaemonSocket(path) && socketAvailable,
            utcNow: () => now);

        Assert.Equal(InputProviderMode.Daemon, provider.GetSnapshot().ResolvedMode);

        socketAvailable = false;
        now = now.AddSeconds(6);
        Assert.Equal(InputProviderMode.Daemon, provider.GetSnapshot().ResolvedMode);

        now = now.AddSeconds(6);
        Assert.Equal(InputProviderMode.Daemon, provider.GetSnapshot().ResolvedMode);

        now = now.AddSeconds(6);
        Assert.Equal(InputProviderMode.None, provider.GetSnapshot().ResolvedMode);
    }

    [Fact]
    public void GetSnapshot_WhenGraceWindowExpiresWithoutFallback_FailsFast()
    {
        var now = Epoch;
        var socketAvailable = true;
        using var provider = Create(
            fileExists: path => IsDaemonSocket(path) && socketAvailable,
            utcNow: () => now);

        _ = provider.GetSnapshot();
        socketAvailable = false;
        now = now.AddSeconds(31);

        Assert.Equal(InputProviderMode.None, provider.GetSnapshot().ResolvedMode);
    }

    [Fact]
    public void InvalidateCache_WhenPermissionsChange_ReprobesAndResolvesDirectMode()
    {
        var uinputWritable = false;
        using var provider = Create(canOpenForWrite: _ => uinputWritable);

        Assert.Equal(InputProviderMode.None, provider.GetSnapshot().ResolvedMode);

        uinputWritable = true;
        provider.InvalidateCache();

        Assert.Equal(InputProviderMode.Legacy, provider.GetSnapshot().ResolvedMode);
    }

    [Fact]
    public void InvalidateCache_ForgetsDaemonHistory()
    {
        var now = Epoch;
        var socketAvailable = true;
        using var provider = Create(
            fileExists: path => IsDaemonSocket(path) && socketAvailable,
            utcNow: () => now);

        _ = provider.GetSnapshot();
        provider.InvalidateCache();
        socketAvailable = false;

        Assert.Equal(InputProviderMode.None, provider.GetSnapshot().ResolvedMode);
    }

    [Fact]
    public void InvalidateDirectInputCache_ReprobesDirectAccessButKeepsDaemonObservation()
    {
        var writable = false;
        var handshakes = 0;
        using var provider = Create(
            fileExists: IsDaemonSocket,
            canOpenForWrite: _ => writable,
            hasReadableInputDevices: () => true,
            handshake: Handshake((path, timeout) =>
            {
                handshakes++;
                return Success(path, timeout);
            }));

        Assert.False(provider.GetSnapshot().HasDirectInputAccess);

        writable = true;
        provider.InvalidateDirectInputCache();

        Assert.True(provider.GetSnapshot().HasDirectInputAccess);
        Assert.Equal(1, handshakes);
    }

    [Fact]
    public void GetSnapshot_WhenUInputProbeThrows_FailsClosedAndCachesResult()
    {
        var probes = 0;
        using var provider = Create(canOpenForWrite: _ =>
        {
            probes++;
            throw new InvalidOperationException("uinput probe failed");
        });

        Assert.False(provider.GetSnapshot().CanUseDirectUInput);
        Assert.False(provider.GetSnapshot().CanUseDirectUInput);
        Assert.Equal(1, probes);
    }

    [Fact]
    public void GetSnapshot_WhenReadableDeviceProbeThrows_FailsClosedAndCachesResult()
    {
        var probes = 0;
        using var provider = Create(hasReadableInputDevices: () =>
        {
            probes++;
            throw new InvalidOperationException("input-event probe failed");
        });

        Assert.False(provider.GetSnapshot().CanReadInputEvents);
        Assert.False(provider.GetSnapshot().CanReadInputEvents);
        Assert.Equal(1, probes);
    }

    [Fact]
    public void GetSnapshot_WhenHandshakeProbeThrows_ClassifiesTheFailure()
    {
        using var provider = Create(
            fileExists: IsDaemonSocket,
            canOpenForWrite: IsPrimaryUInput,
            hasReadableInputDevices: () => true,
            handshake: (_, _, _) => throw new UnauthorizedAccessException("permission denied"));

        var snapshot = provider.GetSnapshot();

        Assert.False(snapshot.DaemonHandshakeSucceeded);
        Assert.Equal(LinuxDaemonHandshakeStatus.PermissionDenied, snapshot.DaemonHandshake.Status);
        Assert.Equal(InputProviderMode.Legacy, snapshot.ResolvedMode);
    }

    [Fact]
    public async Task GetSnapshotAsync_WhenCanceled_PropagatesCancellation()
    {
        using var provider = Create(fileExists: IsDaemonSocket);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetSnapshotAsync(cancellation.Token).AsTask());
    }

    [Fact]
    public void GetSnapshot_WhenPermissionDeniedButDirectFallbackAvailable_PreservesDaemonDiagnostic()
    {
        using var provider = Create(
            fileExists: IsDaemonSocket,
            canOpenForWrite: IsPrimaryUInput,
            hasReadableInputDevices: () => true,
            handshake: HandshakeThatFails(
                LinuxDaemonHandshakeStatus.PermissionDenied,
                new UnauthorizedAccessException("permission denied")));

        var snapshot = provider.GetSnapshot();

        Assert.Equal(InputProviderMode.Legacy, snapshot.ResolvedMode);
        Assert.True(snapshot.HasDirectInputAccess);
        Assert.False(snapshot.DaemonHandshakeTimedOut);
        Assert.Equal(LinuxDaemonHandshakeStatus.PermissionDenied, snapshot.DaemonHandshake.Status);
    }

    [Fact]
    public void GetSnapshot_WhenDaemonTimesOut_KeepsTimeoutDistinctFromPermissionDenied()
    {
        using var provider = Create(
            fileExists: IsDaemonSocket,
            canOpenForWrite: IsPrimaryUInput,
            hasReadableInputDevices: () => true,
            handshake: HandshakeThatFails(LinuxDaemonHandshakeStatus.Timeout));

        var snapshot = provider.GetSnapshot();

        Assert.True(snapshot.DaemonHandshakeTimedOut);
        Assert.Equal(LinuxDaemonHandshakeStatus.Timeout, snapshot.DaemonHandshake.Status);
        Assert.Equal(InputProviderMode.Legacy, snapshot.ResolvedMode);
    }

    [Fact]
    public void GetSnapshot_WhenSocketIsMissingAndDirectFallbackAvailable_RecordsMissingSocket()
    {
        using var provider = Create(
            fileExists: IsPrimaryUInput,
            canOpenForWrite: IsPrimaryUInput,
            hasReadableInputDevices: () => true);

        var snapshot = provider.GetSnapshot();

        Assert.Equal(InputProviderMode.Legacy, snapshot.ResolvedMode);
        Assert.False(snapshot.DaemonSocketExists);
        Assert.Equal(LinuxDaemonHandshakeStatus.MissingSocket, snapshot.DaemonHandshake.Status);
    }

    [Fact]
    public void GetSnapshot_WhenSocketPermissionDeniedWithoutFallback_FailsFast()
    {
        using var provider = Create(
            fileExists: IsDaemonSocket,
            handshake: HandshakeThatFails(LinuxDaemonHandshakeStatus.PermissionDenied));

        var snapshot = provider.GetSnapshot();

        Assert.Equal(InputProviderMode.None, snapshot.ResolvedMode);
        Assert.Equal(LinuxDaemonHandshakeStatus.PermissionDenied, snapshot.DaemonHandshake.Status);
    }

    [Fact]
    public void GetSnapshot_WhenSocketCreatedAfterAFailedProbe_ProbesAgainOnceTtlElapses()
    {
        var now = Epoch;
        var socketExists = false;
        using var provider = Create(fileExists: path => IsDaemonSocket(path) && socketExists, utcNow: () => now);

        Assert.False(provider.GetSnapshot().DaemonHandshakeSucceeded);

        socketExists = true;
        now = now.AddSeconds(6);

        Assert.True(provider.GetSnapshot().DaemonHandshakeSucceeded);
    }

    [Fact]
    public void ModePolicy_GraceWindowIsDeterministicAtBoundaries()
    {
        var lastSuccess = Epoch;
        var grace = TimeSpan.FromSeconds(30);

        Assert.True(LinuxInputModePolicy.IsWithinDaemonGracePeriod(lastSuccess.AddSeconds(30), lastSuccess, 2, grace, 3));
        Assert.False(LinuxInputModePolicy.IsWithinDaemonGracePeriod(lastSuccess.AddSeconds(30).AddTicks(1), lastSuccess, 2, grace, 3));
        Assert.False(LinuxInputModePolicy.IsWithinDaemonGracePeriod(lastSuccess.AddSeconds(1), lastSuccess, 3, grace, 3));
        Assert.False(LinuxInputModePolicy.IsWithinDaemonGracePeriod(lastSuccess.AddSeconds(1), DateTime.MinValue, 0, grace, 3));
    }
}
