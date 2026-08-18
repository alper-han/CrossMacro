
namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public sealed class IpcClientResolveSocketPathTests
{
    [LinuxFact]
    public void ResolveSocketPath_WhenSocketExists_ReturnsDefaultSocketPath()
    {
        var socketPath = IpcClient.ResolveSocketPath(path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal), _ => { });

        Assert.Equal(IpcProtocol.DefaultSocketPath, socketPath);
    }

    [LinuxFact]
    public void ResolveSocketPath_WhenSocketAccessIsDenied_ThrowsPermissionDenied()
    {
        var exception = Assert.Throws<IpcClientException>(() =>
            IpcClient.ResolveSocketPath(_ => false, _ => throw new UnauthorizedAccessException("denied")));

        Assert.Equal(IpcClientFailureReason.PermissionDenied, exception.Reason);
        Assert.Contains("access denied", exception.Message, StringComparison.OrdinalIgnoreCase);
        _ = Assert.IsType<UnauthorizedAccessException>(exception.InnerException);
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
