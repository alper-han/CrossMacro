using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Platform.Linux;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxInputProbeUtilitiesTests
{
    [LinuxFact]
    public void ResolveAvailableSocketPath_WhenDefaultSocketExists_ReturnsDefaultSocketPath()
    {
        var socketPath = LinuxInputProbeUtilities.ResolveAvailableSocketPath(path => path == IpcProtocol.DefaultSocketPath);

        Assert.Equal(IpcProtocol.DefaultSocketPath, socketPath);
    }

    [LinuxFact]
    public void ResolveAvailableSocketPath_WhenDefaultSocketMissing_ReturnsNull()
    {
        var socketPath = LinuxInputProbeUtilities.ResolveAvailableSocketPath(_ => false);

        Assert.Null(socketPath);
    }

    [LinuxFact]
    public void HasUInputWriteAccess_WhenAlternatePathWritable_ReturnsTrue()
    {
        var result = LinuxInputProbeUtilities.HasUInputWriteAccess(path => path == LinuxConstants.UInputAlternatePath);

        Assert.True(result);
    }

    [LinuxFact]
    public void HasReadableInputEventAccess_WhenReadableCandidateExists_ReturnsTrue()
    {
        var result = LinuxInputProbeUtilities.HasReadableInputEventAccess(
            canOpenForRead: path => path == "/dev/input/event7",
            getInputEventCandidates: () => ["/dev/input/event5", "/dev/input/event7"]);

        Assert.True(result);
    }

    [LinuxFact]
    public void HasReadableInputEventAccess_WhenNoReadableCandidateExists_ReturnsFalse()
    {
        var result = LinuxInputProbeUtilities.HasReadableInputEventAccess(
            canOpenForRead: _ => false,
            getInputEventCandidates: () => ["/dev/input/event5", "/dev/input/event7"]);

        Assert.False(result);
    }
}
