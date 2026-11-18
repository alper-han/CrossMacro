using CrossMacro.Platform.Linux.Native;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxInputProbeUtilitiesTests
{
    [Fact]
    public void ResolveAvailableSocketPath_WhenDefaultSocketExists_ReturnsDefaultSocketPath()
    {
        var socketPath = LinuxInputProbeUtilities.ResolveAvailableSocketPath(path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal));

        Assert.Equal(IpcProtocol.DefaultSocketPath, socketPath);
    }

    [Fact]
    public void ResolveAvailableSocketPath_WhenDefaultSocketMissing_ReturnsNull()
    {
        var socketPath = LinuxInputProbeUtilities.ResolveAvailableSocketPath(_ => false);

        Assert.Null(socketPath);
    }

    [Fact]
    public void HasUInputWriteAccess_WhenAlternatePathWritable_ReturnsTrue()
    {
        var result = LinuxInputProbeUtilities.HasUInputWriteAccess(path => string.Equals(path, LinuxSystemPaths.UInputAlternatePath, StringComparison.Ordinal));

        Assert.True(result);
    }

    [Fact]
    public void HasUInputWriteAccess_WhenNeitherPathWritable_ReturnsFalse()
    {
        Assert.False(LinuxInputProbeUtilities.HasUInputWriteAccess(_ => false));
    }

    [Fact]
    public void CanOpenForWrite_WhenFileIsMissing_ReturnsFalse()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crossmacro-missing-{Guid.NewGuid():N}");

        Assert.False(LinuxInputProbeUtilities.CanOpenForWrite(path));
    }

    [Fact]
    public void CanOpenForWrite_WhenFileIsWritable_ReturnsTrue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crossmacro-writable-{Guid.NewGuid():N}");
        File.WriteAllText(path, string.Empty);
        try
        {
            Assert.True(LinuxInputProbeUtilities.CanOpenForWrite(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LinuxDaemonDiagnosticSnapshot_WhenSocketAndHandshakeSucceed_CanUseDaemon()
    {
        var socketAccess = LinuxDaemonSocketAccessResult.Accessible(
            IpcProtocol.DefaultSocketPath,
            LinuxDaemonGroupMembershipStatus.Member);
        var fallback = LinuxDirectInputFallbackResult.FromAccess(canWriteUInput: false, canReadInputEvents: false);
        var handshake = LinuxDaemonHandshakeProbeResult.Success(IpcProtocol.DefaultSocketPath, TimeSpan.FromSeconds(1));

        var snapshot = new LinuxDaemonDiagnosticSnapshot(
            IpcProtocol.DefaultSocketPath,
            socketAccess,
            LinuxDaemonGroupMembershipStatus.Member,
            fallback,
            handshake);

        Assert.True(snapshot.CanUseDaemon);
        Assert.Equal(LinuxDaemonGroupMembershipStatus.Member, snapshot.GroupMembershipStatus);
        Assert.Equal(LinuxDirectInputFallbackStatus.MissingUInputWriteAccess, snapshot.DirectInputFallback.Status);
    }
}
