using System;
using System.IO;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services;

public class LinuxDaemonHandshakeProbeTests
{
    [LinuxFact]
    public void ProbeStructured_WhenTransportReportsPermissionDenied_PreservesPermissionDeniedStatus()
    {
        var probe = new LinuxDaemonHandshakeProbe((_, _) => LinuxDaemonHandshakeTransport.ProbeResult.Failed(
            new UnauthorizedAccessException("access denied")));

        var result = probe.Probe("/run/crossmacro/crossmacro.sock", TimeSpan.FromSeconds(2));

        Assert.False(result.Succeeded);
        Assert.Equal(LinuxDaemonHandshakeStatus.PermissionDenied, result.Status);
        Assert.IsType<UnauthorizedAccessException>(result.Exception);
    }

    [LinuxFact]
    public void ProbeStructured_WhenTransportReportsProtocolMismatch_PreservesProtocolMismatchStatus()
    {
        var probe = new LinuxDaemonHandshakeProbe((_, _) => LinuxDaemonHandshakeTransport.ProbeResult.Failed(
            new IpcClientException(IpcClientFailureReason.ProtocolMismatch, "mismatch")));

        var result = probe.Probe("/run/crossmacro/crossmacro.sock", TimeSpan.FromSeconds(2));

        Assert.False(result.Succeeded);
        Assert.Equal(LinuxDaemonHandshakeStatus.ProtocolMismatch, result.Status);
    }
}
