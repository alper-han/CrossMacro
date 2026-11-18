
namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public sealed class IpcClientSocketResolutionTests
{
    [Fact]
    public void ResolveSocketPath_WhenSocketExists_ReturnsDefaultPath()
    {
        var result = IpcClient.ResolveSocketPath(_ => true, _ => { });

        Assert.Equal(IpcProtocol.DefaultSocketPath, result);
    }

    [Fact]
    public void ResolveSocketPath_WhenSocketFileIsMissing_ThrowsSocketNotFound()
    {
        var exception = Assert.Throws<IpcClientException>(() => IpcClient.ResolveSocketPath(
            _ => false,
            _ => throw new FileNotFoundException("socket is gone")));

        Assert.Equal(IpcClientFailureReason.SocketNotFound, exception.Reason);
        Assert.Contains("Daemon socket not found", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveSocketPath_WhenSocketDirectoryIsMissing_ThrowsSocketNotFound()
    {
        var exception = Assert.Throws<IpcClientException>(() => IpcClient.ResolveSocketPath(
            _ => false,
            _ => throw new DirectoryNotFoundException("runtime directory is gone")));

        Assert.Equal(IpcClientFailureReason.SocketNotFound, exception.Reason);
    }

    [Fact]
    public void ResolveSocketPath_WhenAccessIsDenied_ThrowsPermissionDenied()
    {
        var exception = Assert.Throws<IpcClientException>(() => IpcClient.ResolveSocketPath(
            _ => false,
            _ => throw new UnauthorizedAccessException("access denied")));

        Assert.Equal(IpcClientFailureReason.PermissionDenied, exception.Reason);
        Assert.Contains("access denied", exception.Message, StringComparison.OrdinalIgnoreCase);
        _ = Assert.IsType<UnauthorizedAccessException>(exception.InnerException);
    }

    [Fact]
    public void GetStartupFailureMessage_WhenDaemonIsDown_DoesNotBlamePolkit()
    {
        var exception = new IpcClientException(
            IpcClientFailureReason.SocketNotFound,
            "Daemon socket not found",
            new FileNotFoundException("socket is gone"));

        var message = LinuxIpcInputCapture.GetStartupFailureMessage(exception);

        Assert.DoesNotContain("Polkit", message, StringComparison.Ordinal);
        Assert.Contains("not reachable", message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetStartupFailureMessage_WhenConnectionFailsMidHandshake_KeepsTechnicalDetails()
    {
        var exception = new IpcClientException(
            IpcClientFailureReason.ConnectFailed,
            "Failed to connect to daemon.",
            new IOException("Connection reset by peer"));

        var message = LinuxIpcInputCapture.GetStartupFailureMessage(exception);

        Assert.DoesNotContain("Polkit", message, StringComparison.Ordinal);
        Assert.Contains("Connection reset by peer", message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetStartupFailureMessage_WhenHandshakeRejected_UsesDaemonProvidedReason()
    {
        var exception = new IpcClientException(
            IpcClientFailureReason.HandshakeFailed,
            "Daemon handshake error: Protocol version mismatch");

        var message = LinuxIpcInputCapture.GetStartupFailureMessage(exception);

        Assert.Contains("Protocol version mismatch", message, StringComparison.Ordinal);
    }
}
