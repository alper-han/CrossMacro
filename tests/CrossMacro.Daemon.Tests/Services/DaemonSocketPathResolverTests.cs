namespace CrossMacro.Daemon.Tests.Services;

using System;
using System.IO;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Daemon.Services;

public sealed class DaemonSocketPathResolverTests
{
    [Fact]
    public void ResolveSocketPath_WhenPrimaryDirectoryAlreadyExists_ReturnsDefaultSocketPath()
    {
        var checkedDirectory = string.Empty;
        var createCalled = false;
        var resolver = new DaemonSocketPathResolver(
            directoryExists: path =>
            {
                checkedDirectory = path;
                return true;
            },
            createDirectory: _ => createCalled = true);

        var result = resolver.ResolveSocketPath();

        Assert.Equal("/run/crossmacro", checkedDirectory);
        Assert.False(createCalled);
        Assert.Equal(IpcProtocol.DefaultSocketPath, result);
    }

    [Fact]
    public void ResolveSocketPath_WhenPrimaryDirectoryCanBeCreated_ReturnsDefaultSocketPath()
    {
        var createdDirectory = string.Empty;
        var checkedDirectory = string.Empty;
        var resolver = new DaemonSocketPathResolver(
            directoryExists: path =>
            {
                checkedDirectory = path;
                return false;
            },
            createDirectory: path => createdDirectory = path);

        var result = resolver.ResolveSocketPath();

        Assert.Equal("/run/crossmacro", checkedDirectory);
        Assert.Equal("/run/crossmacro", createdDirectory);
        Assert.Equal(IpcProtocol.DefaultSocketPath, result);
    }

    [Fact]
    public void ResolveSocketPath_WhenPrimaryDirectoryCannotBeCreated_Throws()
    {
        var resolver = new DaemonSocketPathResolver(
            directoryExists: _ => false,
            createDirectory: _ => throw new IOException("denied"));

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.ResolveSocketPath());
        Assert.IsType<IOException>(ex.InnerException);
        Assert.Contains("/run/crossmacro", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveSocketPath_WhenPrimaryDirectoryAccessDenied_Throws()
    {
        var resolver = new DaemonSocketPathResolver(
            directoryExists: _ => false,
            createDirectory: _ => throw new UnauthorizedAccessException("denied"));

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.ResolveSocketPath());
        Assert.IsType<UnauthorizedAccessException>(ex.InnerException);
        Assert.Contains("/run/crossmacro", ex.Message, StringComparison.Ordinal);
    }
}
