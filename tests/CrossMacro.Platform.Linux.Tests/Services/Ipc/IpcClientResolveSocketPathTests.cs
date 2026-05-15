using System;
using System.IO;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public class IpcClientResolveSocketPathTests
{
    [LinuxFact]
    public void ResolveSocketPath_WhenSocketExists_ReturnsDefaultSocketPath()
    {
        var socketPath = IpcClient.ResolveSocketPath(path => path == IpcProtocol.DefaultSocketPath, _ => { });

        Assert.Equal(IpcProtocol.DefaultSocketPath, socketPath);
    }

    [LinuxFact]
    public void ResolveSocketPath_WhenSocketAccessIsDenied_ThrowsPermissionDenied()
    {
        var exception = Assert.Throws<IpcClientException>(() =>
            IpcClient.ResolveSocketPath(_ => false, _ => throw new UnauthorizedAccessException("denied")));

        Assert.Equal(IpcClientFailureReason.PermissionDenied, exception.Reason);
        Assert.Contains("access denied", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<UnauthorizedAccessException>(exception.InnerException);
    }

    [LinuxFact]
    public void ResolveSocketPath_WhenSocketMissing_ThrowsSocketNotFound()
    {
        var exception = Assert.Throws<IpcClientException>(() =>
            IpcClient.ResolveSocketPath(_ => false, _ => { }));

        Assert.Equal(IpcClientFailureReason.SocketNotFound, exception.Reason);
        Assert.Contains("Daemon socket not found", exception.Message, StringComparison.Ordinal);
    }
}
