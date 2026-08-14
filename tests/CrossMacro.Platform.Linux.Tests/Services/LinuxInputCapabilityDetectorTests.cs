
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxInputCapabilityDetectorTests
{
    [Fact]
    public void IsWithinDaemonGracePeriod_IsDeterministicAtGraceAndFailureBoundaries()
    {
        var lastSuccess = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var grace = TimeSpan.FromSeconds(30);

        Assert.True(LinuxInputCapabilityDetector.IsWithinDaemonGracePeriod(
            lastSuccess.AddSeconds(30), lastSuccess, 2, grace, 3));
        Assert.False(LinuxInputCapabilityDetector.IsWithinDaemonGracePeriod(
            lastSuccess.AddSeconds(30).AddTicks(1), lastSuccess, 2, grace, 3));
        Assert.False(LinuxInputCapabilityDetector.IsWithinDaemonGracePeriod(
            lastSuccess.AddSeconds(1), lastSuccess, 3, grace, 3));
    }

    [LinuxFact]
    public void DetermineMode_WhenDaemonHandshakeSucceeds_ReturnsDaemon()
    {
        // Arrange
        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            getInputEventCandidates: () => [],
            utcNow: () => DateTime.UtcNow);

        // Act
        var mode = detector.DetermineMode();

        // Assert
        Assert.True(detector.CanConnectToDaemon);
        Assert.Equal(InputProviderMode.Daemon, mode);
    }

    [LinuxFact]
    public void DetermineMode_WhenDaemonIsDisabled_SkipsSocketAndHandshakeProbes()
    {
        var socketProbeCount = 0;
        var handshakeProbeCount = 0;
        var detector = new LinuxInputCapabilityDetector(
            fileExists: _ =>
            {
                socketProbeCount++;
                return true;
            },
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputDevicePath, StringComparison.Ordinal),
            canOpenForRead: path => path is "/dev/input/event0",
            daemonHandshakeProbe: (_, _) =>
            {
                handshakeProbeCount++;
                return LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Success();
            },
            getInputEventCandidates: () => ["/dev/input/event0"],
            utcNow: () => DateTime.UtcNow,
            daemonEnabled: false);

        var mode = detector.DetermineMode();
        var snapshot = detector.GetSnapshot();

        Assert.Equal(InputProviderMode.Legacy, mode);
        Assert.False(snapshot.DaemonSocketExists);
        Assert.False(snapshot.DaemonHandshakeSucceeded);
        Assert.Equal(0, socketProbeCount);
        Assert.Equal(0, handshakeProbeCount);
    }

    [LinuxFact]
    public void DetermineMode_WhenOnlyAlternateUInputIsWritable_ReturnsLegacy()
    {
        // Arrange
        var detector = new LinuxInputCapabilityDetector(
            fileExists: _ => false,
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputAlternatePath, StringComparison.Ordinal),
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => [],
            utcNow: () => DateTime.UtcNow);

        // Act
        var mode = detector.DetermineMode();

        // Assert
        Assert.True(detector.CanUseDirectUInput);
        Assert.Equal(InputProviderMode.Legacy, mode);
    }

    [LinuxFact]
    public void DetermineMode_WhenSocketExistsButHandshakeFails_UsesLegacyFallback()
    {
        // Arrange
        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputAlternatePath, StringComparison.Ordinal),
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => [],
            utcNow: () => DateTime.UtcNow);

        // Act
        var mode = detector.DetermineMode();

        // Assert
        Assert.False(detector.CanConnectToDaemon);
        Assert.Equal(InputProviderMode.Legacy, mode);
    }

    [LinuxFact]
    public void DetermineMode_WhenSocketExistsButHandshakeFailsAndUInputUnavailable_ReturnsNone()
    {
        // Arrange
        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => [],
            utcNow: () => DateTime.UtcNow);

        // Act
        var mode = detector.DetermineMode();

        // Assert
        Assert.False(detector.CanConnectToDaemon);
        Assert.Equal(InputProviderMode.None, mode);
    }

    [LinuxFact]
    public void DetermineMode_WhenNoDaemonAndNoUInput_ReturnsNone()
    {
        // Arrange
        var detector = new LinuxInputCapabilityDetector(
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => [],
            utcNow: () => DateTime.UtcNow);

        // Act
        var mode = detector.DetermineMode();

        // Assert
        Assert.False(detector.CanConnectToDaemon);
        Assert.False(detector.CanUseDirectUInput);
        Assert.Equal(InputProviderMode.None, mode);
    }

    [LinuxFact]
    public void DetermineMode_WhenDaemonRecentlySucceededAndSocketRemainsPresent_DoesNotReprobeDaemon()
    {
        // Arrange
        var probeCount = 0;
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ =>
            {
                probeCount++;
                return probeCount is 1;
            },
            getInputEventCandidates: () => [],
            utcNow: () => now);

        // Act
        var firstMode = detector.DetermineMode();
        now = now.AddSeconds(6);
        var secondMode = detector.DetermineMode();

        Assert.Equal(InputProviderMode.Daemon, firstMode);
        Assert.Equal(InputProviderMode.Daemon, secondMode);
        Assert.Equal(1, probeCount);
    }

    [LinuxFact]
    public void DetermineMode_WhenPriorSuccessAndSocketRemainsPresent_IgnoresDirectFallbackAndKeepsDaemonMode()
    {
        var probeCount = 0;
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal) ||
string.Equals(path, LinuxConstants.UInputAlternatePath, StringComparison.Ordinal),
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputAlternatePath, StringComparison.Ordinal),
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ =>
            {
                probeCount++;
                return probeCount is 1;
            },
            getInputEventCandidates: () => [],
            utcNow: () => now);

        var firstMode = detector.DetermineMode();
        now = now.AddSeconds(6);
        var secondMode = detector.DetermineMode();

        Assert.Equal(InputProviderMode.Daemon, firstMode);
        Assert.Equal(InputProviderMode.Daemon, secondMode);
        Assert.Equal(1, probeCount);
    }

    [LinuxFact]
    public void DetermineMode_WhenPriorSuccessSocketDisappearsAndDirectFallbackIsAvailable_FallsBackToLegacy()
    {
        var probeCount = 0;
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var socketAvailable = true;

        var detector = new LinuxInputCapabilityDetector(
            fileExists: path =>
                (string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal) && socketAvailable) ||
string.Equals(path, LinuxConstants.UInputAlternatePath, StringComparison.Ordinal),
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputAlternatePath, StringComparison.Ordinal),
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ =>
            {
                probeCount++;
                return probeCount is 1;
            },
            getInputEventCandidates: () => [],
            utcNow: () => now);

        var firstMode = detector.DetermineMode();
        socketAvailable = false;
        now = now.AddSeconds(6);
        var secondMode = detector.DetermineMode();

        Assert.Equal(InputProviderMode.Daemon, firstMode);
        Assert.Equal(InputProviderMode.Legacy, secondMode);
        Assert.Equal(1, probeCount);
    }

    [LinuxFact]
    public void GetSnapshot_PreservesDaemonThroughTransientFailuresThenUsesThresholdFallback()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var socketAvailable = true;
        var probeCount = 0;

        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal) && socketAvailable,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) =>
            {
                probeCount++;
                return probeCount is 1
                    ? LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Success()
                    : LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(
                        LinuxDaemonHandshakeStatus.UnexpectedError);
            },
            getInputEventCandidates: () => [],
            utcNow: () => now);

        Assert.Equal(InputProviderMode.Daemon, detector.DetermineMode());

        socketAvailable = false;
        now = now.AddSeconds(6);
        Assert.Equal(InputProviderMode.Daemon, detector.GetSnapshot().ResolvedMode);

        now = now.AddSeconds(6);
        Assert.Equal(InputProviderMode.Daemon, detector.GetSnapshot().ResolvedMode);

        now = now.AddSeconds(6);
        var fallbackSnapshot = detector.GetSnapshot();

        Assert.Equal(InputProviderMode.None, fallbackSnapshot.ResolvedMode);
        Assert.Equal(1, probeCount);
    }

    [LinuxFact]
    public void DetermineMode_WhenDaemonSocketDisappearsAfterSuccess_FallsBackToLegacy()
    {
        // Arrange
        var probeCount = 0;
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var socketAvailable = true;

        var detector = new LinuxInputCapabilityDetector(
            fileExists: path =>
                (string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal) && socketAvailable) ||
string.Equals(path, LinuxConstants.UInputAlternatePath, StringComparison.Ordinal),
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputAlternatePath, StringComparison.Ordinal),
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ =>
            {
                probeCount++;
                return true;
            },
            getInputEventCandidates: () => [],
            utcNow: () => now);

        // Act
        var firstMode = detector.DetermineMode(); // daemon success
        socketAvailable = false;
        now = now.AddSeconds(31);
        var secondMode = detector.DetermineMode(); // no socket => fallback legacy

        // Assert
        Assert.Equal(InputProviderMode.Daemon, firstMode);
        Assert.Equal(InputProviderMode.Legacy, secondMode);
        Assert.Equal(1, probeCount);
    }

    [LinuxFact]
    public void InvalidateCache_WhenPermissionsChange_ReprobesAndReturnsLegacy()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var uinputWritable = false;

        var detector = new LinuxInputCapabilityDetector(
            fileExists: _ => false,
            canOpenForWrite: _ => uinputWritable,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => [],
            utcNow: () => now);

        var firstMode = detector.DetermineMode();
        Assert.Equal(InputProviderMode.None, firstMode);

        uinputWritable = true;
        detector.InvalidateCache();
        var secondMode = detector.DetermineMode();

        Assert.Equal(InputProviderMode.Legacy, secondMode);
    }

    [LinuxFact]
    public void CanReadInputEvents_WhenReadableEventExists_ReturnsTrue()
    {
        var detector = new LinuxInputCapabilityDetector(
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            canOpenForRead: path => path is "/dev/input/event5",
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => ["/dev/input/event4", "/dev/input/event5"],
            utcNow: () => DateTime.UtcNow);

        Assert.True(detector.CanReadInputEvents);
    }

    [LinuxFact]
    public void DetermineMode_WhenRawEventReadableButNoUsableInputDevice_ReturnsLegacyButDirectFallbackUnavailable()
    {
        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, LinuxConstants.UInputDevicePath, StringComparison.Ordinal),
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputDevicePath, StringComparison.Ordinal),
            hasUsableReadableInputDevices: () => false,
            daemonHandshakeProbe: (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(LinuxDaemonHandshakeStatus.MissingSocket),
            utcNow: () => DateTime.UtcNow);

        var mode = detector.DetermineMode();
        var snapshot = detector.GetSnapshot();

        Assert.Equal(InputProviderMode.Legacy, mode);
        Assert.True(detector.CanUseDirectUInput);
        Assert.False(detector.CanReadInputEvents);
        Assert.False(snapshot.HasDirectInputAccess);
    }

    [LinuxFact]
    public void DetermineMode_WhenDaemonHandshakeFailsBecausePermissionDenied_UsesLegacyFallback()
    {
        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputDevicePath, StringComparison.Ordinal),
            canOpenForRead: path => path is "/dev/input/event0",
            daemonHandshakeProbe: _ => throw new UnauthorizedAccessException("permission denied"),
            getInputEventCandidates: () => ["/dev/input/event0"],
            utcNow: () => DateTime.UtcNow);

        var mode = detector.DetermineMode();

        Assert.False(detector.CanConnectToDaemon);
        Assert.True(detector.CanUseDirectUInput);
        Assert.True(detector.CanReadInputEvents);
        Assert.Equal(InputProviderMode.Legacy, mode);
    }

    [LinuxFact]
    public void GetSnapshot_WhenPermissionDeniedButDirectFallbackIsAvailable_PreservesDaemonDiagnostic()
    {
        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputDevicePath, StringComparison.Ordinal),
            canOpenForRead: path => path is "/dev/input/event0",
            daemonHandshakeProbe: (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(
                LinuxDaemonHandshakeStatus.PermissionDenied,
                new UnauthorizedAccessException("permission denied")),
            getInputEventCandidates: () => ["/dev/input/event0"],
            utcNow: () => DateTime.UtcNow);

        var mode = detector.DetermineMode();
        var snapshot = detector.GetSnapshot();

        Assert.Equal(InputProviderMode.Legacy, mode);
        Assert.True(snapshot.HasDirectInputAccess);
        Assert.False(snapshot.DaemonHandshakeSucceeded);
        Assert.False(snapshot.DaemonHandshakeTimedOut);
        Assert.Equal(LinuxDaemonHandshakeStatus.PermissionDenied, snapshot.DaemonHandshake.Status);
    }

    [LinuxFact]
    public void GetSnapshot_WhenDaemonTimesOut_PreservesTimeoutDiagnostic()
    {
        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputDevicePath, StringComparison.Ordinal),
            canOpenForRead: path => path is "/dev/input/event0",
            daemonHandshakeProbe: (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Timeout(
                new TimeoutException("timeout")),
            getInputEventCandidates: () => ["/dev/input/event0"],
            utcNow: () => DateTime.UtcNow);

        var mode = detector.DetermineMode();
        var snapshot = detector.GetSnapshot();

        Assert.Equal(InputProviderMode.Legacy, mode);
        Assert.True(snapshot.DaemonHandshakeTimedOut);
        Assert.Equal(LinuxDaemonHandshakeStatus.Timeout, snapshot.DaemonHandshake.Status);
        Assert.NotEqual(LinuxDaemonHandshakeStatus.PermissionDenied, snapshot.DaemonHandshake.Status);
    }

    [LinuxFact]
    public void GetSnapshot_WhenDaemonSocketMissingAndDirectFallbackIsAvailable_RecordsMissingSocketAndReturnsLegacy()
    {
        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, LinuxConstants.UInputDevicePath, StringComparison.Ordinal),
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputDevicePath, StringComparison.Ordinal),
            canOpenForRead: path => path is "/dev/input/event0",
            daemonHandshakeProbe: (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Success(),
            getInputEventCandidates: () => ["/dev/input/event0"],
            utcNow: () => DateTime.UtcNow);

        var mode = detector.DetermineMode();
        var snapshot = detector.GetSnapshot();

        Assert.Equal(InputProviderMode.Legacy, mode);
        Assert.False(snapshot.DaemonSocketExists);
        Assert.True(snapshot.HasDirectInputAccess);
        Assert.Equal(LinuxDaemonHandshakeStatus.MissingSocket, snapshot.DaemonHandshake.Status);
    }

    [LinuxFact]
    public void SnapshotProvider_WhenPermissionDeniedAndTimeoutResultsOccur_KeepsDistinctHandshakeStatuses()
    {
        var permissionDeniedProvider = new LinuxInputCapabilitySnapshotProvider(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputDevicePath, StringComparison.Ordinal),
            canOpenForRead: path => path is "/dev/input/event0",
            daemonHandshakeProbe: (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(
                LinuxDaemonHandshakeStatus.PermissionDenied,
                new UnauthorizedAccessException("permission denied")),
            getInputEventCandidates: () => ["/dev/input/event0"]);
        var timeoutProvider = new LinuxInputCapabilitySnapshotProvider(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputDevicePath, StringComparison.Ordinal),
            canOpenForRead: path => path is "/dev/input/event0",
            daemonHandshakeProbe: (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Timeout(
                new TimeoutException("timeout")),
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var permissionDeniedSnapshot = permissionDeniedProvider.CaptureSnapshot(TimeSpan.FromSeconds(5));
        var timeoutSnapshot = timeoutProvider.CaptureSnapshot(TimeSpan.FromSeconds(5));

        Assert.Equal(LinuxDaemonHandshakeStatus.PermissionDenied, permissionDeniedSnapshot.DaemonHandshake.Status);
        Assert.False(permissionDeniedSnapshot.DaemonHandshakeTimedOut);
        Assert.Equal(LinuxDaemonHandshakeStatus.Timeout, timeoutSnapshot.DaemonHandshake.Status);
        Assert.True(timeoutSnapshot.DaemonHandshakeTimedOut);
    }

    [LinuxFact]
    public void SnapshotProvider_WhenDaemonIsDisabled_SkipsSocketAndHandshakeProbes()
    {
        var socketProbeCount = 0;
        var handshakeProbeCount = 0;
        var provider = new LinuxInputCapabilitySnapshotProvider(
            fileExists: _ =>
            {
                socketProbeCount++;
                return true;
            },
            canOpenForWrite: path => string.Equals(path, LinuxConstants.UInputDevicePath, StringComparison.Ordinal),
            canOpenForRead: path => path is "/dev/input/event0",
            daemonHandshakeProbe: (_, _) =>
            {
                handshakeProbeCount++;
                return LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Success();
            },
            getInputEventCandidates: () => ["/dev/input/event0"],
            daemonEnabled: false);

        var snapshot = provider.CaptureSnapshot(TimeSpan.FromSeconds(5));

        Assert.False(snapshot.DaemonSocketExists);
        Assert.False(snapshot.DaemonHandshakeSucceeded);
        Assert.True(snapshot.HasDirectInputAccess);
        Assert.Equal(0, socketProbeCount);
        Assert.Equal(0, handshakeProbeCount);
    }

    [LinuxFact]
    public void DetermineMode_WhenIssue44SocketPermissionDeniedScenarioHasNoFallback_ReturnsNone()
    {
        var scenario = Issue44LinuxInputCapabilityScenario.SocketPermissionDenied();
        var detector = scenario.CreateDetector();

        var mode = detector.DetermineMode();
        var snapshot = scenario.CreateDiagnosticSnapshot();

        Assert.Equal(InputProviderMode.None, mode);
        Assert.False(detector.CanConnectToDaemon);
        Assert.Equal(LinuxDaemonSocketAccessStatus.PermissionDenied, snapshot.SocketAccess.Status);
        Assert.Equal(LinuxDaemonHandshakeStatus.PermissionDenied, snapshot.Handshake.Status);
    }

    [LinuxFact]
    public void DetermineMode_WhenIssue44DirectFallbackAvailableScenario_ReturnsLegacy()
    {
        var scenario = Issue44LinuxInputCapabilityScenario.DirectFallbackAvailable();
        var detector = scenario.CreateDetector();

        var mode = detector.DetermineMode();
        var snapshot = scenario.CreateDiagnosticSnapshot();

        Assert.Equal(InputProviderMode.Legacy, mode);
        Assert.True(detector.CanUseDirectUInput);
        Assert.True(detector.CanReadInputEvents);
        Assert.True(snapshot.DirectInputFallback.IsAvailable);
    }

    [LinuxFact]
    public void Issue44ScenarioBuilders_ExposeMissingGroupAndStaleSessionDiagnostics()
    {
        var missingGroup = Issue44LinuxInputCapabilityScenario.Issue44MissingCrossmacroGroup().CreateSocketAccessResult();
        var staleSession = Issue44LinuxInputCapabilityScenario.Issue44StaleCrossmacroSession().CreateSocketAccessResult();

        Assert.Equal(LinuxDaemonGroupMembershipStatus.MissingGroup, missingGroup.GroupMembershipStatus);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.StaleSession, staleSession.GroupMembershipStatus);
        Assert.Equal(LinuxFileSystemEntryKind.Socket, staleSession.Metadata?.EntryKind);
    }

    [LinuxFact]
    public void DetermineMode_WhenDaemonHandshakeTimesOutAndDirectInputUnavailable_ReturnsNone()
    {
        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => throw new TimeoutException("timeout"),
            getInputEventCandidates: () => [],
            utcNow: () => DateTime.UtcNow);

        var mode = detector.DetermineMode();

        Assert.False(detector.CanConnectToDaemon);
        Assert.False(detector.CanUseDirectUInput);
        Assert.False(detector.CanReadInputEvents);
        Assert.Equal(InputProviderMode.None, mode);
    }

    [LinuxFact]
    public void DetermineMode_WhenDaemonProbeUsesTimeoutResult_PassesStartupBudgetToProbe()
    {
        TimeSpan? requestedTimeout = null;

        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (socketPath, timeout) =>
            {
                requestedTimeout = timeout;
                Assert.Equal(IpcProtocol.DefaultSocketPath, socketPath);
                return LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Timeout();
            },
            getInputEventCandidates: () => [],
            utcNow: () => DateTime.UtcNow);

        var mode = detector.DetermineMode();

        Assert.Equal(TimeSpan.FromSeconds(5), requestedTimeout);
        Assert.False(detector.CanConnectToDaemon);
        Assert.Equal(InputProviderMode.None, mode);
    }

    [LinuxFact]
    public async Task ProbeDaemonHandshakeWithinBudget_WhenServerAcceptsButNeverReplies_TimesOutWithinSingleBudgetAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var socketPath = TestSocketPaths.CreateShort("cm-input");
        using var server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        server.Bind(new UnixDomainSocketEndPoint(socketPath));
        server.Listen(1);

        var releaseAcceptedClient = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acceptTask = Task.Run(async () =>
        {
            using var accepted = await server.AcceptAsync();
            accepted.ReceiveTimeout = 1000;

            try
            {
                var buffer = new byte[sizeof(byte) + sizeof(int)];
                var totalRead = 0;
                while (totalRead < buffer.Length)
                {
                    var read = accepted.Receive(buffer, totalRead, buffer.Length - totalRead, SocketFlags.None);
                    if (read <= 0)
                    {
                        break;
                    }

                    totalRead += read;
                }
            }
            catch (IOException)
            {
                // Best effort drain only; the probe result itself is the real assertion target.
            }

            await releaseAcceptedClient.Task;
        });

        try
        {
            var timeout = TimeSpan.FromMilliseconds(250);
            var sw = Stopwatch.StartNew();
            var result = LinuxInputCapabilityDetector.ProbeDaemonHandshakeWithinBudget(socketPath, timeout);
            sw.Stop();

            Assert.False(result.Succeeded);
            Assert.True(result.TimedOut);
            Assert.InRange(sw.ElapsedMilliseconds, 100, 1200);
        }
        finally
        {
            releaseAcceptedClient.SetResult();
            await acceptTask;
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }
}
