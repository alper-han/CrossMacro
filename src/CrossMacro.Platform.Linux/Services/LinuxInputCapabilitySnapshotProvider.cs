using System;
using System.IO;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.Platform.Linux.Services;

internal sealed class LinuxInputCapabilitySnapshotProvider : ILinuxInputCapabilitySnapshotProvider
{
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _canOpenForWrite;
    private readonly Func<string, bool> _canOpenForRead;
    private readonly Func<string, TimeSpan, LinuxInputCapabilityDetector.DaemonHandshakeProbeResult> _daemonHandshakeProbe;
    private readonly Func<string[]> _getInputEventCandidates;

    public LinuxInputCapabilitySnapshotProvider()
        : this(
            File.Exists,
            LinuxInputProbeUtilities.CanOpenForWrite,
            LinuxInputProbeUtilities.CanOpenForRead,
            LinuxInputCapabilityDetector.ProbeDaemonHandshakeWithinBudget,
            LinuxInputProbeUtilities.GetInputEventCandidates)
    {
    }

    public LinuxInputCapabilitySnapshotProvider(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<string, TimeSpan, LinuxInputCapabilityDetector.DaemonHandshakeProbeResult> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates)
    {
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _canOpenForWrite = canOpenForWrite ?? throw new ArgumentNullException(nameof(canOpenForWrite));
        _canOpenForRead = canOpenForRead ?? throw new ArgumentNullException(nameof(canOpenForRead));
        _daemonHandshakeProbe = daemonHandshakeProbe ?? throw new ArgumentNullException(nameof(daemonHandshakeProbe));
        _getInputEventCandidates = getInputEventCandidates ?? throw new ArgumentNullException(nameof(getInputEventCandidates));
    }

    public LinuxInputCapabilitySnapshot CaptureSnapshot(TimeSpan daemonHandshakeBudget)
    {
        var resolvedSocketPath = LinuxInputProbeUtilities.ResolveAvailableSocketPath(_fileExists);
        var daemonSocketExists = resolvedSocketPath is not null;

        var daemonProbeResult = daemonSocketExists
            ? ProbeDaemonHandshake(resolvedSocketPath!, daemonHandshakeBudget)
            : LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(LinuxDaemonHandshakeStatus.MissingSocket);

        bool canUseDirectUInput;
        try
        {
            canUseDirectUInput = LinuxInputProbeUtilities.HasUInputWriteAccess(_canOpenForWrite);
        }
        catch
        {
            canUseDirectUInput = false;
        }

        bool canReadInputEvents;
        try
        {
            canReadInputEvents = LinuxInputProbeUtilities.HasReadableInputEventAccess(_canOpenForRead, _getInputEventCandidates);
        }
        catch
        {
            canReadInputEvents = false;
        }

        return new LinuxInputCapabilitySnapshot(
            ResolvedSocketPath: resolvedSocketPath,
            DaemonSocketExists: daemonSocketExists,
            DaemonHandshakeSucceeded: daemonProbeResult.Succeeded,
            DaemonHandshakeTimedOut: daemonProbeResult.TimedOut,
            CanUseDirectUInput: canUseDirectUInput,
            CanReadInputEvents: canReadInputEvents,
            DaemonHandshakeDiagnostic: CreateDaemonHandshakeDiagnostic(resolvedSocketPath, daemonProbeResult, daemonHandshakeBudget));
    }


    private static LinuxDaemonHandshakeProbeResult CreateDaemonHandshakeDiagnostic(
        string? socketPath,
        LinuxInputCapabilityDetector.DaemonHandshakeProbeResult probeResult,
        TimeSpan timeout)
    {
        var resolvedSocketPath = socketPath ?? IpcProtocol.DefaultSocketPath;
        return probeResult.Succeeded
            ? LinuxDaemonHandshakeProbeResult.Success(resolvedSocketPath, timeout)
            : LinuxDaemonHandshakeProbeResult.Failed(
                resolvedSocketPath,
                timeout,
                probeResult.Status,
                probeResult.Failure?.Message,
                probeResult.Failure);
    }

    private LinuxInputCapabilityDetector.DaemonHandshakeProbeResult ProbeDaemonHandshake(string socketPath, TimeSpan timeout)
    {
        try
        {
            return _daemonHandshakeProbe(socketPath, timeout);
        }
        catch (Exception ex)
        {
            return LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(ex);
        }
    }
}
