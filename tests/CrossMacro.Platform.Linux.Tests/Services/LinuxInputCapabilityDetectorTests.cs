using System;
using CrossMacro.Core.Ipc;
using CrossMacro.Platform.Linux;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services;

public class LinuxInputCapabilityDetectorTests
{
    [LinuxFact]
    public void DetermineMode_WhenDaemonHandshakeSucceeds_ReturnsDaemon()
    {
        // Arrange
        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => path == IpcProtocol.DefaultSocketPath,
            canOpenForWrite: _ => false,
            daemonHandshakeProbe: path => path == IpcProtocol.DefaultSocketPath,
            utcNow: () => DateTime.UtcNow);

        // Act
        var mode = detector.DetermineMode();

        // Assert
        Assert.True(detector.CanConnectToDaemon);
        Assert.Equal(InputProviderMode.Daemon, mode);
    }

    [LinuxFact]
    public void DetermineMode_WhenOnlyAlternateUInputIsWritable_ReturnsLegacy()
    {
        // Arrange
        var detector = new LinuxInputCapabilityDetector(
            fileExists: _ => false,
            canOpenForWrite: path => path == LinuxConstants.UInputAlternatePath,
            daemonHandshakeProbe: _ => false,
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
            fileExists: path => path == IpcProtocol.DefaultSocketPath,
            canOpenForWrite: path => path == LinuxConstants.UInputAlternatePath,
            daemonHandshakeProbe: _ => false,
            utcNow: () => DateTime.UtcNow);

        // Act
        var mode = detector.DetermineMode();

        // Assert
        Assert.False(detector.CanConnectToDaemon);
        Assert.Equal(InputProviderMode.Legacy, mode);
    }

    [LinuxFact]
    public void DetermineMode_WhenSocketExistsButHandshakeFailsAndUInputUnavailable_KeepsDaemonMode()
    {
        // Arrange
        var detector = new LinuxInputCapabilityDetector(
            fileExists: path => path == IpcProtocol.DefaultSocketPath,
            canOpenForWrite: _ => false,
            daemonHandshakeProbe: _ => false,
            utcNow: () => DateTime.UtcNow);

        // Act
        var mode = detector.DetermineMode();

        // Assert
        Assert.False(detector.CanConnectToDaemon);
        Assert.Equal(InputProviderMode.Daemon, mode);
    }

    [LinuxFact]
    public void DetermineMode_WhenDaemonPreviouslySucceededAndSocketStillExists_KeepsDaemonModeWithoutReprobe()
    {
        // Arrange
        var probeCount = 0;
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var detector = new LinuxInputCapabilityDetector(
            fileExists: path =>
                path == IpcProtocol.DefaultSocketPath ||
                path == LinuxConstants.UInputAlternatePath,
            canOpenForWrite: path => path == LinuxConstants.UInputAlternatePath,
            daemonHandshakeProbe: _ =>
            {
                probeCount++;
                return probeCount == 1;
            },
            utcNow: () => now);

        // Act
        var firstMode = detector.DetermineMode();
        now = now.AddSeconds(6);
        var secondMode = detector.DetermineMode();

        // Assert
        Assert.Equal(InputProviderMode.Daemon, firstMode);
        Assert.Equal(InputProviderMode.Daemon, secondMode);
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
                (path == IpcProtocol.DefaultSocketPath && socketAvailable) ||
                path == LinuxConstants.UInputAlternatePath,
            canOpenForWrite: path => path == LinuxConstants.UInputAlternatePath,
            daemonHandshakeProbe: _ =>
            {
                probeCount++;
                return true;
            },
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
}
