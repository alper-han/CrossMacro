namespace CrossMacro.Daemon.Tests.Services;

using System;
using System.IO;
using CrossMacro.Daemon.Services;
using CrossMacro.Infrastructure.Linux.Native;
using CrossMacro.TestInfrastructure;

public class LinuxPermissionServiceTests
{
    private const string SocketPath = "/run/crossmacro/crossmacro.sock";

    [Fact]
    public void ConfigureSocketPermissions_WhenSocketPathDoesNotExist_DoesNotThrow()
    {
        var service = new LinuxPermissionService();
        var missingPath = Path.Combine(Path.GetTempPath(), $"crossmacro-permission-test-missing-{Guid.NewGuid():N}");

        var ex = Record.Exception(() => service.ConfigureSocketPermissions(missingPath));

        Assert.Null(ex);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenSocketPathExists_DoesNotThrow()
    {
        var service = new LinuxPermissionService();
        var socketPath = Path.Combine(Path.GetTempPath(), $"crossmacro-permission-test-existing-{Guid.NewGuid():N}");
        File.WriteAllText(socketPath, string.Empty);

        try
        {
            var ex = Record.Exception(() => service.ConfigureSocketPermissions(socketPath));
            Assert.Null(ex);
        }
        finally
        {
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenCrossmacroGroupExists_ChownsSocketToGroupAndSetsMode()
    {
        var chownCalls = new List<(string Path, int Owner, int Group)>();
        var modePaths = new List<string>();
        var service = new LinuxPermissionService(
            fileExists: path => path == LinuxSystemPaths.GroupFile,
            readLines: path =>
            {
                Assert.Equal(LinuxSystemPaths.GroupFile, path);
                return ["root:x:0:", "crossmacro:x:4242:test-user"];
            },
            chown: (path, owner, group) =>
            {
                chownCalls.Add((path, owner, group));
                return 0;
            },
            setSocketPermissions: path => modePaths.Add(path));

        var ex = Record.Exception(() => service.ConfigureSocketPermissions(SocketPath));

        Assert.Null(ex);
        var call = Assert.Single(chownCalls);
        Assert.Equal((SocketPath, -1, 4242), call);
        Assert.Equal([SocketPath], modePaths);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenCrossmacroGroupMissing_SkipsChownButStillSetsMode()
    {
        var chownCalls = new List<(string Path, int Owner, int Group)>();
        var modePaths = new List<string>();
        var service = new LinuxPermissionService(
            fileExists: _ => true,
            readLines: _ => ["root:x:0:", "input:x:100:test-user"],
            chown: (path, owner, group) =>
            {
                chownCalls.Add((path, owner, group));
                return 0;
            },
            setSocketPermissions: path => modePaths.Add(path));

        var ex = Record.Exception(() => service.ConfigureSocketPermissions(SocketPath));

        Assert.Null(ex);
        Assert.Empty(chownCalls);
        Assert.Equal([SocketPath], modePaths);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenGroupLookupFails_DoesNotChownButStillSetsMode()
    {
        var chownCalls = new List<(string Path, int Owner, int Group)>();
        var modePaths = new List<string>();
        var service = new LinuxPermissionService(
            fileExists: _ => throw new IOException("group file unavailable"),
            readLines: _ => throw new InvalidOperationException("should not read"),
            chown: (path, owner, group) =>
            {
                chownCalls.Add((path, owner, group));
                return 0;
            },
            setSocketPermissions: path => modePaths.Add(path));

        var ex = Record.Exception(() => service.ConfigureSocketPermissions(SocketPath));

        Assert.Null(ex);
        Assert.Empty(chownCalls);
        Assert.Equal([SocketPath], modePaths);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenChownThrows_StillSetsMode()
    {
        var modePaths = new List<string>();
        var service = new LinuxPermissionService(
            fileExists: _ => true,
            readLines: _ => ["crossmacro:x:4242:test-user"],
            chown: (_, _, _) => throw new EntryPointNotFoundException("chown unavailable"),
            setSocketPermissions: path => modePaths.Add(path));

        var ex = Record.Exception(() => service.ConfigureSocketPermissions(SocketPath));

        Assert.Null(ex);
        Assert.Equal([SocketPath], modePaths);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenChownFailsAndModeFails_ThrowsModeFailure()
    {
        var chownCalls = new List<(string Path, int Owner, int Group)>();
        var service = new LinuxPermissionService(
            fileExists: _ => true,
            readLines: _ => ["crossmacro:x:4242:test-user"],
            chown: (path, owner, group) =>
            {
                chownCalls.Add((path, owner, group));
                return -1;
            },
            setSocketPermissions: _ => throw new IOException("chmod failed"));

        var ex = Assert.Throws<IOException>(() => service.ConfigureSocketPermissions(SocketPath));

        Assert.Equal("chmod failed", ex.Message);
        var call = Assert.Single(chownCalls);
        Assert.Equal((SocketPath, -1, 4242), call);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenChownSucceedsAndModeFails_ThrowsModeFailure()
    {
        var service = new LinuxPermissionService(
            fileExists: _ => true,
            readLines: _ => ["crossmacro:x:4242:test-user"],
            chown: (_, _, _) => 0,
            setSocketPermissions: _ => throw new IOException("chmod failed"));

        var ex = Assert.Throws<IOException>(() => service.ConfigureSocketPermissions(SocketPath));

        Assert.Equal("chmod failed", ex.Message);
    }

    [LinuxFact]
    public void ConfigureSocketPermissions_DefaultConstructorUsesRealChownEntryPoint()
    {
        var socketPath = Path.Combine(Path.GetTempPath(), $"crossmacro-permission-test-real-entrypoint-{Guid.NewGuid():N}");
        File.WriteAllText(socketPath, string.Empty);

        try
        {
            var service = new LinuxPermissionService();

            var ex = Record.Exception(() => service.ConfigureSocketPermissions(socketPath));

            Assert.Null(ex);
        }
        finally
        {
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }
}
