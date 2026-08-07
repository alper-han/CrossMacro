
namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public sealed class IpcClientSocketResolutionTests
{
    [Fact]
    public void ResolveSocketPath_WhenSocketExists_ReturnsDefaultPath()
    {
        var result = IpcClient.ResolveSocketPath(_ => true, _ => { });

        _ = result.Should().Be(IpcProtocol.DefaultSocketPath);
    }

    [Fact]
    public void ResolveSocketPath_WhenSocketFileIsMissing_ThrowsSocketNotFound()
    {
        var act = () => IpcClient.ResolveSocketPath(
            _ => false,
            _ => throw new FileNotFoundException("socket is gone"));

        _ = act.Should().Throw<IpcClientException>()
            .Which.Reason.Should().Be(IpcClientFailureReason.SocketNotFound);
    }

    [Fact]
    public void ResolveSocketPath_WhenSocketDirectoryIsMissing_ThrowsSocketNotFound()
    {
        var act = () => IpcClient.ResolveSocketPath(
            _ => false,
            _ => throw new DirectoryNotFoundException("runtime directory is gone"));

        _ = act.Should().Throw<IpcClientException>()
            .Which.Reason.Should().Be(IpcClientFailureReason.SocketNotFound);
    }

    [Fact]
    public void ResolveSocketPath_WhenAccessIsDenied_ThrowsPermissionDenied()
    {
        var act = () => IpcClient.ResolveSocketPath(
            _ => false,
            _ => throw new UnauthorizedAccessException("access denied"));

        _ = act.Should().Throw<IpcClientException>()
            .Which.Reason.Should().Be(IpcClientFailureReason.PermissionDenied);
    }

    [Fact]
    public void GetStartupFailureMessage_WhenDaemonIsDown_DoesNotBlamePolkit()
    {
        var exception = new IpcClientException(
            IpcClientFailureReason.SocketNotFound,
            "Daemon socket not found",
            new FileNotFoundException("socket is gone"));

        var message = LinuxIpcInputCapture.GetStartupFailureMessage(exception);

        _ = message.Should().NotContain("Polkit");
        _ = message.Should().Contain("not reachable");
    }

    [Fact]
    public void GetStartupFailureMessage_WhenConnectionFailsMidHandshake_KeepsTechnicalDetails()
    {
        var exception = new IpcClientException(
            IpcClientFailureReason.ConnectFailed,
            "Failed to connect to daemon.",
            new IOException("Connection reset by peer"));

        var message = LinuxIpcInputCapture.GetStartupFailureMessage(exception);

        _ = message.Should().NotContain("Polkit");
        _ = message.Should().Contain("Connection reset by peer");
    }

    [Fact]
    public void GetStartupFailureMessage_WhenHandshakeRejected_UsesDaemonProvidedReason()
    {
        var exception = new IpcClientException(
            IpcClientFailureReason.HandshakeFailed,
            "Daemon handshake error: Protocol version mismatch");

        var message = LinuxIpcInputCapture.GetStartupFailureMessage(exception);

        _ = message.Should().Contain("Protocol version mismatch");
    }
}
