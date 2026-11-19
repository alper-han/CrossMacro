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
}
