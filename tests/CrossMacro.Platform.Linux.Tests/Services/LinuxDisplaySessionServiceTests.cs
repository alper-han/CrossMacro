using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Linux;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services;

[Collection("EnvironmentVariableSensitive")]
public sealed class LinuxDisplaySessionServiceTests
{
    [LinuxFact]
    public void IsSessionSupported_WhenNotFlatpak_ShouldReturnTrue()
    {
        var service = CreateService(
            Snapshot(sessionType: "wayland", useDaemon: "0"),
            fileExists: _ => true,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakAndUnsupportedSession_ShouldReturnFalseWithReason()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "tty", useDaemon: "0"),
            fileExists: _ => true,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("Unsupported Flatpak session", reason, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakAndMissingSession_ShouldReturnFalseWithReason()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro"),
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("Unsupported Flatpak session", reason, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandWithDaemonSocket_ShouldReturnTrue()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "1"),
            fileExists: _ => true,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Success(),
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out _);

        Assert.True(supported);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonModeWithoutSocket_ShouldReturnFalse()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "1"),
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("handshake failed", reason, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDirectModeWithoutUInput_ShouldReturnFalse()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "0"),
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => true,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("/dev/uinput", reason, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDirectModeWithUInput_ShouldReturnTrue()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "0"),
            fileExists: _ => false,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: path => path == "/dev/input/event0",
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out _);

        Assert.True(supported);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonFlagMissingAndDirectReady_ShouldReturnTrueWithoutDaemonProbe()
    {
        var probeCount = 0;
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: null),
            fileExists: _ => false,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: path => path == "/dev/input/event0",
            daemonHandshakeProbe: (_, _) =>
            {
                probeCount++;
                return LinuxDisplaySessionService.DaemonHandshakeProbeResult.Success();
            },
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
        Assert.Equal(0, probeCount);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonFlagIsNotOneAndDirectReady_ShouldReturnTrueWithoutDaemonProbe()
    {
        var probeCount = 0;
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "true"),
            fileExists: _ => false,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: path => path == "/dev/input/event0",
            daemonHandshakeProbe: (_, _) =>
            {
                probeCount++;
                return LinuxDisplaySessionService.DaemonHandshakeProbeResult.Success();
            },
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
        Assert.Equal(0, probeCount);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDirectModeWithUInputButNoEventRead_ShouldReturnFalse()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "0"),
            fileExists: _ => false,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("/dev/input/event*", reason, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDirectModeHasRawEventButNoUsableDevice_ShouldReturnFalse()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "0"),
            fileExists: _ => false,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: path => path == "/dev/input/event0",
            hasUsableReadableInputDevices: () => false,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("/dev/input/event*", reason, StringComparison.Ordinal);
    }


    [LinuxFact]
    public void IsSessionSupported_WhenDirectProbeThrows_ShouldReturnFalse()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "0"),
            fileExists: _ => false,
            canOpenForWrite: _ => throw new UnauthorizedAccessException("uinput denied"),
            canOpenForRead: _ => true,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("Wayland direct mode", reason, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakX11DetectedByCompositor_ShouldReturnTrue()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", display: ":0"),
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonSocketExistsButHandshakeFails_ShouldReturnFalse()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "1"),
            fileExists: _ => true,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("handshake failed", reason, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonHandshakeFailsButDirectReady_ShouldReturnTrue()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "1"),
            fileExists: _ => true,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: path => path == "/dev/input/event0",
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakX11Session_ShouldReturnTrue()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "x11", useDaemon: "0"),
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakX11DaemonHandshakeFailsAndDirectUnavailable_ShouldReturnTrueWithoutProbing()
    {
        var probeCount = 0;

        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", useDaemon: "1", sessionType: "x11"),
            fileExists: _ => true,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) =>
            {
                probeCount++;
                return LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed();
            },
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
        Assert.Equal(0, probeCount);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonTimesOutButDirectReady_ShouldReturnTrue()
    {
        TimeSpan? requestedTimeout = null;

        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "1"),
            fileExists: _ => true,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: path => path == "/dev/input/event0",
            daemonHandshakeProbe: (socketPath, timeout) =>
            {
                requestedTimeout = timeout;
                Assert.Equal(IpcProtocol.DefaultSocketPath, socketPath);
                return LinuxDisplaySessionService.DaemonHandshakeProbeResult.Timeout();
            },
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
        Assert.Equal(TimeSpan.FromSeconds(5), requestedTimeout);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonTimesOutAndDirectUnavailable_ShouldReturnFalse()
    {
        TimeSpan? requestedTimeout = null;

        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "1"),
            fileExists: _ => true,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (socketPath, timeout) =>
            {
                requestedTimeout = timeout;
                Assert.Equal(IpcProtocol.DefaultSocketPath, socketPath);
                return LinuxDisplaySessionService.DaemonHandshakeProbeResult.Timeout();
            },
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("handshake failed", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TimeSpan.FromSeconds(5), requestedTimeout);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonFailsButDirectReady_ProbesHandshakeOnlyOnce()
    {
        var probeCount = 0;

        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "1"),
            fileExists: _ => true,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: path => path == "/dev/input/event0",
            daemonHandshakeProbe: (_, _) =>
            {
                probeCount++;
                return LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed();
            },
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
        Assert.Equal(1, probeCount);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenEnvironmentChangesDuringCall_UsesOneStartupSnapshot()
    {
        var environment = new SequencedEnvironmentVariables(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "1"),
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "tty", useDaemon: "0"));

        var service = CreateService(
            environment,
            fileExists: _ => true,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: path => path == "/dev/input/event0",
            daemonHandshakeProbe: (_, _) => LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
        Assert.Equal(1, environment.CaptureCount);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonPermissionDeniedAndDirectUnavailable_ShouldReturnFalse()
    {
        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "1"),
            fileExists: _ => true,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (_, _) => throw new UnauthorizedAccessException("permission denied"),
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("handshake failed", reason, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenDaemonHandshakeProbeReturnsAfterDelay_ShouldWaitForProbeResult()
    {
        TimeSpan? requestedTimeout = null;

        var service = CreateService(
            Snapshot(flatpakId: "io.github.alper_han.crossmacro", sessionType: "wayland", useDaemon: "1"),
            fileExists: _ => true,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: (socketPath, timeout) =>
            {
                requestedTimeout = timeout;
                Assert.Equal(IpcProtocol.DefaultSocketPath, socketPath);
                return LinuxDisplaySessionService.DaemonHandshakeProbeResult.Failed();
            },
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("handshake failed", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TimeSpan.FromSeconds(5), requestedTimeout);
    }

    [LinuxFact]
    public async Task ProbeDaemonHandshakeWithinBudget_WhenServerAcceptsButNeverReplies_TimesOutWithinSingleBudgetAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var socketPath = Path.Combine(Path.GetTempPath(), $"crossmacro-display-probe-{Guid.NewGuid():N}.sock");
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
            var result = LinuxDisplaySessionService.ProbeDaemonHandshakeWithinBudget(socketPath, timeout);
            sw.Stop();

            Assert.False(result.Succeeded);
            Assert.True(result.TimedOut, result.Failure?.ToString() ?? "Expected probe to return a timeout result.");
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

    private static LinuxDisplaySessionService CreateService(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<string, TimeSpan, LinuxDisplaySessionService.DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates)
    {
        return new LinuxDisplaySessionService(
            fileExists,
            canOpenForWrite,
            canOpenForRead,
            daemonHandshakeProbe,
            getInputEventCandidates);
    }

    private static LinuxDisplaySessionService CreateService(
        LinuxEnvironmentSnapshot environment,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<string, TimeSpan, LinuxDisplaySessionService.DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates)
    {
        return CreateService(
            new FixedEnvironmentVariables(environment),
            fileExists,
            canOpenForWrite,
            canOpenForRead,
            daemonHandshakeProbe,
            getInputEventCandidates);
    }

    private static LinuxDisplaySessionService CreateService(
        ILinuxEnvironmentVariables environmentVariables,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<string, TimeSpan, LinuxDisplaySessionService.DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates)
    {
        return CreateService(
            environmentVariables,
            fileExists,
            canOpenForWrite,
            canOpenForRead,
            () => LinuxInputProbeUtilities.HasReadableInputEventAccess(canOpenForRead, getInputEventCandidates),
            daemonHandshakeProbe,
            getInputEventCandidates);
    }

    private static LinuxDisplaySessionService CreateService(
        LinuxEnvironmentSnapshot environment,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<bool> hasUsableReadableInputDevices,
        Func<string, TimeSpan, LinuxDisplaySessionService.DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates)
    {
        return CreateService(
            new FixedEnvironmentVariables(environment),
            fileExists,
            canOpenForWrite,
            canOpenForRead,
            hasUsableReadableInputDevices,
            daemonHandshakeProbe,
            getInputEventCandidates);
    }

    private static LinuxDisplaySessionService CreateService(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<bool> hasUsableReadableInputDevices,
        Func<string, TimeSpan, LinuxDisplaySessionService.DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates)
    {
        return CreateService(
            new LinuxEnvironmentVariables(),
            fileExists,
            canOpenForWrite,
            canOpenForRead,
            hasUsableReadableInputDevices,
            daemonHandshakeProbe,
            getInputEventCandidates);
    }

    private static LinuxDisplaySessionService CreateService(
        ILinuxEnvironmentVariables environmentVariables,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<bool> hasUsableReadableInputDevices,
        Func<string, TimeSpan, LinuxDisplaySessionService.DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates)
    {
        return new LinuxDisplaySessionService(
            new LinuxInputCapabilitySnapshotProvider(
                fileExists,
                canOpenForWrite,
                canOpenForRead,
                hasUsableReadableInputDevices,
                (socketPath, timeout) => MapProbeResult(daemonHandshakeProbe(socketPath, timeout)),
                getInputEventCandidates),
            environmentVariables);
    }

    private static LinuxInputCapabilityDetector.DaemonHandshakeProbeResult MapProbeResult(
        LinuxDisplaySessionService.DaemonHandshakeProbeResult result)
    {
        return result.TimedOut
            ? LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Timeout(result.Failure)
            : result.Succeeded
                ? LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Success()
                : LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(result.Failure);
    }

    private static LinuxEnvironmentSnapshot Snapshot(
        string? flatpakId = null,
        string? useDaemon = null,
        string? sessionType = null,
        string? waylandDisplay = null,
        string? display = null)
    {
        return new LinuxEnvironmentSnapshot(
            FlatpakId: flatpakId,
            AppImage: null,
            UseDaemon: useDaemon,
            SessionType: sessionType,
            WaylandDisplay: waylandDisplay,
            Display: display,
            CurrentDesktop: null,
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: null,
            WindowButtons: null);
    }

    private sealed class FixedEnvironmentVariables(LinuxEnvironmentSnapshot environment) : ILinuxEnvironmentVariables
    {
        public LinuxEnvironmentSnapshot CaptureSnapshot()
        {
            return environment;
        }
    }

    private sealed class SequencedEnvironmentVariables(params LinuxEnvironmentSnapshot[] environments) : ILinuxEnvironmentVariables
    {
        private int _captureCount;

        public int CaptureCount => _captureCount;

        public LinuxEnvironmentSnapshot CaptureSnapshot()
        {
            var index = Math.Min(_captureCount, environments.Length - 1);
            _captureCount++;
            return environments[index];
        }
    }

}
